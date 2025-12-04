using Microsoft.Extensions.Options;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Models;
using ZkFingerBridge.Services;

namespace ZkFingerBridge;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IZkDeviceClient _deviceClient;
    private readonly IHrApiClient _hrApiClient;
    private readonly IStateStore _stateStore;
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly SyncOptions _syncOptions;
    private readonly ZkDeviceOptions _deviceOptions;

    public Worker(
        ILogger<Worker> logger,
        IZkDeviceClient deviceClient,
        IHrApiClient hrApiClient,
        IStateStore stateStore,
        IDeviceDiscoveryService discoveryService,
        IOptions<SyncOptions> syncOptions,
        IOptions<ZkDeviceOptions> deviceOptions)
    {
        _logger = logger;
        _deviceClient = deviceClient;
        _hrApiClient = hrApiClient;
        _stateStore = stateStore;
        _discoveryService = discoveryService;
        _syncOptions = syncOptions.Value;
        _deviceOptions = deviceOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ZK Finger bridge worker started");

        // Auto-discover device IP if needed
        await TryAutoDiscoverDeviceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // host is shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during sync cycle");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_syncOptions.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("ZK Finger bridge worker stopping");
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

        // Otherwise, test if configured IP is reachable
        _logger.LogInformation("Testing connection to configured IP: {IpAddress}", _deviceOptions.IpAddress);
        
        // We'll let the first sync attempt handle the connection test
        // If it fails, the error will be logged and we can add retry logic here if needed
    }

    private async Task DiscoverAndUpdateIpAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_deviceOptions.AutoDiscoverySubnet))
        {
            _logger.LogWarning("Auto-discovery enabled but AutoDiscoverySubnet is not configured. Using default: 192.168.0");
            _deviceOptions.AutoDiscoverySubnet = "192.168.0";
        }

        var deviceInfo = await _discoveryService.DiscoverDeviceAsync(
            _deviceOptions.AutoDiscoverySubnet,
            _deviceOptions.Port,
            timeoutMs: 5000);

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

    private async Task SyncOnceAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting sync cycle");

        var state = await _stateStore.ReadAsync(cancellationToken);
        var deviceLogs = await _deviceClient.ReadLogsAsync(cancellationToken);

        var filteredLogs = Filter(deviceLogs, state).ToList();
        if (filteredLogs.Count == 0)
        {
            _logger.LogInformation("No new logs to sync");
            if (_syncOptions.ClearDeviceLogsAfterSync)
            {
                await _deviceClient.ClearLogsAsync(cancellationToken);
            }
            return;
        }

        await _hrApiClient.SendAsync(filteredLogs, cancellationToken);

        var newestTimestamp = filteredLogs.Max(log => log.PunchTime);
        await _stateStore.WriteAsync(new SyncState(newestTimestamp), cancellationToken);

        if (_syncOptions.ClearDeviceLogsAfterSync)
        {
            await _deviceClient.ClearLogsAsync(cancellationToken);
        }

        _logger.LogInformation("Synced {Count} log(s)", filteredLogs.Count);
    }

    private static IEnumerable<AttendanceLog> Filter(IEnumerable<AttendanceLog> logs, SyncState state)
    {
        var ordered = logs.OrderBy(log => log.PunchTime);
        if (state.LastSyncedAt is null)
        {
            return ordered;
        }

        return ordered.Where(log => log.PunchTime > state.LastSyncedAt.Value);
    }
}
