namespace ZkFingerBridge.Configuration;

public class QueueOptions
{
    public int MaxRetryAttempts { get; set; } = 10;
    public int RetryBackoffSeconds { get; set; } = 60;
    public string DatabasePath { get; set; } = "queue.db";
}
