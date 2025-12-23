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

            // DEBUG: Show what we got from the device
            _logger.LogInformation("📊 Total logs from device: {Total}", deviceLogs.Count);
            _logger.LogInformation("📊 Last synced at: {LastSync}", state.LastSyncedAt?.ToString() ?? "Never");

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

                _logger.LogInformation("✅ Successfully synced {Count} log(s) to HR API!", filteredLogs.Count);
                foreach (var log in filteredLogs)
                {
                    _logger.LogInformation("   📋 EmployeeId: {EmployeeId}, PunchTime: {PunchTime}", log.EmployeeId, log.PunchTime);
                }
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
    /// Logs are sent in batches to reduce HTTP requests.
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

        _logger.LogInformation("📤 Processing {Count} queued log(s) in batches of {BatchSize}...", 
            pendingCount, _queueOptions.BatchSize);

        // Get all pending logs that haven't exceeded max retry attempts
        var queuedLogs = await _logQueue.GetPendingAsync(_queueOptions.MaxRetryAttempts, cancellationToken);

        // Group logs by BranchId first (API expects same branch per request)
        var groupedByBranch = queuedLogs.GroupBy(log => log.BranchId);

        foreach (var branchGroup in groupedByBranch)
        {
            // Then batch each branch group
            var batches = branchGroup.Chunk(_queueOptions.BatchSize);
            
            foreach (var batch in batches)
            {
                await ProcessBatchAsync(batch.ToList(), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Processes a single batch of queued logs
    /// </summary>
    private async Task ProcessBatchAsync(List<QueuedLog> batch, CancellationToken cancellationToken)
    {
        try
        {
            // Convert batch to AttendanceLog[]
            var attendanceLogs = batch.Select(q => new AttendanceLog(
                q.EmployeeId,
                q.PunchTime,
                q.VerifyMode,
                q.PunchType,
                q.WorkCode,
                q.BranchId
            )).ToArray();

            // Send entire batch in one request
            var response = await _hrApiClient.SendAsync(attendanceLogs, cancellationToken);

            // Handle response and mark logs accordingly
            await HandleBatchResponseAsync(batch, response, cancellationToken);
        }
        catch (Exception ex)
        {
            // Network/API error - mark all logs in batch as failed for retry
            _logger.LogError(ex, "Failed to send batch of {Count} logs", batch.Count);
            
            foreach (var log in batch)
            {
                await _logQueue.MarkAsFailedAsync(log.Id, ex.Message, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Handles the API response for a batch, marking logs appropriately based on success/failure
    /// </summary>
    private async Task HandleBatchResponseAsync(
        List<QueuedLog> batch, 
        BiometricLogsResponse response, 
        CancellationToken cancellationToken)
    {
        // Extract unmatched employee details from response
        var unmatchedEmployees = response.Errors?.UnmatchedEmployees?.Details
            ?? new List<UnmatchedEmployeeError>();
        
        var unmatchedEmployeeIds = unmatchedEmployees
            .Select(e => e.EmployeeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int sentCount = 0;
        int unmatchedCount = 0;

        foreach (var log in batch)
        {
            if (unmatchedEmployeeIds.Contains(log.EmployeeId))
            {
                // Find the specific error for this employee
                var errorDetail = unmatchedEmployees.FirstOrDefault(
                    e => e.EmployeeId.Equals(log.EmployeeId, StringComparison.OrdinalIgnoreCase));
                
                var errorMessage = errorDetail?.Error ?? "Employee not found in HR system";
                
                // Log the specific employee and error
                _logger.LogWarning("❌ Employee {EmployeeId}: {Error}", log.EmployeeId, errorMessage);
                
                // Employee not found in HR system - retry with low priority
                // This allows HR to fix the issue (add employee) and it will be retried later
                await _logQueue.IncreasePriorityAsync(log.Id, cancellationToken);
                unmatchedCount++;
            }
            else
            {
                // Success or duplicate - both are okay to mark as sent
                await _logQueue.MarkAsSentAsync(log.Id, cancellationToken);
                sentCount++;
            }
        }
        
        if (unmatchedCount > 0)
        {
            _logger.LogWarning("⚠ {Count} unmatched employee(s) moved to low-priority retry queue", unmatchedCount);
        }
        
        _logger.LogInformation("✅ Batch processed: {Sent} sent, {Unmatched} retrying later", sentCount, unmatchedCount);
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
