using Microsoft.Extensions.Options;
using Quartz;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Models;
using ZkFingerBridge.Services;

namespace ZkFingerBridge.Jobs;

/// <summary>
/// Main synchronization job that runs on a schedule (every 10 seconds by default).
/// This job:
/// 1. Retries any queued logs from previous failed attempts
/// 2. Reads new attendance logs from the ZKTeco device
/// 3. Sends logs to the HR API
/// 4. Queues logs if API is unavailable (offline resilience)
/// </summary>
[DisallowConcurrentExecution] // Prevents multiple instances of this job running at the same time
public class SyncJob : IJob
{
    // Dependencies injected via constructor
    private readonly ILogger<SyncJob> _logger;                  // For logging messages
    private readonly IZkDeviceClient _deviceClient;             // Communicates with ZKTeco device
    private readonly IHrApiClient _hrApiClient;                 // Sends data to HR API
    private readonly IStateStore _stateStore;                   // Tracks last sync timestamp
    private readonly ILogQueue _logQueue;                       // Offline queue (SQLite)
    private readonly SyncOptions _syncOptions;                  // Sync configuration
    private readonly ZkDeviceOptions _deviceOptions;            // Device configuration
    private readonly QueueOptions _queueOptions;                // Queue configuration

    /// <summary>
    /// Constructor - all dependencies are injected by .NET's dependency injection system
    /// </summary>
    public SyncJob(
        ILogger<SyncJob> logger,
        IZkDeviceClient deviceClient,
        IHrApiClient hrApiClient,
        IStateStore stateStore,
        ILogQueue logQueue,
        IOptions<SyncOptions> syncOptions,
        IOptions<ZkDeviceOptions> deviceOptions,
        IOptions<QueueOptions> queueOptions)
    {
        _logger = logger;
        _deviceClient = deviceClient;
        _hrApiClient = hrApiClient;
        _stateStore = stateStore;
        _logQueue = logQueue;
        _syncOptions = syncOptions.Value;           // Extract value from IOptions wrapper
        _deviceOptions = deviceOptions.Value;
        _queueOptions = queueOptions.Value;
    }

