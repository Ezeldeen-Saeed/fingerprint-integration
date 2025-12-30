namespace ZkFingerBridge.Models;

/// <summary>
/// Information about a discovered ZK device
/// </summary>
public record DeviceInfo(
    string IpAddress,
    int Port,
    string? SerialNumber = null
)
{
    /// <summary>
    /// Last successful connection time
    /// </summary>
    public DateTimeOffset? LastSeen { get; init; }
    
    /// <summary>
    /// Device unique identifier (IP:Port)
    /// </summary>
    public string DeviceId => $"{IpAddress}:{Port}";
}
