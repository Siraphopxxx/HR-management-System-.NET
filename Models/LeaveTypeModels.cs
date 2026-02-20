using System.ComponentModel.DataAnnotations;

namespace be.Models;

// Model สำหรับ "อ่าน"
public class LeaveTypeReadModel
{
    public int LeaveTypeId { get; set; }
    public required string LeaveTypeName { get; set; }
    public string? Description { get; set; }
    public bool IsPaid { get; set; }
}

// Model สำหรับ "สร้าง" หรือ "แก้ไข"
public class LeaveTypeWriteModel
{
    [Required]
    [MaxLength(100)]
    public required string LeaveTypeName { get; set; }

    public string? Description { get; set; }

    [Required]
    public bool IsPaid { get; set; } = true; // ค่าเริ่มต้น
}