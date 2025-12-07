namespace TestHrApi.Models;

public class Employee
{
    public int Id { get; set; }
    public required string EmployeeId { get; set; }
    public required string Name { get; set; }
    public bool Enabled { get; set; }
    public int Privilege { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
