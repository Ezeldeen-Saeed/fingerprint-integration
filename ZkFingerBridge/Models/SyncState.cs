namespace ZkFingerBridge.Models;

public sealed record SyncState(DateTimeOffset? LastSyncedAt)
{
    public static readonly SyncState Empty = new(null);
}
