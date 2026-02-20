using System.ComponentModel.DataAnnotations;

namespace be.Models;

// Model สำหรับ "อ่าน" (ต้อง JOIN)
public class LeaveQuotaReadModel
{
    public int LeaveQuotaId { get; set; }
    public int LeaveTypeId { get; set; }
    public required string LeaveTypeName { get; set; } // << เราจะ JOIN มา
    public int DefaultDays { get; set; }
}

// Model สำหรับ "สร้าง" หรือ "แก้ไข"
public class LeaveQuotaWriteModel
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Leave Type is required.")]
    public int LeaveTypeId { get; set; } // << ID ของประเภทการลา

    [Required]
    [Range(0, 365, ErrorMessage = "Days must be between 0 and 365.")]
    public int DefaultDays { get; set; } // << จำนวนวัน
}