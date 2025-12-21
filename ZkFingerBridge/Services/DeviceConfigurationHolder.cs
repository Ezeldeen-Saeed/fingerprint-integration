namespace ZkFingerBridge.Services;

/// <summary>
/// Holds the runtime device configuration, including the discovered IP address.
/// This is a shared singleton that gets updated by the discovery service
/// and used by the ZkDeviceClient.
/// </summary>
public interface IDeviceConfigurationHolder
{
    string IpAddress { get; set; }
    int Port { get; set; }
    int MachineNumber { get; set; }
    int? CommPassword { get; set; }
    bool IsDiscovered { get; set; }
}

public class DeviceConfigurationHolder : IDeviceConfigurationHolder
{
    public string IpAddress { get; set; } = "auto";
    public int Port { get; set; } = 4370;
    public int MachineNumber { get; set; } = 1;
    public int? CommPassword { get; set; }
    public bool IsDiscovered { get; set; } = false;
}
