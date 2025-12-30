using Microsoft.Extensions.Options;
using ZkFingerBridge.Configuration;

namespace ZkFingerBridge.Services;

/// <summary>
/// Background worker that periodically discovers ZK devices on the network
/// </summary>
public class DiscoveryWorker : BackgroundService
{
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly DiscoveryOptions _options;
    private readonly ILogger<DiscoveryWorker> _logger;

    public DiscoveryWorker(
        IDeviceDiscoveryService discoveryService,
        IDeviceRegistry deviceRegistry,
        IOptions<DiscoveryOptions> options,
        ILogger<DiscoveryWorker> logger)
    {
        _discoveryService = discoveryService;
        _deviceRegistry = deviceRegistry;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Device discovery is disabled");
            return;
        }

        _logger.LogInformation("Discovery worker starting...");

        // Initial discovery on startup
        if (_options.ScanOnStartup)
        {
            await DiscoverAndUpdateDevicesAsync(stoppingToken);
        }

        // Periodic scanning
        if (_options.PeriodicScanIntervalSeconds > 0)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PeriodicScanIntervalSeconds));
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                    await DiscoverAndUpdateDevicesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Service is stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during periodic device discovery");
                }
            }
        }

        _logger.LogInformation("Discovery worker stopped");
    }

    private async Task DiscoverAndUpdateDevicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🔍 Starting device discovery...");
            
            var devices = await _discoveryService.DiscoverDevicesAsync(
                _options.Subnets,
                _options.Port,
                _options.ConnectionTimeoutSeconds * 1000);

            _deviceRegistry.UpdateDevices(devices);
            
            if (devices.Count == 0)
            {
                _logger.LogWarning("⚠ No devices found. Ensure ZK devices are powered on and on the network");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover devices");
        }
    }
}
