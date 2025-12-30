using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

/// <summary>
/// Registry for managing discovered ZK devices
/// </summary>
public interface IDeviceRegistry
{
    /// <summary>
    /// Get all currently discovered devices
    /// </summary>
    IReadOnlyList<DeviceInfo> GetDevices();
    
    /// <summary>
    /// Update the registry with newly discovered devices
    /// </summary>
    void UpdateDevices(IEnumerable<DeviceInfo> devices);
    
    /// <summary>
    /// Check if any devices are registered
    /// </summary>
    bool HasDevices();
    
    /// <summary>
    /// Check if initial discovery scan has completed
    /// </summary>
    bool IsDiscoveryCompleted();
}
