namespace ZkFingerBridge.Models;

/// <summary>
/// Represents an employee registered on the ZKTeco device.
/// </summary>
public sealed record Employee(
    string EmployeeId,
    string Name,
    bool Enabled,
    int Privilege
);
