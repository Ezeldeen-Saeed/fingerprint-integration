using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Services;

namespace ZkFingerBridge;

public class StartupWorker : BackgroundService
{
    private readonly ILogger<StartupWorker> _logger;
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly IDeviceConfigurationHolder _configHolder;
    private readonly ZkDeviceOptions _deviceOptions;

    public StartupWorker(
        ILogger<StartupWorker> logger,
        IDeviceDiscoveryService discoveryService,
        IDeviceConfigurationHolder configHolder,
        IOptions<ZkDeviceOptions> deviceOptions)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        _configHolder = configHolder;
        _deviceOptions = deviceOptions.Value;
        
        // Initialize the configuration holder with settings from appsettings.json
        _configHolder.IpAddress = _deviceOptions.IpAddress;
        _configHolder.Port = _deviceOptions.Port;
        _configHolder.MachineNumber = _deviceOptions.MachineNumber;
        _configHolder.CommPassword = _deviceOptions.CommPassword;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ZK Finger bridge starting up...");

        // Auto-discover device IP if needed
        await TryAutoDiscoverDeviceAsync(stoppingToken);

        _logger.LogInformation("Startup complete. Quartz scheduler will handle sync jobs.");
        
        // This worker completes after startup
    }

    private async Task TryAutoDiscoverDeviceAsync(CancellationToken cancellationToken)
    {
        if (!_deviceOptions.EnableAutoDiscovery)
        {
            _configHolder.IsDiscovered = true; // Mark as ready with static IP
            return;
        }

        // If IP is set to "auto", force auto-discovery
        if (_deviceOptions.IpAddress.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("IP address is set to 'auto'. Running device discovery...");
            await DiscoverAndUpdateIpAsync(cancellationToken);
            return;
        }

        // Otherwise, just log the configured IP
        _logger.LogInformation("Using configured device IP: {IpAddress}:{Port}", 
            _deviceOptions.IpAddress, _deviceOptions.Port);
        _configHolder.IsDiscovered = true;
    }

    private async Task DiscoverAndUpdateIpAsync(CancellationToken cancellationToken)
    {
        // Determine the subnet to scan
        var subnet = GetSubnetToScan();
        
        if (string.IsNullOrWhiteSpace(subnet))
        {
            _logger.LogError("Could not determine subnet to scan. No valid network interface found.");
            return;
        }

        _logger.LogInformation("Scanning subnet {Subnet}.0/24 for ZKTeco devices on port {Port}...",
            subnet, _deviceOptions.Port);

        var deviceInfo = await _discoveryService.DiscoverDeviceAsync(
            subnet,
            _deviceOptions.Port);

        if (deviceInfo != null)
        {
            _logger.LogInformation("Auto-discovery successful! Updating device IP from {OldIP} to {NewIP} (S/N: {Serial}, Model: {Model})",
                _deviceOptions.IpAddress,
                deviceInfo.IpAddress,
                deviceInfo.SerialNumber ?? "Unknown",
                deviceInfo.DeviceModel ?? "Unknown");
            
            // Update the shared configuration holder
            _configHolder.IpAddress = deviceInfo.IpAddress;
            _configHolder.IsDiscovered = true;
        }
        else
        {
            _logger.LogError("Auto-discovery failed. No ZKTeco device found on subnet {Subnet}.0/24 port {Port}",
                subnet, _deviceOptions.Port);
        }
    }

    /// <summary>
    /// Gets the subnet to scan for ZK devices.
    /// If AutoDiscoverySubnet is "auto" or empty, auto-detects from local network interfaces.
    /// </summary>
    private string? GetSubnetToScan()
    {
        var configuredSubnet = _deviceOptions.AutoDiscoverySubnet;
        
        // If a valid subnet is configured (not "auto" and not empty), use it
        if (!string.IsNullOrWhiteSpace(configuredSubnet) && 
            !configuredSubnet.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return configuredSubnet;
        }

        // Auto-detect subnet from local network interfaces
        _logger.LogInformation("Auto-detecting local subnet...");
        
        try
        {
            var candidates = new List<(string Subnet, string InterfaceName, string Ip, int Priority)>();
            
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Skip loopback and non-operational interfaces
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                // Skip VPN/tunnel interfaces
                var nicName = nic.Name.ToLowerInvariant();
                var nicDescription = nic.Description.ToLowerInvariant();
                if (nicName.Contains("vpn") || nicName.Contains("warp") || nicName.Contains("tunnel") ||
                    nicName.Contains("virtual") || nicName.Contains("vmware") || nicName.Contains("vethernet") ||
                    nicDescription.Contains("vpn") || nicDescription.Contains("cloudflare") ||
                    nicDescription.Contains("tunnel") || nicDescription.Contains("virtual"))
                {
                    _logger.LogDebug("Skipping VPN/virtual interface: {Name}", nic.Name);
                    continue;
                }

                var ipProps = nic.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    // Only consider IPv4 addresses
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    var ip = addr.Address.ToString();
                    
                    // Skip loopback and link-local
                    if (ip.StartsWith("127.") || ip.StartsWith("169.254."))
                        continue;

                    // Extract subnet (first 3 octets)
                    var parts = ip.Split('.');
                    if (parts.Length >= 3)
                    {
                        var subnet = $"{parts[0]}.{parts[1]}.{parts[2]}";
                        
                        // Prioritize: 192.168.x.x (3), 10.x.x.x (2), 172.16-31.x.x (1), other (0)
                        int priority = 0;
                        if (ip.StartsWith("192.168.")) priority = 3;
                        else if (ip.StartsWith("10.")) priority = 2;
                        else if (ip.StartsWith("172.") && int.TryParse(parts[1], out var second) && second >= 16 && second <= 31) priority = 1;
                        
                        // Ethernet adapters get priority boost
                        if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet) priority += 10;
                        else if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) priority += 5;
                        
                        candidates.Add((subnet, nic.Name, ip, priority));
                        _logger.LogDebug("Found candidate subnet: {Subnet} (from {Interface}: {IP}, priority: {Priority})", 
                            subnet, nic.Name, ip, priority);
                    }
                }
            }
            
            // Pick the highest priority candidate
            var best = candidates.OrderByDescending(c => c.Priority).FirstOrDefault();
            if (best.Subnet != null)
            {
                _logger.LogInformation("Selected subnet: {Subnet} (from {Interface}: {IP})", 
                    best.Subnet, best.InterfaceName, best.Ip);
                return best.Subnet;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting local network interfaces");
        }


        return null;
    }
}

