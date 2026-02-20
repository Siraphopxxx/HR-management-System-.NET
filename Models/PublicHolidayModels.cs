using System.ComponentModel.DataAnnotations;

namespace be.Models;

// Model สำหรับ "อ่าน"
public class PublicHolidayReadModel
{
    public int HolidayId { get; set; }
    public DateTime HolidayDate { get; set; }
    public required string HolidayName { get; set; }
}

// Model สำหรับ "สร้าง" หรือ "แก้ไข"
public class PublicHolidayWriteModel
{
    [Required]
    public DateTime HolidayDate { get; set; }

    [Required]
    [MaxLength(100)]
    public required string HolidayName { get; set; }
}