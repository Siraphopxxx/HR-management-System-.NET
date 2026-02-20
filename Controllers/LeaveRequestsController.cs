using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using be.Helpers; // (Import เครื่องคำนวณ)

namespace be.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaveRequestsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public LeaveRequestsController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // (Model เล็กๆ ไว้เก็บยอดคงเหลือ)
    private class LeaveBalanceInfo
    {
        public int TotalQuotaDays { get; set; } = 0;
        public int UsedDays { get; set; } = 0;
        public int RemainingDays { get { return TotalQuotaDays - UsedDays; } }
    }

    // --- (GET: GetMyRequests - เหมือนเดิม) ---
    [HttpGet("my-requests/{employeeId}")]
    public async Task<ActionResult<IEnumerable<LeaveRequestReadModel>>> GetMyRequests(int employeeId)
    {
        // ... (โค้ดเหมือนเดิม) ...
        var requests = new List<LeaveRequestReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                SELECT 
                    r.LeaveRequestId,
                    t.LeaveTypeName,
                    r.StartDate,
                    r.EndDate,
                    r.Reason,
                    r.Status,
                    r.RequestedOn
                FROM LeaveRequests r
                JOIN LeaveTypes t ON r.LeaveTypeId = t.LeaveTypeId
                WHERE r.EmployeeId = @EmployeeId
                ORDER BY r.RequestedOn DESC;
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
                            requests.Add(new LeaveRequestReadModel
                            {
                                LeaveRequestId = reader.GetInt32(0),
                                LeaveTypeName = reader.GetString(1),
                                StartDate = reader.GetDateTime(2),
                                EndDate = reader.GetDateTime(3),
                                Reason = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Status = reader.GetString(5),
                                RequestedOn = reader.GetDateTime(6)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(requests);
    }

    // --- (POST: SubmitLeaveRequest - *** นี่คือส่วนที่ผ่าตัดใหญ่ (อีกครั้ง) ***) ---
    [HttpPost]
    public async Task<ActionResult> SubmitLeaveRequest([FromBody] LeaveRequestWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (model.EndDate.Date < model.StartDate.Date)
        {
            return BadRequest(new { message = "End date cannot be before start date." });
        }

        // (*** 1. เรียก "เครื่องคำนวณ" (เหมือนเดิม) ***)
        int calculatedDays = await LeaveCalculationHelper.CalculateBusinessDays(
            model.StartDate,
            model.EndDate,
            _connectionString
        );

        if (calculatedDays <= 0)
        {
            return BadRequest(new { message = "The selected date range does not contain any working days." });
        }

        // (*** 2. (ใหม่!) เช็กยอดคงเหลือ (Validation) ***)
        LeaveBalanceInfo balance = new LeaveBalanceInfo();
        using (SqlConnection balanceConn = new SqlConnection(_connectionString))
        {
            // (ใช้ SQL คล้ายๆ กับใน LeaveBalanceController)
            string balanceSql = @"
                SELECT 
                    ISNULL(lq.DefaultDays, 0) AS TotalQuotaDays,
                    ISNULL(Used.UsedDays, 0) AS UsedDays
                FROM LeaveTypes lt
                LEFT JOIN LeaveQuotas lq ON lt.LeaveTypeId = lq.LeaveTypeId
                LEFT JOIN (
                    SELECT 
                        LeaveTypeId, 
                        SUM(CalculatedLeaveDays) AS UsedDays
                    FROM LeaveRequests
                    WHERE EmployeeId = @EmployeeId 
                      AND LeaveTypeId = @LeaveTypeId
                      AND (Status = 'Approved' OR Status = 'Pending') -- (สำคัญ: นับ Pending ด้วย!)
                    GROUP BY LeaveTypeId
                ) AS Used ON lt.LeaveTypeId = Used.LeaveTypeId
                WHERE lt.LeaveTypeId = @LeaveTypeId;
            ";
            using (SqlCommand balanceCmd = new SqlCommand(balanceSql, balanceConn))
            {
                balanceCmd.Parameters.AddWithValue("@EmployeeId", model.EmployeeId);
                balanceCmd.Parameters.AddWithValue("@LeaveTypeId", model.LeaveTypeId);
                try
                {
                    await balanceConn.OpenAsync();
                    using (SqlDataReader reader = await balanceCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            balance.TotalQuotaDays = reader.GetInt32(0);
                            balance.UsedDays = reader.GetInt32(1);
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error checking balance."); }
            }
        }

        // (*** 3. (ใหม่!) บล็อก (Block) ถ้าโควตาไม่พอ ***)
        if (balance.RemainingDays < calculatedDays)
        {
            return BadRequest(new
            {
                message = $"You do not have enough leave quota. You requested {calculatedDays} days, but you only have {balance.RemainingDays} days remaining."
            });
        }
        // (*** จบส่วน Validation ***)


        // (4. หา ManagerId - เหมือนเดิม)
        int? managerId = null;
        using (SqlConnection findMgrConn = new SqlConnection(_connectionString))
        {
            // ... (โค้ดหา ManagerId เหมือนเดิม) ...
            string findMgrSql = "SELECT ManagerId FROM Employees WHERE EmployeeId = @EmployeeId";
            using (SqlCommand findMgrCmd = new SqlCommand(findMgrSql, findMgrConn))
            {
                findMgrCmd.Parameters.AddWithValue("@EmployeeId", model.EmployeeId);
                try
                {
                    await findMgrConn.OpenAsync();
                    var result = await findMgrCmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        managerId = (int)result;
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error finding manager."); }
            }
        }

        // (5. อัปเกรด SQL INSERT - เหมือนเดิม)
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // ... (โค้ด INSERT เหมือนเดิม) ...
            string sql = @"
                INSERT INTO LeaveRequests 
                    (EmployeeId, LeaveTypeId, StartDate, EndDate, Reason, ManagerId, Status, RequestedOn, CalculatedLeaveDays) 
                VALUES 
                    (@EmployeeId, @LeaveTypeId, @StartDate, @EndDate, @Reason, @ManagerId, 'Pending', GETDATE(), @CalculatedLeaveDays)
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@EmployeeId", model.EmployeeId);
                command.Parameters.AddWithValue("@LeaveTypeId", model.LeaveTypeId);
                command.Parameters.AddWithValue("@StartDate", model.StartDate.Date);
                command.Parameters.AddWithValue("@EndDate", model.EndDate.Date);
                command.Parameters.AddWithValue("@Reason", (object?)model.Reason ?? DBNull.Value);
                command.Parameters.AddWithValue("@ManagerId", (object?)managerId ?? DBNull.Value);
                command.Parameters.AddWithValue("@CalculatedLeaveDays", calculatedDays);

                try
                {
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error submitting request."); }
            }
        }

        return StatusCode(201, new { message = $"Leave request submitted ({calculatedDays} working days)." });
    }

    // --- (DELETE: CancelLeaveRequest - เหมือนเดิม) ---
    [HttpDelete("{id}/{employeeId}")]
    public async Task<IActionResult> CancelLeaveRequest(int id, int employeeId)
    {
        // ... (โค้ดเหมือนเดิม) ...
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "DELETE FROM LeaveRequests WHERE LeaveRequestId = @Id AND EmployeeId = @EmployeeId AND Status = 'Pending'";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@EmployeeId", employeeId);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = "Request not found, already approved, or you do not have permission." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent();
    }

    // --- (Manager Functions: GetTeamRequests, Approve, Reject - เหมือนเดิม) ---
    public class TeamLeaveRequestReadModel : LeaveRequestReadModel
    {
        public int EmployeeId { get; set; }
        public required string EmployeeName { get; set; }
    }

    [HttpGet("team-requests/{managerId}")]
    public async Task<ActionResult<IEnumerable<TeamLeaveRequestReadModel>>> GetTeamRequests(int managerId)
    {
        // ... (โค้ดเหมือนเดิม) ...
        var requests = new List<TeamLeaveRequestReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                SELECT 
                    r.LeaveRequestId,
                    t.LeaveTypeName,
                    r.StartDate,
                    r.EndDate,
                    r.Reason,
                    r.Status,
                    r.RequestedOn,
                    e.EmployeeId,
                    e.FirstName,
                    e.LastName
                FROM LeaveRequests r
                JOIN LeaveTypes t ON r.LeaveTypeId = t.LeaveTypeId
                JOIN Employees e ON r.EmployeeId = e.EmployeeId
                WHERE r.ManagerId = @ManagerId AND r.Status = 'Pending'
                ORDER BY r.RequestedOn ASC;
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@ManagerId", managerId);
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            requests.Add(new TeamLeaveRequestReadModel
                            {
                                LeaveRequestId = reader.GetInt32(0),
                                LeaveTypeName = reader.GetString(1),
                                StartDate = reader.GetDateTime(2),
                                EndDate = reader.GetDateTime(3),
                                Reason = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Status = reader.GetString(5),
                                RequestedOn = reader.GetDateTime(6),
                                EmployeeId = reader.GetInt32(7),
                                EmployeeName = $"{reader.GetString(8)} {reader.GetString(9)}"
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(requests);
    }

    [HttpPost("approve/{id}")]
    public async Task<IActionResult> ApproveRequest(int id, [FromQuery] int managerId)
    {
        // ... (โค้ดเหมือนเดิม) ...
        string sql = @"
            UPDATE LeaveRequests 
            SET Status = 'Approved', ApprovedOn = GETDATE()
            WHERE LeaveRequestId = @Id AND ManagerId = @ManagerId AND Status = 'Pending'
        ";

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@ManagerId", managerId);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = "Request not found, already actioned, or you are not the manager." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(new { message = "Request approved successfully." });
    }

    [HttpPost("reject/{id}")]
    public async Task<IActionResult> RejectRequest(int id, [FromQuery] int managerId)
    {
        // ... (โค้ดเหมือนเดิม) ...
        string sql = @"
            UPDATE LeaveRequests 
            SET Status = 'Rejected', ApprovedOn = GETDATE()
            WHERE LeaveRequestId = @Id AND ManagerId = @ManagerId AND Status = 'Pending'
        ";

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@ManagerId", managerId);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = "Request not found, already actioned, or you are not the manager." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(new { message = "Request rejected successfully." });
    }
}