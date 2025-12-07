using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

public sealed class ZkDeviceClient : IZkDeviceClient
{
    private readonly ZkDeviceOptions _options;
    private readonly ILogger<ZkDeviceClient> _logger;
    private readonly object _syncRoot = new();
    private readonly dynamic _zkem;
    private bool _connected;

    public ZkDeviceClient(IOptions<ZkDeviceOptions> options, ILogger<ZkDeviceClient> logger)
    {
        _options = options.Value;
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

                var logs = new List<AttendanceLog>();
                _logger.LogInformation("Reading logs from device {Machine} ({Ip}:{Port})", _options.MachineNumber, _options.IpAddress, _options.Port);

                _zkem.EnableDevice(_options.MachineNumber, false);
                try
                {
                    if (!_zkem.ReadAllGLogData(_options.MachineNumber))
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
                    _zkem.EnableDevice(_options.MachineNumber, true);
                }
            }
        }, cancellationToken);
    }

    public Task<IReadOnlyCollection<Employee>> ReadEmployeesAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            lock (_syncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureConnectedUnsafe();

                var employees = new List<Employee>();
                _logger.LogInformation("Reading employees from device {Machine} ({Ip}:{Port})", 
                    _options.MachineNumber, _options.IpAddress, _options.Port);

                _zkem.EnableDevice(_options.MachineNumber, false);
                try
                {
                    if (!_zkem.ReadAllUserID(_options.MachineNumber))
                    {
                        var error = GetLastError();
                        _logger.LogWarning("ReadAllUserID returned false (error {ErrorCode})", error);
                        if (error != 0)
                        {
                            throw new InvalidOperationException($"ReadAllUserID failed with error {error}");
                        }
                    }

                    string employeeId = string.Empty;
                    string name = string.Empty;
                    string password = string.Empty;
                    int privilege = 0;
                    bool enabled = false;

                    while (_zkem.SSR_GetAllUserInfo(
                        _options.MachineNumber,
                        out employeeId,
                        out name,
                        out password,
                        out privilege,
                        out enabled))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        employees.Add(new Employee(employeeId, name, enabled, privilege));
                    }

                    _logger.LogInformation("Retrieved {Count} employee(s) from device", employees.Count);
                    return (IReadOnlyCollection<Employee>)employees;
                }
                finally
                {
                    _zkem.EnableDevice(_options.MachineNumber, true);
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

                if (!_zkem.ClearGLog(_options.MachineNumber))
                {
                    throw new InvalidOperationException($"ClearGLog failed with error {GetLastError()}");
                }

                _logger.LogInformation("Cleared logs on device {Machine}", _options.MachineNumber);
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

        _logger.LogInformation("Connecting to device at {Ip}:{Port}", _options.IpAddress, _options.Port);
        if (!_zkem.Connect_Net(_options.IpAddress, _options.Port))
        {
            throw new InvalidOperationException($"Connect_Net failed with error {GetLastError()}");
        }

        if (_options.CommPassword.HasValue)
        {
            _zkem.SetCommPassword(_options.CommPassword.Value);
        }

        _connected = true;
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
            _options.MachineNumber,
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
