using System.ComponentModel.DataAnnotations;

namespace be.Models; // << ตรวจสอบ namespace

// Model สำหรับ "อ่าน" ข้อมูลแผนก (มี ID)
public class DepartmentReadModel
{
    public int DepartmentId { get; set; }
    public required string DepartmentName { get; set; }
}

// Model สำหรับ "สร้าง" หรือ "แก้ไข" แผนก (ไม่มี ID)
public class DepartmentWriteModel
{
    [Required]
    [MaxLength(100)]
    public required string DepartmentName { get; set; }
}