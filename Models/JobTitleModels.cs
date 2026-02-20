using System.ComponentModel.DataAnnotations;

namespace be.Models; // << ตรวจสอบ namespace

// Model สำหรับ "อ่าน" ข้อมูลตำแหน่งงาน (มี ID)
public class JobTitleReadModel
{
    public int JobTitleId { get; set; }
    public required string TitleName { get; set; }
}

// Model สำหรับ "สร้าง" หรือ "แก้ไข" ตำแหน่งงาน (ไม่มี ID)
public class JobTitleWriteModel
{
    [Required]
    [MaxLength(100)]
    public required string TitleName { get; set; }
}