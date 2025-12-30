using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Data;
using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

public class SqliteLogQueue : ILogQueue
{
    private readonly string _databasePath;
    private readonly ILogger<SqliteLogQueue> _logger;

    public SqliteLogQueue(IOptions<QueueOptions> options, ILogger<SqliteLogQueue> logger)
    {
        _databasePath = options.Value.DatabasePath;
        _logger = logger;
        
        // Ensure database and tables are created
        using var db = new LogQueueDbContext(_databasePath);
        db.Database.EnsureCreated();
        
        // Run migration to add Priority column if it doesn't exist (for existing databases)
        MigrateDatabaseSchema(db);
    }

    /// <summary>
    /// Migrates existing database schema to add new columns/indexes
    /// </summary>
    private void MigrateDatabaseSchema(LogQueueDbContext db)
    {
        try
        {
            // Check if Priority column exists
            var tableInfo = db.Database.SqlQueryRaw<TableInfo>(
                "PRAGMA table_info(QueuedLogs)").ToList();
            
            var hasPriorityColumn = tableInfo.Any(col => col.name == "Priority");
            
            if (!hasPriorityColumn)
            {
                _logger.LogInformation("Running database migration: Adding Priority column...");
                
                // Add Priority column with default value 0
                db.Database.ExecuteSqlRaw(
                    "ALTER TABLE QueuedLogs ADD COLUMN Priority INTEGER NOT NULL DEFAULT 0");
                
                // Create index for priority-based ordering
                db.Database.ExecuteSqlRaw(
                    @"CREATE INDEX IF NOT EXISTS IX_QueuedLogs_Status_Priority_QueuedAt 
                      ON QueuedLogs(Status, Priority, QueuedAt)");
                
                _logger.LogInformation("✅ Database migration completed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate database schema. This may cause errors.");
        }
    }
    
    // Helper class for reading PRAGMA table_info
    private class TableInfo
    {
        public string name { get; set; } = string.Empty;
    }

    public async Task EnqueueAsync(IEnumerable<AttendanceLog> logs, int branchId, CancellationToken cancellationToken = default)
    {
        using var db = new LogQueueDbContext(_databasePath);
        
        var queuedLogs = logs.Select(log => new QueuedLog
        {
            EmployeeId = log.EmployeeId,
            PunchTime = log.PunchTime,
            VerifyMode = log.VerifyMode,
            PunchType = log.PunchType,
            WorkCode = log.WorkCode,
            BranchId = branchId,
            QueuedAt = DateTime.UtcNow,
            RetryCount = 0,
            Status = QueueStatus.Pending
        }).ToList();
        
        db.QueuedLogs.AddRange(queuedLogs);
        await db.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("📦 Queued {Count} log(s) for offline storage", queuedLogs.Count);
    }

    public async Task<IReadOnlyList<QueuedLog>> GetPendingAsync(int maxRetryCount, CancellationToken cancellationToken = default)
    {
        using var db = new LogQueueDbContext(_databasePath);
        
        return await db.QueuedLogs
            .Where(log => log.Status == QueueStatus.Pending && log.RetryCount < maxRetryCount)
            .OrderBy(log => log.Priority)  // Lower priority values first (0 = normal, higher = low priority)
            .ThenBy(log => log.QueuedAt)   // Then by time queued
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsSentAsync(int logId, CancellationToken cancellationToken = default)
    {
        using var db = new LogQueueDbContext(_databasePath);
        
        var log = await db.QueuedLogs.FindAsync(new object[] { logId }, cancellationToken);
        if (log != null)
        {
            log.Status = QueueStatus.Sent;
            log.SentAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsFailedAsync(int logId, string error, CancellationToken cancellationToken = default)
    {
        using var db = new LogQueueDbContext(_databasePath);
        
        var log = await db.QueuedLogs.FindAsync(new object[] { logId }, cancellationToken);
        if (log != null)
        {
            log.RetryCount++;
            log.LastError = error.Length > 500 ? error.Substring(0, 500) : error;
            await db.SaveChangesAsync(cancellationToken);
            
            _logger.LogWarning("⚠️ Failed to send queued log {LogId} (retry {RetryCount}): {Error}", 
                logId, log.RetryCount, error);
        }
    }

    public async Task IncreasePriorityAsync(int logId, string reason, CancellationToken cancellationToken = default)
    {
        using var db = new LogQueueDbContext(_databasePath);
        
        var log = await db.QueuedLogs.FindAsync(new object[] { logId }, cancellationToken);
        if (log != null)
        {
            log.Priority++;  // Increase priority value (moves to back of queue)
            log.RetryCount++;
            log.LastError = reason;
            await db.SaveChangesAsync(cancellationToken);
            
            _logger.LogDebug("Moved log {LogId} to low priority (Priority={Priority}). Reason: {Reason}", 
                logId, log.Priority, reason);
        }
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        using var db = new LogQueueDbContext(_databasePath);
        return await db.QueuedLogs.CountAsync(log => log.Status == QueueStatus.Pending, cancellationToken);
    }
}
