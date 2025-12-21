using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ZkFingerBridge.Services;

public interface IDeviceDiscoveryService
{
    Task<DeviceInfo?> DiscoverDeviceAsync(string subnet, int port, int timeoutMs = 60000);
}

public record DeviceInfo(string IpAddress, string? SerialNumber, string? DeviceModel);

public class DeviceDiscoveryService : IDeviceDiscoveryService
{
    private readonly ILogger<DeviceDiscoveryService> _logger;
    private int _scannedCount = 0;
    private int _foundCount = 0;

    public DeviceDiscoveryService(ILogger<DeviceDiscoveryService> logger)
    {
        _logger = logger;
    }

    public async Task<DeviceInfo?> DiscoverDeviceAsync(string subnet, int port, int timeoutMs = 60000)
    {
        _logger.LogInformation("Starting ZKTeco device discovery on subnet {Subnet}.0/24, port {Port}, timeout {Timeout}ms...", 
            subnet, port, timeoutMs);

        // Check COM registration once at the start
        var zkType = Type.GetTypeFromProgID("zkemkeeper.ZKEM");
        if (zkType == null)
        {
            _logger.LogError("zkemkeeper.ZKEM COM component is NOT registered. " +
                "Please run as Administrator: regsvr32 \"<path>\\zkemkeeper.dll\"");
            return null;
        }
        
        _logger.LogInformation("✓ ZK SDK COM component is registered. Scanning 254 IPs in batches...");

        _scannedCount = 0;
        _foundCount = 0;
        DeviceInfo? foundDevice = null;

        // Scan in smaller batches to avoid overwhelming the SDK
        const int batchSize = 20;
        for (int start = 1; start <= 254 && foundDevice == null; start += batchSize)
        {
            var tasks = new List<Task<DeviceInfo?>>();
            int end = Math.Min(start + batchSize - 1, 254);
            
            for (int i = start; i <= end; i++)
            {
                var ip = $"{subnet}.{i}";
                tasks.Add(VerifyZkDeviceAsync(zkType, ip, port, timeoutMs));
            }

            var results = await Task.WhenAll(tasks);
            foundDevice = results.FirstOrDefault(device => device != null);

            // Log progress every batch
            _logger.LogDebug("Scanned IPs {Start}-{End}, total scanned: {Scanned}", start, end, _scannedCount);
        }

        if (foundDevice != null)
        {
            _logger.LogInformation("✓ ZKTeco device FOUND at: {IpAddress} (S/N: {Serial}, Model: {Model})", 
                foundDevice.IpAddress, 
                foundDevice.SerialNumber ?? "Unknown", 
                foundDevice.DeviceModel ?? "Unknown");
        }
        else
        {
            _logger.LogWarning("✗ No ZKTeco device found on subnet {Subnet}.0/24 port {Port} after scanning {Count} IPs. " +
                "Make sure the device is powered on and connected to the same network.", 
                subnet, port, _scannedCount);
        }

        return foundDevice;
    }

    /// <summary>
    /// Verifies if the device at the given IP is actually a ZKTeco device by attempting to connect with the SDK
    /// </summary>
    private async Task<DeviceInfo?> VerifyZkDeviceAsync(Type zkType, string ip, int port, int timeoutMs)
    {
        return await Task.Run(() =>
        {
            Interlocked.Increment(ref _scannedCount);
            dynamic? zkem = null;
            try
            {
                zkem = Activator.CreateInstance(zkType);
                if (zkem == null) return null;

                // Set a reasonable timeout for connection attempt
                zkem.SetCommTimeout(timeoutMs);

                // Try to actually connect to the device using ZK protocol
                bool connected = zkem.Connect_Net(ip, port);
                
                if (connected)
                {
                    Interlocked.Increment(ref _foundCount);
                    
                    // Successfully connected! This is definitely a ZKTeco device
                    _logger.LogInformation("✓ Connected to ZKTeco device at {IP}:{Port}", ip, port);

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
                // Not a ZK device or connection failed - this is expected for most IPs
                _logger.LogTrace(ex, "No ZK device at {IP}", ip);
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
