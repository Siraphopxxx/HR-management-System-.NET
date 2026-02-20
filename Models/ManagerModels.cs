namespace be.Models;

public class TeamMemberReadModel
{
    public int EmployeeId { get; set; }
    public required string EmployeeNumber { get; set; }
    public required string FullName { get; set; }
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}