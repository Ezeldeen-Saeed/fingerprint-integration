using System.ComponentModel.DataAnnotations;

namespace TestHrApi.Models;

public class AttendanceLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string EmployeeId { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset PunchTime { get; set; }

    public int VerifyMode { get; set; }

    public int PunchType { get; set; }

    public int WorkCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
