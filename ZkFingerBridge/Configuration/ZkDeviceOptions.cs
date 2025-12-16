namespace ZkFingerBridge.Configuration;

public sealed class ZkDeviceOptions
{
    public const string SectionName = "ZkDevice";

    /// <summary>
    /// IP address of the device. Leave as "auto" to enable auto-discovery.
    /// </summary>
    public string IpAddress { get; set; } = "auto";

    public int Port { get; set; } = 8089;

    public int MachineNumber { get; set; } = 1;

    /// <summary>
    /// Optional communication password configured on the device.
    /// </summary>
    public int? CommPassword { get; set; } = null;

    /// <summary>
    /// Subnet to scan for auto-discovery (e.g., "192.168.0"). Only used when IpAddress is "auto".
    /// </summary>
    public string? AutoDiscoverySubnet { get; set; } = "192.168.0";

    /// <summary>
    /// Enable auto-discovery on startup if static IP fails
    /// </summary>
    public bool EnableAutoDiscovery { get; set; } = true;

    /// <summary>
    /// Branch identifier for multi-branch deployments
    /// </summary>
    public int BranchId { get; set; } = 1;

    /// <summary>
    /// Branch name for display purposes (set by wizard)
    /// </summary>
    public string? BranchName { get; set; }
}
