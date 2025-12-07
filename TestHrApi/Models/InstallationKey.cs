using System.ComponentModel.DataAnnotations;

namespace TestHrApi.Models;

public class InstallationKey
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public required string Key { get; set; } // The code (e.g., "BRANCH-2024-X9Y2")
    
    [Required]
    [MaxLength(50)]
    public required string BranchId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public required string BranchName { get; set; }
    
    public bool Used { get; set; }
    public DateTime? UsedAt { get; set; }
    
    [MaxLength(45)]
    public string? UsedByIp { get; set; }
    
    public DateTime? ExpiresAt { get; set; }
    
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; }
}

public class ActivateKeyRequest
{
    public required string InstallationKey { get; set; }
    public string? MachineName { get; set; }
    public string? Version { get; set; }
}

public class ActivateKeyResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public Branch? Branch { get; set; }
    public string? Message { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedBy { get; set; }
}

public class Branch
{
    public required string Id { get; set; }
    public required string Name { get; set; }
}
