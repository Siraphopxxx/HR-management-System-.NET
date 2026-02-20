using System.ComponentModel.DataAnnotations;

namespace be.Models;

// Model สำหรับ "อ่าน"
public class AnnouncementReadModel
{
    public int AnnouncementId { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public DateTime PostedDate { get; set; }
}

// Model สำหรับ "เขียน" (สร้าง/แก้ไข)
public class AnnouncementWriteModel
{
    [Required]
    [MaxLength(255)]
    public required string Title { get; set; }

    [Required]
    public required string Content { get; set; }
}