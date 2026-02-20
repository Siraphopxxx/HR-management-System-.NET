namespace be.Models;

public class AttendanceStatusModel
{
    public int AttendanceId { get; set; }
    public TimeSpan? CheckIn { get; set; }
    public TimeSpan? CheckOut { get; set; }
    public required string Status { get; set; }
    public DateTime Date { get; set; }
}