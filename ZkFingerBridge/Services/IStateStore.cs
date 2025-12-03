using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

public interface IStateStore
{
    Task<SyncState> ReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(SyncState state, CancellationToken cancellationToken);
}
