using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

/// <summary>
/// In-memory registry of discovered ZK devices
/// </summary>
public class DeviceRegistry : IDeviceRegistry
{
    private readonly object _lock = new();
    private List<DeviceInfo> _devices = new();
    private readonly ILogger<DeviceRegistry> _logger;
    private bool _discoveryCompleted = false;

    public DeviceRegistry(ILogger<DeviceRegistry> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<DeviceInfo> GetDevices()
    {
        lock (_lock)
        {
            return _devices.ToList();
        }
    }

    public void UpdateDevices(IEnumerable<DeviceInfo> devices)
    {
        lock (_lock)
        {
            _devices = devices.ToList();
            _discoveryCompleted = true;
            
            _logger.LogInformation("════════════════════════════════════════════");
            _logger.LogInformation("DEVICE DISCOVERY COMPLETED");
            _logger.LogInformation("════════════════════════════════════════════");
            _logger.LogInformation("Found {Count} ZK fingerprint device(s):", _devices.Count);
            
            if (_devices.Count == 0)
            {
                _logger.LogWarning("NO DEVICES FOUND on the network");
                _logger.LogWarning("Please ensure:");
                _logger.LogWarning("  1. ZK devices are powered on");
                _logger.LogWarning("  2. Devices are connected to the same network");
                _logger.LogWarning("  3. Network allows device communication");
            }
            else
            {
                foreach (var device in _devices)
                {
                    _logger.LogInformation("  ✓ Device at {IP}:{Port} (S/N: {Serial})", 
                        device.IpAddress, 
                        device.Port,
                        device.SerialNumber ?? "Unknown");
                }
            }
            
            _logger.LogInformation("════════════════════════════════════════════");
            _logger.LogInformation("Ready to start syncing attendance logs");
            _logger.LogInformation("════════════════════════════════════════════");
        }
    }

    public bool HasDevices()
    {
        lock (_lock)
        {
            return _devices.Count > 0;
        }
    }
    
    public bool IsDiscoveryCompleted()
    {
        lock (_lock)
        {
            return _discoveryCompleted;
        }
    }
}
