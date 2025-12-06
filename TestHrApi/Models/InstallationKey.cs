namespace TestHrApi.Models;

public class InstallationKey
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public required string BranchId { get; set; }
    public required string BranchName { get; set; }
    
    // Usage tracking
    public bool Used { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedByIp { get; set; }
    
    // Optional expiration
    public DateTime? ExpiresAt { get; set; }
    
    // Metadata
    public string? CreatedBy { get; set; }
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; }
}

public class Branch
{
    public required string Id { get; set; }
    public required string Name { get; set; }
}

public class ActivateKeyRequest
{
    public required string InstallationKey { get; set; }
}

public class ActivateKeyResponse
{
    public bool Success { get; set; }
    public Branch? Branch { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedBy { get; set; }
}
