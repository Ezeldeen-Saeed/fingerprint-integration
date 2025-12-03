using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

public interface IHrApiClient
{
    Task SendAsync(IReadOnlyCollection<AttendanceLog> logs, CancellationToken cancellationToken);
}
