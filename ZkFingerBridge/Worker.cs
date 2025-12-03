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
    private readonly SyncOptions _syncOptions;

    public Worker(
        ILogger<Worker> logger,
        IZkDeviceClient deviceClient,
        IHrApiClient hrApiClient,
        IStateStore stateStore,
        IOptions<SyncOptions> syncOptions)
    {
        _logger = logger;
        _deviceClient = deviceClient;
        _hrApiClient = hrApiClient;
        _stateStore = stateStore;
        _syncOptions = syncOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ZK Finger bridge worker started");

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