    /// <summary>
    /// Main execution method called by Quartz scheduler.
    /// This runs every X seconds (configured in appsettings.json)
    /// </summary>
    public async Task Execute(IJobExecutionContext context)
    {
        // Get cancellation token from Quartz context (used to stop gracefully)
        var cancellationToken = context.CancellationToken;
        
        _logger.LogInformation("Starting sync cycle");

        try
        {
            // ═══════════════════════════════════════════════════════════════
            // STEP 1: Process Offline Queue First
            // ═══════════════════════════════════════════════════════════════
            // Try to send any logs that were queued from previous failed attempts
            // This ensures we don't lose old data - prioritize sending queued logs
            await TryProcessQueueAsync(cancellationToken);

            // ═══════════════════════════════════════════════════════════════
            // STEP 2: Read New Logs from Device
            // ═══════════════════════════════════════════════════════════════
            // Get the last sync timestamp (to avoid re-reading old logs)
            var state = await _stateStore.ReadAsync(cancellationToken);
            
            // Connect to ZKTeco device and read all attendance logs
            var deviceLogs = await _deviceClient.ReadLogsAsync(cancellationToken);

            // Filter out logs we've already synced (only keep new ones)
            var filteredLogs = Filter(deviceLogs, state).ToList();
            
            // If no new logs, we're done
            if (filteredLogs.Count == 0)
            {
                _logger.LogInformation("No new logs to sync");
                
                // Optionally clear logs from device after reading
                if (_syncOptions.ClearDeviceLogsAfterSync)
                {
                    await _deviceClient.ClearLogsAsync(cancellationToken);
                }
                return;
            }

            // ═══════════════════════════════════════════════════════════════
            // STEP 3: Try to Send Logs to API
            // ═══════════════════════════════════════════════════════════════
            try
            {
                // Attempt to send logs to HR API
                await _hrApiClient.SendAsync(filteredLogs, cancellationToken);
                
                // ✅ SUCCESS! API accepted the logs
                
                // Update sync state with the newest timestamp
                // This prevents re-syncing these logs on the next cycle
                var newestTimestamp = filteredLogs.Max(log => log.PunchTime);
                await _stateStore.WriteAsync(new SyncState(newestTimestamp), cancellationToken);

                // Optionally clear logs from device after successful sync
                if (_syncOptions.ClearDeviceLogsAfterSync)
                {
                    await _deviceClient.ClearLogsAsync(cancellationToken);
                }

                _logger.LogInformation("Synced {Count} log(s)", filteredLogs.Count);
            }
            catch (Exception ex)
            {
                // ❌ FAILED! API is unavailable or returned an error
                
                _logger.LogWarning(ex, "Failed to send logs to API. Queuing for offline storage...");
                
                // Save logs to SQLite queue for later retry
                // BranchId is included to identify which branch these logs came from
                await _logQueue.EnqueueAsync(filteredLogs, _deviceOptions.BranchId, cancellationToken);
                
                // Still update sync state even though API failed
                // This prevents re-reading the same logs from the device on next cycle
                // The logs are safely stored in the queue and will be retried
                var newestTimestamp = filteredLogs.Max(log => log.PunchTime);
                await _stateStore.WriteAsync(new SyncState(newestTimestamp), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Unexpected error (e.g., device connection failed, database error)
            _logger.LogError(ex, "Unexpected error during sync cycle");
            throw; // Re-throw so Quartz can handle it (retry if configured)
        }
    }

    /// <summary>
    /// Attempts to send any logs that are in the offline queue.
    /// This runs before reading new logs from the device to prioritize old data.
    /// </summary>
    private async Task TryProcessQueueAsync(CancellationToken cancellationToken)
    {
        // Check how many logs are waiting in the queue
        var pendingCount = await _logQueue.GetPendingCountAsync(cancellationToken);
        if (pendingCount == 0)
        {
            // No queued logs, nothing to do
            return;
        }

        _logger.LogInformation("📤 Processing {Count} queued log(s)...", pendingCount);

        // Get all pending logs that haven't exceeded max retry attempts
        var queuedLogs = await _logQueue.GetPendingAsync(_queueOptions.MaxRetryAttempts, cancellationToken);

        // Try to send each queued log
        foreach (var queuedLog in queuedLogs)
        {
            try
            {
                // Convert QueuedLog (database entity) back to AttendanceLog (API model)
                // Include BranchId so the log is sent to the correct branch
                var attendanceLog = new AttendanceLog(
                    queuedLog.EmployeeId,
                    queuedLog.PunchTime,
                    queuedLog.VerifyMode,
                    queuedLog.PunchType,
                    queuedLog.WorkCode,
                    queuedLog.BranchId  // Pass the stored BranchId for correct routing
                );

                // Try to send this single log to the API
                await _hrApiClient.SendAsync(new[] { attendanceLog }, cancellationToken);

                // ✅ Success! Mark this log as sent in the database
                await _logQueue.MarkAsSentAsync(queuedLog.Id, cancellationToken);
                _logger.LogInformation("✅ Sent queued log {LogId}", queuedLog.Id);
            }
            catch (Exception ex)
            {
                // ❌ Failed again - increment retry count and save error message
                // The log will be retried on the next sync cycle (up to MaxRetryAttempts)
                await _logQueue.MarkAsFailedAsync(queuedLog.Id, ex.Message, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Filters logs to only include new ones that haven't been synced yet.
    /// Uses the LastSyncedAt timestamp from SyncState to determine what's new.
    /// </summary>
    /// <param name="logs">All logs from the device</param>
    /// <param name="state">Sync state containing last sync timestamp</param>
    /// <returns>Only logs newer than the last sync</returns>
    private static IEnumerable<AttendanceLog> Filter(IEnumerable<AttendanceLog> logs, SyncState state)
    {
        // Order logs by punch time (oldest first)
        var ordered = logs.OrderBy(log => log.PunchTime);
        
        // If this is the first sync (no previous state), return all logs
        if (state.LastSyncedAt is null)
        {
            return ordered;
        }

        // Otherwise, only return logs that are newer than the last sync
        // This prevents re-syncing the same logs multiple times
        return ordered.Where(log => log.PunchTime > state.LastSyncedAt.Value);
    }
}
