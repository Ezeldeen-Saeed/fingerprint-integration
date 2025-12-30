namespace ZkFingerBridge.Configuration;

/// <summary>
/// Configuration for multi-device auto-discovery
/// </summary>
public class DiscoveryOptions
{
    public const string SectionName = "Discovery";
    
    /// <summary>
    /// Enable automatic device discovery
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Subnets to scan (first 3 octets). Empty = scan common private ranges
    /// </summary>
    public List<string> Subnets { get; set; } = new();
    
    /// <summary>
    /// Port to scan for ZK devices
    /// </summary>
    public int Port { get; set; } = 4370;
    
    /// <summary>
    /// Scan for devices on startup
    /// </summary>
    public bool ScanOnStartup { get; set; } = true;
    
    /// <summary>
    /// Interval in seconds between periodic scans (0 = disabled)
    /// </summary>
    public int PeriodicScanIntervalSeconds { get; set; } = 3600; // 1 hour
    
    /// <summary>
    /// Connection timeout per device in seconds
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 5;
    
    /// <summary>
    /// Number of parallel connections during scan
    /// </summary>
    public int ParallelConnections { get; set; } = 100;
    
    /// <summary>
    /// Common private IP ranges (192.168.x, 172.16-31.x, 10.x.x)
    /// </summary>
    public static readonly List<string> CommonPrivateRanges = new()
    {
        "192.168.0",
        "192.168.1",
        "192.168.100",
        "172.16",
        "10.0"
    };
}
