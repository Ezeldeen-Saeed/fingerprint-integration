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
    }

    public async Task EnqueueAsync(IEnumerable<AttendanceLog> logs, string branchId, CancellationToken cancellationToken = default)
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
            .OrderBy(log => log.QueuedAt)
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

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        using var db = new LogQueueDbContext(_databasePath);
        return await db.QueuedLogs.CountAsync(log => log.Status == QueueStatus.Pending, cancellationToken);
    }
}
