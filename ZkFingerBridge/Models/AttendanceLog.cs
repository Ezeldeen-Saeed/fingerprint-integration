namespace ZkFingerBridge.Models;

public sealed record AttendanceLog(
    string EmployeeId,
    DateTimeOffset PunchTime,
    int VerifyMode,
    int PunchType,
    int WorkCode
);
