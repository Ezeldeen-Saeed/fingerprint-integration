using Microsoft.Extensions.Options;
using ZkFingerBridge;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Services;

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services.AddSingleton<IZkDeviceClient, ZkDeviceClient>();
builder.Services.AddSingleton<IStateStore, FileStateStore>();

builder.Services.AddHttpClient<IHrApiClient, HrApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<HrApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
