namespace ZkFingerBridge.Configuration;

public sealed class ZkDeviceOptions
{
    public const string SectionName = "ZkDevice";

    public string IpAddress { get; set; } = "192.168.1.201";

    public int Port { get; set; } = 4370;

    public int MachineNumber { get; set; } = 1;

    /// <summary>
    /// Optional communication password configured on the device.
    /// </summary>
    public int? CommPassword { get; set; }
        = null;
}
