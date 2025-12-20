namespace ZkFingerBridge.Models;

public sealed record AttendanceLog(
    string EmployeeId,
    DateTimeOffset PunchTime,
    int VerifyMode,
    int PunchType,
    int WorkCode,
    int? BranchId = null  // Optional: used when re-sending queued logs with their original BranchId
);