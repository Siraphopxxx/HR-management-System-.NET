namespace be.Models;

public class EmployeeBasicModel
{
    public int EmployeeId { get; set; }
    public required string FullName { get; set; }
    public required string EmployeeNumber { get; set; }
}