using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

public sealed class ZkDeviceClient : IZkDeviceClient
{
    private readonly IDeviceConfigurationHolder _configHolder;
    private readonly ILogger<ZkDeviceClient> _logger;
    private readonly object _syncRoot = new();
    private readonly dynamic _zkem;
    private bool _connected;

    private readonly IDeviceRegistry _deviceRegistry;

    public ZkDeviceClient(
        IDeviceConfigurationHolder configHolder,
        IDeviceRegistry deviceRegistry, // <--- Added dependency
        ILogger<ZkDeviceClient> logger)
    {
        _configHolder = configHolder;
        _deviceRegistry = deviceRegistry;
        _logger = logger;

        var zkType = Type.GetTypeFromProgID("zkemkeeper.ZKEM")
                     ?? throw new InvalidOperationException("zkemkeeper.ZKEM COM component is not registered. Run the SDK installer first.");

        _zkem = Activator.CreateInstance(zkType)
                 ?? throw new InvalidOperationException("Unable to instantiate zkemkeeper.ZKEM. Verify SDK installation.");
    }

    public Task<IReadOnlyCollection<AttendanceLog>> ReadLogsAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            lock (_syncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureConnectedUnsafe();

                // Get current connection details (might be resolved from auto)
                string currentIp = _configHolder.IpAddress;
                int currentPort = _configHolder.Port;
                
                if (string.Equals(currentIp, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    var devices = _deviceRegistry.GetDevices();
                    if (devices.Count > 0)
                    {
                        var device = devices[0];
                        currentIp = device.IpAddress;
                        currentPort = device.Port;
                    }
                }

                var logs = new List<AttendanceLog>(); // <--- Restored
                _logger.LogInformation("Reading logs from device {Machine} ({Ip}:{Port})", 
                    _configHolder.MachineNumber, currentIp, currentPort);

                _zkem.EnableDevice(_configHolder.MachineNumber, false);
                try
                {
                    if (!_zkem.ReadAllGLogData(_configHolder.MachineNumber))
                    {
                        var error = GetLastError();
                        _logger.LogWarning("ReadAllGLogData returned false (error {ErrorCode})", error);
                        if (error != 0)
                        {
                            throw new InvalidOperationException($"ReadAllGLogData failed with error {error}");
                        }
                    }

                    while (TryReadSingleLog(out var log))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        logs.Add(log);
                    }

                    _logger.LogInformation("Retrieved {Count} log(s) from device", logs.Count);
                    return (IReadOnlyCollection<AttendanceLog>)logs;
                }
                finally
                {
                    _zkem.EnableDevice(_configHolder.MachineNumber, true);
                }
            }
        }, cancellationToken);
    }

    public Task ClearLogsAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            lock (_syncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureConnectedUnsafe();

                if (!_zkem.ClearGLog(_configHolder.MachineNumber))
                {
                    throw new InvalidOperationException($"ClearGLog failed with error {GetLastError()}");
                }

                _logger.LogInformation("Cleared logs on device {Machine}", _configHolder.MachineNumber);
            }
        }, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        lock (_syncRoot)
        {
            if (_connected)
            {
                try
                {
                    _zkem.Disconnect();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error while disconnecting from device");
                }

                _connected = false;
            }

            if (_zkem is not null && Marshal.IsComObject(_zkem))
            {
                Marshal.FinalReleaseComObject(_zkem);
            }
        }

        return ValueTask.CompletedTask;
    }

    private void EnsureConnectedUnsafe()
    {
        if (_connected)
        {
            return;
        }

        string targetIp = _configHolder.IpAddress;
        int targetPort = _configHolder.Port;

        // Resolve "auto" IP address
        if (string.Equals(targetIp, "auto", StringComparison.OrdinalIgnoreCase))
        {
            if (!_deviceRegistry.HasDevices())
            {
                throw new InvalidOperationException("Configuration set to 'auto' but no devices have been discovered yet.");
            }

            var devices = _deviceRegistry.GetDevices();
            var device = devices.First();
            targetIp = device.IpAddress;
            targetPort = device.Port; // Use discovered port
            
            _logger.LogInformation("Resolved 'auto' to {IP}:{Port}", targetIp, targetPort);
        }

        _logger.LogInformation("Connecting to device at {Ip}:{Port}...", targetIp, targetPort);
        
        // Disconnect first to avoid "already connected" errors
        try
        {
            _zkem.Disconnect();
        }
        catch
        {
            // Ignore disconnect errors
        }
        
        // Set CommPassword BEFORE connecting (required by ZK SDK for some devices)
        if (_configHolder.CommPassword.HasValue)
        {
            _zkem.SetCommPassword(_configHolder.CommPassword.Value);
        }
        
        // Attempt connection
        if (!_zkem.Connect_Net(targetIp, targetPort))
        {
            var errorCode = GetLastError();
            var errorMessage = errorCode switch
            {
                -7 => "Already connected or device in use by another application. Close other ZK software",
                -6 => "Communication password wrong or device busy. Try CommPassword=null",
                -5 => "Device not found. Check IP address",
                -4 => "Incorrect port. Try 8089 instead of 4370",
                -2 => "Connection timeout. Device offline or firewall blocking",
                -8 => "Buffer overflow. Try reducing sync interval",
                _ => $"Error code {errorCode}"
            };
            
            _logger.LogError("❌ Connect_Net failed: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException($"Connect_Net failed with error {errorCode}: {errorMessage}");
        }

        _connected = true;
        _logger.LogInformation("✅ Successfully connected to device");
    }

    private bool TryReadSingleLog(out AttendanceLog log)
    {
        string enrollNumber;
        int verifyMode;
        int inOutMode;
        int year;
        int month;
        int day;
        int hour;
        int minute;
        int second;
        int workCode = 0;

        var success = _zkem.SSR_GetGeneralLogData(
            _configHolder.MachineNumber,
            out enrollNumber,
            out verifyMode,
            out inOutMode,
            out year,
            out month,
            out day,
            out hour,
            out minute,
            out second,
            ref workCode);

        if (!success)
        {
            log = default!;
            return false;
        }

        var localTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local);
        log = new AttendanceLog(
            EmployeeId: enrollNumber,
            PunchTime: new DateTimeOffset(localTime),
            VerifyMode: verifyMode,
            PunchType: inOutMode,
            WorkCode: workCode);

        return true;
    }

    private int GetLastError()
    {
        int error = 0;
        try
        {
            _zkem.GetLastError(ref error);
        }
        catch
        {
            // ignored
        }

        return error;
    }
}
