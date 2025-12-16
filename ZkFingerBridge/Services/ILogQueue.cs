using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

public interface ILogQueue
{
    Task EnqueueAsync(IEnumerable<AttendanceLog> logs, int branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueuedLog>> GetPendingAsync(int maxRetryCount, CancellationToken cancellationToken = default);
    Task MarkAsSentAsync(int logId, CancellationToken cancellationToken = default);
    Task MarkAsFailedAsync(int logId, string error, CancellationToken cancellationToken = default);
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
}
