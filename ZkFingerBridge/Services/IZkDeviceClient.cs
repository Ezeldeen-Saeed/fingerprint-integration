using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

public interface IZkDeviceClient : IAsyncDisposable
{
    Task<IReadOnlyCollection<AttendanceLog>> ReadLogsAsync(CancellationToken cancellationToken);

    Task ClearLogsAsync(CancellationToken cancellationToken);
}
