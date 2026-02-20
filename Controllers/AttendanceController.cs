using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using be.Helpers; // (Import เครื่องคำนวณ)

namespace be.Controllers;

[ApiController]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public AttendanceController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- (1. GET: /status/{employeeId} - ฟังก์ชัน "ฉลาด" ที่เราเพิ่งทำ - เหมือนเดิม) ---
    [HttpGet("status/{employeeId}")]
    public async Task<ActionResult<AttendanceStatusModel>> GetTodayAttendanceStatus(int employeeId)
    {
        DateTime today = DateTime.Today;

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            // (ลำดับที่ 1: เช็ก "วันหยุด")
            string holidaySql = "SELECT HolidayName FROM PublicHolidays WHERE HolidayDate = @Today";
            using (SqlCommand holidayCmd = new SqlCommand(holidaySql, connection))
            {
                holidayCmd.Parameters.AddWithValue("@Today", today);
                var holidayName = await holidayCmd.ExecuteScalarAsync();
                if (holidayName != null)
                {
                    return Ok(new AttendanceStatusModel
                    {
                        AttendanceId = 0,
                        Status = $"Holiday ({holidayName})",
                        CheckIn = null,
                        CheckOut = null,
                        Date = today
                    });
                }
            }

            // (ลำดับที่ 2: เช็ก "วันลา")
            string leaveSql = @"
                SELECT lt.LeaveTypeName 
                FROM LeaveRequests lr
                JOIN LeaveTypes lt ON lr.LeaveTypeId = lt.LeaveTypeId
                WHERE lr.EmployeeId = @EmployeeId 
                  AND lr.Status = 'Approved'
                  AND @Today BETWEEN lr.StartDate AND lr.EndDate;
            ";
            using (SqlCommand leaveCmd = new SqlCommand(leaveSql, connection))
            {
                leaveCmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                leaveCmd.Parameters.AddWithValue("@Today", today);
                var leaveName = await leaveCmd.ExecuteScalarAsync();
                if (leaveName != null)
                {
                    return Ok(new AttendanceStatusModel
                    {
                        AttendanceId = 0,
                        Status = $"On Leave ({leaveName})",
                        CheckIn = null,
                        CheckOut = null,
                        Date = today
                    });
                }
            }

            // (ลำดับที่ 3: เช็ก "ตอกบัตร" แล้ว)
            AttendanceStatusModel? attendance = null;
            string sql = "SELECT AttendanceId, CheckIn, CheckOut, Status, Date FROM Attendance WHERE EmployeeId = @EmployeeId AND [Date] = @Today";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@EmployeeId", employeeId);
                command.Parameters.AddWithValue("@Today", today);
                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            attendance = new AttendanceStatusModel
                            {
                                AttendanceId = reader.GetInt32(0),
                                CheckIn = reader.IsDBNull(1) ? (TimeSpan?)null : reader.GetTimeSpan(1),
                                CheckOut = reader.IsDBNull(2) ? (TimeSpan?)null : reader.GetTimeSpan(2),
                                Status = reader.GetString(3),
                                Date = reader.GetDateTime(4)
                            };
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }

            if (attendance != null)
            {
                return Ok(attendance);
            }
        }

        // (ลำดับที่ 4: = ยังไม่ตอกบัตร)
        return NotFound(new { message = "Absent / Not Checked In" });
    }

    // (*** 2. (ใหม่!) GET: /api/attendance/{employeeId} - ดึงประวัติทั้งหมด ***)
    [HttpGet("{employeeId}")]
    public async Task<ActionResult<IEnumerable<AttendanceStatusModel>>> GetAttendanceHistory(int employeeId)
    {
        var history = new List<AttendanceStatusModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // (ดึงข้อมูลย้อนหลัง 90 วัน)
            string sql = @"
                SELECT AttendanceId, [Date], CheckIn, CheckOut, Status 
                FROM Attendance 
                WHERE EmployeeId = @EmployeeId
                  AND [Date] >= DATEADD(day, -90, GETDATE())
                ORDER BY [Date] DESC;
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@EmployeeId", employeeId);
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            history.Add(new AttendanceStatusModel
                            {
                                AttendanceId = reader.GetInt32(0),
                                Date = reader.GetDateTime(1),
                                CheckIn = reader.IsDBNull(2) ? (TimeSpan?)null : reader.GetTimeSpan(2),
                                CheckOut = reader.IsDBNull(3) ? (TimeSpan?)null : reader.GetTimeSpan(3),
                                Status = reader.GetString(4)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error fetching history."); }
            }
        }
        return Ok(history);
    }


    // --- (3. POST: /check-in/{employeeId} - เหมือนเดิม) ---
    [HttpPost("check-in/{employeeId}")]
    public async Task<ActionResult<AttendanceStatusModel>> CheckIn(int employeeId)
    {
        // ... (โค้ด CheckIn ที่ฉลาดของเรา เหมือนเดิม) ...
        DateTime today = DateTime.Today;
        TimeSpan now = DateTime.Now.TimeOfDay;

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            string checkSql = "SELECT AttendanceId FROM Attendance WHERE EmployeeId = @EmployeeId AND [Date] = @Today";
            using (SqlCommand checkCmd = new SqlCommand(checkSql, connection))
            {
                checkCmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                checkCmd.Parameters.AddWithValue("@Today", today);
                var result = await checkCmd.ExecuteScalarAsync();
                if (result != null)
                {
                    return Conflict(new { message = "You have already checked in today." });
                }
            }

            string holidaySql = "SELECT HolidayName FROM PublicHolidays WHERE HolidayDate = @Today";
            using (SqlCommand holidayCmd = new SqlCommand(holidaySql, connection))
            {
                holidayCmd.Parameters.AddWithValue("@Today", today);
                if (await holidayCmd.ExecuteScalarAsync() != null)
                {
                    return BadRequest(new { message = "Cannot check in on a Public Holiday." });
                }
            }
            string leaveSql = "SELECT 1 FROM LeaveRequests lr WHERE lr.EmployeeId = @EmployeeId AND lr.Status = 'Approved' AND @Today BETWEEN lr.StartDate AND lr.EndDate";
            using (SqlCommand leaveCmd = new SqlCommand(leaveSql, connection))
            {
                leaveCmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                leaveCmd.Parameters.AddWithValue("@Today", today);
                if (await leaveCmd.ExecuteScalarAsync() != null)
                {
                    return BadRequest(new { message = "Cannot check in on a day you are approved for leave." });
                }
            }

            int newAttendanceId;
            string sql = @"
                INSERT INTO Attendance (EmployeeId, [Date], CheckIn, Status) 
                OUTPUT INSERTED.AttendanceId
                VALUES (@EmployeeId, @Today, @Now, 'Present');
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@EmployeeId", employeeId);
                command.Parameters.AddWithValue("@Today", today);
                command.Parameters.AddWithValue("@Now", now);
                try
                {
                    newAttendanceId = (int)await command.ExecuteScalarAsync();
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }

            var newStatus = new AttendanceStatusModel
            {
                AttendanceId = newAttendanceId,
                CheckIn = now,
                CheckOut = null,
                Status = "Present",
                Date = today
            };
            return StatusCode(201, newStatus);
        }
    }

    // --- (4. POST: /check-out/{employeeId} - เหมือนเดิม) ---
    [HttpPost("check-out/{employeeId}")]
    public async Task<ActionResult<AttendanceStatusModel>> CheckOut(int employeeId)
    {
        // ... (โค้ด CheckOut เหมือนเดิม) ...
        DateTime today = DateTime.Today;
        TimeSpan now = DateTime.Now.TimeOfDay;
        AttendanceStatusModel? updatedStatus = null;

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                UPDATE Attendance 
                SET CheckOut = @Now 
                OUTPUT INSERTED.AttendanceId, INSERTED.CheckIn, INSERTED.CheckOut, INSERTED.Status, INSERTED.[Date]
                WHERE EmployeeId = @EmployeeId AND [Date] = @Today AND CheckOut IS NULL;
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@EmployeeId", employeeId);
                command.Parameters.AddWithValue("@Today", today);
                command.Parameters.AddWithValue("@Now", now);
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            updatedStatus = new AttendanceStatusModel
                            {
                                AttendanceId = reader.GetInt32(0),
                                CheckIn = reader.GetTimeSpan(1),
                                CheckOut = reader.GetTimeSpan(2),
                                Status = reader.GetString(3),
                                Date = reader.GetDateTime(4)
                            };
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }

        if (updatedStatus == null)
        {
            return NotFound(new { message = "You have not checked in today or you have already checked out." });
        }

        return Ok(updatedStatus);
    }
}