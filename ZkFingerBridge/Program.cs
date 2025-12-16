using Microsoft.Extensions.Options;
using Quartz;
using ZkFingerBridge;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Jobs;
using ZkFingerBridge.Services;
using ZkFingerBridge.UI;

// Check for --setup flag or if not configured
var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var forceSetup = args.Contains("--setup");
var needsSetup = forceSetup || !SettingsSaver.IsConfigured(appSettingsPath);

if (needsSetup)
{
    // Read current settings to get API URL
    var tempBuilder = Host.CreateApplicationBuilder(args);
    var hrApiOptions = tempBuilder.Configuration.GetSection(HrApiOptions.SectionName).Get<HrApiOptions>() 
        ?? new HrApiOptions();

    // Run the setup wizard
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    
    using var wizard = new SetupWizard(
        hrApiOptions.BaseUrl, 
        hrApiOptions.CompaniesEndpoint,
        hrApiOptions.ApiKey);
    
    Application.Run(wizard);
    
    if (wizard.ConfigurationSaved)
    {
        // Save the selected company and branch to appsettings.json
        SettingsSaver.SaveSettings(
            appSettingsPath,
            wizard.SelectedCompanyId,
            wizard.SelectedCompanyName,
            wizard.SelectedBranchId,
            wizard.SelectedBranchName);
        
        Console.WriteLine($"✅ Configuration saved: {wizard.SelectedCompanyName} - {wizard.SelectedBranchName}");
    }
    else
    {
        Console.WriteLine("❌ Setup cancelled. Exiting...");
        return;
    }
}

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services
    .AddOptions<ZkDeviceOptions>()
    .Bind(builder.Configuration.GetSection(ZkDeviceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<HrApiOptions>()
    .Bind(builder.Configuration.GetSection(HrApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<SyncOptions>()
    .Bind(builder.Configuration.GetSection(SyncOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<QueueOptions>()
    .Bind(builder.Configuration.GetSection("Queue"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Services
builder.Services.AddSingleton<IZkDeviceClient, ZkDeviceClient>();
builder.Services.AddSingleton<IStateStore, FileStateStore>();
builder.Services.AddSingleton<IDeviceDiscoveryService, DeviceDiscoveryService>();
builder.Services.AddSingleton<ILogQueue, SqliteLogQueue>();

builder.Services.AddHttpClient<IHrApiClient, HrApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<HrApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

// Quartz.NET Configuration
builder.Services.AddQuartz(q =>
{
    // Define the sync job
    var syncJobKey = new JobKey("sync-job");
    q.AddJob<SyncJob>(opts => opts.WithIdentity(syncJobKey));

    // Configure trigger from appsettings
    q.AddTrigger(opts =>
    {
        var syncOptions = builder.Configuration.GetSection(SyncOptions.SectionName).Get<SyncOptions>();
        var intervalSeconds = syncOptions?.IntervalSeconds ?? 60;

        opts.ForJob(syncJobKey)
            .WithIdentity("sync-trigger")
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(intervalSeconds)
                .RepeatForever())
            .StartNow(); // Start immediately
    });
});

// Add Quartz hosted service
builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true; // Wait for jobs to finish on shutdown
});

// Add startup job for device discovery
builder.Services.AddHostedService<StartupWorker>();

var host = builder.Build();
host.Run();
