namespace ZkFingerBridge.Models;

/// <summary>
/// SyncState tracks last sync time per device for multi-device support
/// </summary>
public sealed class SyncState
{
    /// <summary>
    /// Last sync time per device (Key = device IP, Value = last sync time)
    /// </summary>
    public Dictionary<string, DateTimeOffset> DeviceLastSync { get; set; } = new();
    
    /// <summary>
    /// Legacy: Global last sync time (for backward compatibility)
    /// </summary>
    public DateTimeOffset? LastSyncedAt { get; set; }
    
    public static readonly SyncState Empty = new() { DeviceLastSync = new(), LastSyncedAt = null };
    
    /// <summary>
    /// Get last sync time for a specific device
    /// </summary>
    public DateTimeOffset? GetDeviceLastSync(string deviceIp)
    {
        return DeviceLastSync.TryGetValue(deviceIp, out var lastSync) ? lastSync : null;
    }
    
    /// <summary>
    /// Set last sync time for a specific device
    /// </summary>
    public void SetDeviceLastSync(string deviceIp, DateTimeOffset syncTime)
    {
        DeviceLastSync[deviceIp] = syncTime;
    }
}
