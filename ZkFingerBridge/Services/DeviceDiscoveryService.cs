using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ZkFingerBridge.Services;

public interface IDeviceDiscoveryService
{
    Task<DeviceInfo?> DiscoverDeviceAsync(string subnet, int port, int timeoutMs = 5000);
}

public record DeviceInfo(string IpAddress, string? SerialNumber, string? DeviceModel);

public class DeviceDiscoveryService : IDeviceDiscoveryService
{
    private readonly ILogger<DeviceDiscoveryService> _logger;

    public DeviceDiscoveryService(ILogger<DeviceDiscoveryService> logger)
    {
        _logger = logger;
    }

    public async Task<DeviceInfo?> DiscoverDeviceAsync(string subnet, int port, int timeoutMs = 5000)
    {
        _logger.LogInformation("Scanning subnet {Subnet}.0/24 for ZKTeco device on port {Port}...", subnet, port);

        var tasks = new List<Task<DeviceInfo?>>();

        for (int i = 1; i <= 254; i++)
        {
            var ip = $"{subnet}.{i}";
            tasks.Add(VerifyZkDeviceAsync(ip, port, timeoutMs));
        }

        var results = await Task.WhenAll(tasks);
        var foundDevice = results.FirstOrDefault(device => device != null);

        if (foundDevice != null)
        {
            _logger.LogInformation("✓ ZKTeco device found at: {IpAddress} (S/N: {Serial}, Model: {Model})", 
                foundDevice.IpAddress, 
                foundDevice.SerialNumber ?? "Unknown", 
                foundDevice.DeviceModel ?? "Unknown");
        }
        else
        {
            _logger.LogWarning("✗ No ZKTeco device found on subnet {Subnet}.0/24 port {Port}", subnet, port);
        }

        return foundDevice;
    }

    /// <summary>
    /// Verifies if the device at the given IP is actually a ZKTeco device by attempting to connect with the SDK
    /// </summary>
    private async Task<DeviceInfo?> VerifyZkDeviceAsync(string ip, int port, int timeoutMs)
    {
        return await Task.Run(() =>
        {
            dynamic? zkem = null;
            try
            {
                // Create ZK SDK instance
                var zkType = Type.GetTypeFromProgID("zkemkeeper.ZKEM");
                if (zkType == null)
                {
                    _logger.LogError("zkemkeeper.ZKEM COM component not registered. Cannot verify devices.");
                    return null;
                }

                zkem = Activator.CreateInstance(zkType);
                if (zkem == null) return null;

                // Set a short timeout for connection attempt
                zkem.SetCommTimeout(timeoutMs);

                // Try to actually connect to the device using ZK protocol
                bool connected = zkem.Connect_Net(ip, port);
                
                if (connected)
                {
                    // Successfully connected! This is definitely a ZKTeco device
                    _logger.LogDebug("✓ Verified ZKTeco device at {IP}", ip);

                    // Try to get device info
                    string? serialNumber = null;
                    string? model = null;

                    try
                    {
                        // Get serial number (machine SN)
                        string sn = string.Empty;
                        if (zkem.GetSerialNumber(1, out sn))
                        {
                            serialNumber = sn;
                        }

                        // Get device name/model
                        string deviceName = string.Empty;
                        if (zkem.GetDeviceInfo(1, 72, ref deviceName)) // 72 = device name
                        {
                            model = deviceName;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not retrieve device details from {IP}", ip);
                    }

                    // Disconnect
                    try
                    {
                        zkem.Disconnect();
                    }
                    catch { }

                    return new DeviceInfo(ip, serialNumber, model);
                }
            }
            catch (Exception ex)
            {
                // Not a ZK device or connection failed
                _logger.LogTrace(ex, "Device at {IP} is not a ZKTeco device or connection failed", ip);
            }
            finally
            {
                // Clean up COM object
                if (zkem != null && Marshal.IsComObject(zkem))
                {
                    try
                    {
                        Marshal.FinalReleaseComObject(zkem);
                    }
                    catch { }
                }
            }

            return null;
        });
    }
}
