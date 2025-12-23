namespace ZkFingerBridge.Models;

public class QueuedLog
{
    public int Id { get; set; }
    
    // Attendance log data
    public required string EmployeeId { get; set; }
    public DateTimeOffset PunchTime { get; set; }
    public int VerifyMode { get; set; }
    public int PunchType { get; set; }
    public int WorkCode { get; set; }
    
    // Queue metadata
    public int BranchId { get; set; }
    public DateTime QueuedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public QueueStatus Status { get; set; }
    public DateTime? SentAt { get; set; }
    public int Priority { get; set; } = 0; // 0 = normal priority, higher values = lower priority (processed later)
}

public enum QueueStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}
