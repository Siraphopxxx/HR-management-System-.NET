using System.ComponentModel.DataAnnotations;

namespace be.Models; // << ตรวจสอบว่า namespace คือ 'be.Models'

// (AppUser และ AddUserModel ควรจะอยู่ในนี้ด้วย)
public class AppUser
{
    public int Id { get; set; } // PK
    public required string Name { get; set; } // Username
    public required string Password { get; set; } // Plain Text
    public required string Role { get; set; } // Admin, HR, Manager, Employee
}

// *** คลาสนี้ต้องมีอยู่ ***
public class SimpleLoginModel
{
    [Required]
    public required string Name { get; set; }
    [Required]
    public required string Password { get; set; }
}

// *** คลาสนี้ต้องมีอยู่ ***
public class AddUserModel
{
    // (โค้ด AddUserModel ที่เราอัปเดตล่าสุด)
    [Required]
    public required string Name { get; set; } // Username

    public required string Password { get; set; }
    [Required]
    public required string Role { get; set; }

    [Required]
    public required string EmployeeNumber { get; set; }
    [Required]
    public required string FirstName { get; set; }
    [Required]
    public required string LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }

    public string? Address_Street { get; set; }
    public string? Address_City { get; set; }
    public string? Address_State { get; set; }
    public string? Address_PostalCode { get; set; }
    public string? Address_Country { get; set; }

    public int? DepartmentId { get; set; }
    public int? JobTitleId { get; set; }
    public int? ManagerId { get; set; }
    public decimal? CurrentSalary { get; set; }
}