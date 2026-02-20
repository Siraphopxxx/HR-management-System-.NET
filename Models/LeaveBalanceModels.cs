namespace be.Models;

// Modelสำหรับ "อ่าน" ยอดคงเหลือ
public class LeaveBalanceReadModel
{
    public int LeaveTypeId { get; set; }
    public required string LeaveTypeName { get; set; }
    public int TotalQuotaDays { get; set; } // โควตาทั้งหมด (จาก LeaveQuotas)
    public int UsedDays { get; set; }       // จำนวนวันที่ใช้ไป (SUM จาก LeaveRequests)
    public int RemainingDays { get; set; }  // วันที่เหลือ (คำนวณแล้ว)
}