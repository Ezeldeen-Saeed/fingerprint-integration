using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

public interface IDeviceDiscoveryService
{
    /// <summary>
    /// Discover all ZK devices across multiple subnets
    /// </summary>
    Task<List<DeviceInfo>> DiscoverDevicesAsync(IEnumerable<string> subnets, int port, int timeoutMs = 3000);
    
    /// <summary>
    /// Discover devices on a single subnet (legacy)
    /// </summary>
    Task<DeviceInfo?> DiscoverDeviceAsync(string subnet, int port, int timeoutMs = 60000);
}

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
            _logger.LogInformation("✓ ZKTeco device FOUND at: {IpAddress} (S/N: {Serial})", 
                foundDevice.IpAddress, 
                foundDevice.SerialNumber ?? "Unknown");
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
    private async Task<DeviceInfo?> VerifyZkDeviceAsync(Type zkType, string ip, int port, int timeoutMs, bool skipPortCheck = false)
    {
        _logger.LogInformation("→ VerifyZkDeviceAsync called for {IP}:{Port}, skipPortCheck={Skip}", ip, port, skipPortCheck);
        
        if (!skipPortCheck)
        {
            // STAGE 1: Quick TCP port check to see if port is open
            // This is much faster than SDK connection and respects timeout properly
            bool portOpen = await IsPortOpenAsync(ip, port, timeoutMs);
            
            if (!portOpen)
            {
                // Port is closed or unreachable, skip SDK connection
                Interlocked.Increment(ref _scannedCount);
                return null;
            }
        }
        
        // STAGE 2: Port is open, now try SDK connection on background thread
        _logger.LogInformation("→ Starting Task.Run for SDK connection to {IP}:{Port}", ip, port);
        
        return await Task.Run(() =>
        {
            _logger.LogInformation("→ Inside Task.Run for {IP}:{Port}", ip, port);
            Interlocked.Increment(ref _scannedCount);
            dynamic? zkem = null;
            try
            {
                _logger.LogInformation("→ Creating COM instance for {IP}:{Port}, zkType={Type}", ip, port, zkType?.FullName ?? "NULL");
                zkem = Activator.CreateInstance(zkType);
                if (zkem == null)
                {
                    _logger.LogError("❌ Activator.CreateInstance returned NULL for {IP}:{Port}", ip, port);
                    return null;
                }
                
                _logger.LogInformation("✓ COM instance created successfully for {IP}:{Port}", ip, port);

                // Set a reasonable timeout for connection attempt
                _logger.LogInformation("→ Setting CommTimeout={Timeout}ms for {IP}:{Port}", timeoutMs, ip, port);
                try
                {
                    zkem.SetCommTimeout(timeoutMs);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("SetCommTimeout failed (ignoring): {Message}", ex.Message);
                }
                
                // IMPORTANT: Disconnect first to clear any previous connection state
                // This matches the working implementation in ZkDeviceClient
                _logger.LogInformation("→ Calling Disconnect() for {IP}:{Port}", ip, port);
                try
                {
                    zkem.Disconnect();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Disconnect failed (expected if not connected) for {IP}:{Port}", ip, port);
                }
                
                // Set CommPassword to 0 (required by some devices before connection)
                _logger.LogInformation("→ Setting CommPassword=0 for {IP}:{Port}", ip, port);
                try
                {
                    zkem.SetCommPassword(0);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SetCommPassword failed for {IP}:{Port}", ip, port);
                }

                // Try to actually connect to the device using ZK protocol
                _logger.LogInformation("🔌 Attempting SDK Connect_Net to {IP}:{Port}...", ip, port);
                bool connected = zkem.Connect_Net(ip, port);
                
                _logger.LogInformation("→ Connect_Net returned: {Result} for {IP}:{Port}", connected, ip, port);
                
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

                    return new DeviceInfo(ip, port, serialNumber);
                }
                else
                {
                    // Connection failed - log the error code
                    int errorCode = 0;
                    try
                    {
                        zkem.GetLastError(ref errorCode);
                        _logger.LogWarning("❌ SDK Connect_Net FAILED for {IP}:{Port} - SDK Error Code: {ErrorCode}", ip, port, errorCode);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "❌ SDK Connect_Net FAILED for {IP}:{Port} - Could not get error code", ip, port);
                    }
                }
            }
            catch (Exception ex)
            {
                // Not a ZK device or connection failed - this is expected for most IPs
                _logger.LogError(ex, "❌ EXCEPTION in VerifyZkDeviceAsync for {IP}:{Port}", ip, port);
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

            _logger.LogInformation("→ Returning NULL from VerifyZkDeviceAsync for {IP}:{Port}", ip, port);
            return null;
        });
    }
    
    /// <summary>
    /// Check if a TCP port is open on the given IP address
    /// </summary>
    private async Task<bool> IsPortOpenAsync(string ip, int port, int timeoutMs)
    {
        using var client = new System.Net.Sockets.TcpClient();
        try
        {
            var connectTask = client.ConnectAsync(ip, port);
            var timeoutTask = Task.Delay(timeoutMs);
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            if (completedTask == connectTask)
            {
                if (connectTask.IsFaulted || connectTask.IsCanceled)
                {
                    _logger.LogTrace("Port {Port} CLOSED on {IP}: {Error}", port, ip, connectTask.Exception?.Message ?? "Canceled");
                    return false;
                }
                
                // Connection succeeded
                _logger.LogInformation("✓ Port {Port} is OPEN on {IP} - will attempt SDK connection", port, ip);
                return true;
            }
            else
            {
                // Timeout
                _logger.LogTrace("Port {Port} TIMEOUT on {IP} after {Timeout}ms", port, ip, timeoutMs);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Port {Port} check FAILED on {IP}", port, ip);
            return false;
        }
    }
    
    /// <summary>
    /// Discover all ZK devices across multiple subnets
    /// </summary>
    public async Task<List<DeviceInfo>> DiscoverDevicesAsync(IEnumerable<string> subnets, int port, int timeoutMs = 3000)
    {
        var subnetList = subnets.ToList();
        
        // Use common private ranges if no subnets specified
        if (subnetList.Count == 0)
        {
            subnetList = Configuration.DiscoveryOptions.CommonPrivateRanges;
            _logger.LogInformation("No subnets configured, scanning common private ranges: {Count} subnets", subnetList.Count);
        }
        
        _logger.LogInformation("🔍 Starting multi-subnet device discovery across {SubnetCount} subnet(s), port {Port}...", 
            subnetList.Count, port);
        
        var zkType = Type.GetTypeFromProgID("zkemkeeper.ZKEM");
        if (zkType == null)
        {
            _logger.LogError("zkemkeeper.ZKEM COM component is NOT registered");
            return new List<DeviceInfo>();
        }
        
        var allDevices = new List<DeviceInfo>();
        _scannedCount = 0;
        _foundCount = 0;
        
        foreach (var subnet in subnetList)
        {
            _logger.LogInformation("Scanning subnet {Subnet}.0/24...", subnet);
            var devices = await DiscoverDevicesOnSubnetAsync(zkType, subnet, port, timeoutMs);
            allDevices.AddRange(devices);
        }
        
        _logger.LogInformation("✅ Discovery complete: Found {FoundCount} device(s) after scanning {ScannedCount} IPs",
            _foundCount, _scannedCount);
        
        return allDevices;
    }
    
    /// <summary>
    /// Discovers ZK devices on a specific subnet by scanning IPs and verifying with the ZK SDK
    /// Uses two-phase approach: 1) TCP port scan all IPs, 2) SDK connect only to responsive IPs
    /// </summary>
    private async Task<List<DeviceInfo>> DiscoverDevicesOnSubnetAsync(Type zkType, string subnet, int port, int timeoutMs = 5000)
    {
        _logger.LogInformation("Scanning subnet {Subnet}...", subnet + ".0/24");
        
        var devices = new List<DeviceInfo>();
        var ips = new List<string>();
        
        // Generate all IPs for this subnet (1-254)
        for (int i = 1; i <= 254; i++)
        {
            ips.Add($"{subnet}.{i}");
        }
        
        // ═══════════════════════════════════════════════════════════════
        // PHASE 1: TCP Port Scan - Find all IPs with port 4370 open
        // ═══════════════════════════════════════════════════════════════
        _logger.LogInformation("📡 Phase 1: Scanning {Count} IPs for open port {Port}...", ips.Count, port);
        
        var openPortIps = new List<string>();
        var portCheckTasks = ips.Select(async ip =>
        {
            bool isOpen = await IsPortOpenAsync(ip, port, timeoutMs);
            if (isOpen)
            {
                lock (openPortIps)
                {
                    openPortIps.Add(ip);
                }
            }
        });
        
        await Task.WhenAll(portCheckTasks);
        
        _logger.LogInformation("✓ Phase 1 Complete: Found {Count} IP(s) with port {Port} open", openPortIps.Count, port);
        
        if (openPortIps.Count == 0)
        {
            _logger.LogWarning("No IPs found with port {Port} open on subnet {Subnet}", port, subnet + ".0/24");
            return devices;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // PHASE 2: SDK Connection - Connect ONLY to IPs with open ports
        // ═══════════════════════════════════════════════════════════════
        _logger.LogInformation("🔌 Phase 2: Attempting SDK connection to {Count} IP(s)...", openPortIps.Count);
        
        foreach (var ip in openPortIps)
        {
            var device = await VerifyZkDeviceAsync(zkType, ip, port, timeoutMs, skipPortCheck: true);
            if (device != null)
            {
                devices.Add(device);
                _logger.LogInformation("✓ Device found: {DeviceId}", device.DeviceId);
            }
        }
        
        _logger.LogInformation("✓ Phase 2 Complete: Found {Count} ZK device(s)", devices.Count);
        
        return devices;
    }
}
