using Microsoft.Extensions.Options;
using Quartz;
using ZkFingerBridge;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Jobs;
using ZkFingerBridge.Services;

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
