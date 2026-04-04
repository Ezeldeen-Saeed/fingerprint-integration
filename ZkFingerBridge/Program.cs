using Microsoft.Extensions.Options;
using Quartz;
using ZkFingerBridge;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Jobs;
using ZkFingerBridge.Services;
using ZkFingerBridge.UI;
using Serilog;

// Configure Serilog for file logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "log-.txt"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("ZkFingerBridge starting up...");


// Check for --setup flag or if not configured
var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var forceSetup = args.Contains("--setup");
var needsSetup = forceSetup || !SettingsSaver.IsConfigured(appSettingsPath);

if (needsSetup)
{
    // Read current settings to get API URL - explicitly set content root to match where we save settings
    var tempBuilder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });
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
        // Save the selected company, branch, and device IP to appsettings.json
        SettingsSaver.SaveSettings(
            appSettingsPath,
            wizard.SelectedCompanyId,
            wizard.SelectedCompanyName,
            wizard.SelectedBranchId,
            wizard.SelectedBranchName,
            wizard.DeviceIpAddress);
        
        Console.WriteLine($"✅ Configuration saved: {wizard.SelectedCompanyName} (ID: {wizard.SelectedCompanyId}) - {wizard.SelectedBranchName} (ID: {wizard.SelectedBranchId}) - Device: {wizard.DeviceIpAddress}");
    }
    else
    {
        Console.WriteLine("❌ Setup cancelled. Exiting...");
        return;
    }
}

// Explicitly set content root to AppContext.BaseDirectory to read the updated appsettings.json
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Add Serilog
builder.Services.AddSerilog();

// Enable Windows Service support - allows running as a Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ZkFingerBridge";
});

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

builder.Services
    .AddOptions<DiscoveryOptions>()
    .Bind(builder.Configuration.GetSection(DiscoveryOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Services
builder.Services.AddSingleton<IDeviceRegistry, DeviceRegistry>(); // Register this FIRST
builder.Services.AddSingleton<IDeviceConfigurationHolder, DeviceConfigurationHolder>();
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

// Add discovery worker for periodic device scanning
builder.Services.AddHostedService<DiscoveryWorker>();

// Disabled StartupWorker - DiscoveryWorker handles discovery now
// builder.Services.AddHostedService<StartupWorker>();

var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("ZkFingerBridge shutting down...");
    Log.CloseAndFlush();
}
