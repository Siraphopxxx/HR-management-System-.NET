namespace be.Models;

// Model สำหรับ "อ่าน" ข้อมูลส่วนตัว (แบบปลอดภัย)
public class MyProfileReadModel
{
    public required string EmployeeNumber { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Username { get; set; }
    public required string Role { get; set; }

    public string? DepartmentName { get; set; }
    public string? JobTitleName { get; set; }
    public string? ManagerName { get; set; } // (เช่น "สมชาย ใจดี")

    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }

    public string? Address_Street { get; set; }
    public string? Address_City { get; set; }
    public string? Address_State { get; set; }
    public string? Address_PostalCode { get; set; }
    public string? Address_Country { get; set; }

    public decimal? CurrentSalary { get; set; } // (เราจะแสดง Salary)
}