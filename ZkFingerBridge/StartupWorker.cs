using Microsoft.Extensions.Options;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Services;

namespace ZkFingerBridge;

public class StartupWorker : BackgroundService
{
    private readonly ILogger<StartupWorker> _logger;
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly ZkDeviceOptions _deviceOptions;

    public StartupWorker(
        ILogger<StartupWorker> logger,
        IDeviceDiscoveryService discoveryService,
        IOptions<ZkDeviceOptions> deviceOptions)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        _deviceOptions = deviceOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ZK Finger bridge starting up...");

        // Auto-discover device IP if needed
        await TryAutoDiscoverDeviceAsync(stoppingToken);

        _logger.LogInformation("Startup complete. Quartz scheduler will handle sync jobs.");
        
        // This worker completes after startup
    }

    private async Task TryAutoDiscoverDeviceAsync(CancellationToken cancellationToken)
    {
        if (!_deviceOptions.EnableAutoDiscovery)
        {
            return;
        }

        // If IP is set to "auto", force auto-discovery
        if (_deviceOptions.IpAddress.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("IP address is set to 'auto'. Running device discovery...");
            await DiscoverAndUpdateIpAsync(cancellationToken);
            return;
        }

        // Otherwise, just log the configured IP
        _logger.LogInformation("Using configured device IP: {IpAddress}:{Port}", 
            _deviceOptions.IpAddress, _deviceOptions.Port);
    }

    private async Task DiscoverAndUpdateIpAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_deviceOptions.AutoDiscoverySubnet))
        {
            _logger.LogWarning("AutoDiscoverySubnet is not configured. Skipping auto-discovery.");
            return;
        }

        _logger.LogInformation("Scanning subnet {Subnet}.0/24 for ZKTeco devices on port {Port}...",
            _deviceOptions.AutoDiscoverySubnet, _deviceOptions.Port);

        var deviceInfo = await _discoveryService.DiscoverDeviceAsync(
            _deviceOptions.AutoDiscoverySubnet,
            _deviceOptions.Port);

        if (deviceInfo != null)
        {
            _logger.LogInformation("Auto-discovery successful! Updating device IP from {OldIP} to {NewIP} (S/N: {Serial}, Model: {Model})",
                _deviceOptions.IpAddress,
                deviceInfo.IpAddress,
                deviceInfo.SerialNumber ?? "Unknown",
                deviceInfo.DeviceModel ?? "Unknown");
            _deviceOptions.IpAddress = deviceInfo.IpAddress;
        }
        else
        {
            _logger.LogError("Auto-discovery failed. No ZKTeco device found on subnet {Subnet}.0/24 port {Port}",
                _deviceOptions.AutoDiscoverySubnet, _deviceOptions.Port);
        }
    }
}
