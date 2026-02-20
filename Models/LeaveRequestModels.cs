using System.ComponentModel.DataAnnotations;

namespace be.Models;

// Model สำหรับ "อ่าน" (JOIN แล้ว)
public class LeaveRequestReadModel
{
    public int LeaveRequestId { get; set; }
    public required string LeaveTypeName { get; set; } // (จากตาราง LeaveTypes)
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Reason { get; set; }
    public required string Status { get; set; } // (Pending, Approved, Rejected)
    public DateTime RequestedOn { get; set; }
}

// Model สำหรับ "สร้าง" (พนักงานส่งมา)
public class LeaveRequestWriteModel
{
    [Required]
    public int EmployeeId { get; set; } // (FE ต้องส่งมา)

    [Required]
    public int LeaveTypeId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public string? Reason { get; set; }
}