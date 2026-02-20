using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/leave-balance")]
public class LeaveBalanceController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public LeaveBalanceController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    [HttpGet("{employeeId}")]
    public async Task<ActionResult<IEnumerable<LeaveBalanceReadModel>>> GetLeaveBalance(int employeeId)
    {
        var balanceList = new List<LeaveBalanceReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                SELECT 
                    lt.LeaveTypeId,
                    lt.LeaveTypeName,
                    ISNULL(lq.DefaultDays, 0) AS TotalQuotaDays,
                    ISNULL(Used.UsedDays, 0) AS UsedDays,
                    (ISNULL(lq.DefaultDays, 0) - ISNULL(Used.UsedDays, 0)) AS RemainingDays
                FROM LeaveTypes lt
                LEFT JOIN LeaveQuotas lq ON lt.LeaveTypeId = lq.LeaveTypeId
                LEFT JOIN (
                    SELECT 
                        LeaveTypeId, 
                        SUM(CalculatedLeaveDays) AS UsedDays
                    FROM LeaveRequests
                    WHERE EmployeeId = @EmployeeId 
                      AND (Status = 'Approved' OR Status = 'Pending') -- (*** แก้ไขตรงนี้ ***)
                    GROUP BY LeaveTypeId
                ) AS Used ON lt.LeaveTypeId = Used.LeaveTypeId
                ORDER BY lt.LeaveTypeName;
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
                            balanceList.Add(new LeaveBalanceReadModel
                            {
                                LeaveTypeId = reader.GetInt32(0),
                                LeaveTypeName = reader.GetString(1),
                                TotalQuotaDays = reader.GetInt32(2),
                                UsedDays = reader.GetInt32(3),
                                RemainingDays = reader.GetInt32(4)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error calculating balance."); }
            }
        }
        return Ok(balanceList);
    }
}