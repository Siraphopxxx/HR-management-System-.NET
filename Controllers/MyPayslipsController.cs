using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/my-payslips")]
public class MyPayslipsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public MyPayslipsController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- 1. GET: api/my-payslips/{employeeId} (ดึง "ประวัติ" สลิปทั้งหมดของฉัน) ---
    [HttpGet("{employeeId}")]
    public async Task<ActionResult<IEnumerable<MyPayslipHistoryModel>>> GetMyPayslipHistory(int employeeId)
    {
        var history = new List<MyPayslipHistoryModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                SELECT PayslipId, PayDate, NetSalary 
                FROM Payslips 
                WHERE EmployeeId = @EmployeeId
                ORDER BY PayDate DESC;
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
                            history.Add(new MyPayslipHistoryModel
                            {
                                PayslipId = reader.GetInt32(0),
                                PayDate = reader.GetDateTime(1),
                                NetSalary = reader.GetDecimal(2)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(history);
    }

    // --- 2. GET: api/my-payslips/detail/{payslipId}/{employeeId} (ดึง "สลิปเต็ม" 1 ใบ) ---
    [HttpGet("detail/{payslipId}/{employeeId}")]
    public async Task<ActionResult<PayslipDetailModel>> GetPayslipDetail(int payslipId, int employeeId)
    {
        PayslipDetailModel? detail = null;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            // (1. ดึง "หัวสลิป" ... (*** Security Check: เช็กว่า EmployeeId ตรงกัน ***))
            string headerSql = @"
                SELECT 
                    p.PayslipId, (e.FirstName + ' ' + e.LastName) AS EmployeeName,
                    e.EmployeeNumber, d.DepartmentName, p.PayDate,
                    p.TotalIncome, p.TotalDeductions, p.NetSalary
                FROM Payslips p
                JOIN Employees e ON p.EmployeeId = e.EmployeeId
                LEFT JOIN Departments d ON e.DepartmentId = d.DepartmentId
                WHERE p.PayslipId = @PayslipId AND p.EmployeeId = @EmployeeId; -- (*** Security Check! ***)
            ";
            using (SqlCommand headerCmd = new SqlCommand(headerSql, connection))
            {
                headerCmd.Parameters.AddWithValue("@PayslipId", payslipId);
                headerCmd.Parameters.AddWithValue("@EmployeeId", employeeId);

                using (SqlDataReader reader = await headerCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        detail = new PayslipDetailModel
                        {
                            PayslipId = reader.GetInt32(0),
                            EmployeeName = reader.GetString(1),
                            EmployeeNumber = reader.GetString(2),
                            DepartmentName = reader.IsDBNull(3) ? "N/A" : reader.GetString(3),
                            PayDate = reader.GetDateTime(4),
                            TotalIncome = reader.GetDecimal(5),
                            TotalDeductions = reader.GetDecimal(6),
                            NetSalary = reader.GetDecimal(7)
                        };
                    }
                }
            }

            // (ถ้าไม่เจอ ➔ แปลว่า "ไม่ใช่" สลิปของคุณ)
            if (detail == null)
            {
                return NotFound(new { message = "Payslip not found or access denied." });
            }

            // (2. ดึง "รายการย่อย" (Items))
            string itemsSql = "SELECT ItemType, Description, Amount FROM PayslipItems WHERE PayslipId = @PayslipId";
            using (SqlCommand itemsCmd = new SqlCommand(itemsSql, connection))
            {
                itemsCmd.Parameters.AddWithValue("@PayslipId", payslipId);
                using (SqlDataReader reader = await itemsCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var item = new PayslipItemModel
                        {
                            ItemType = reader.GetString(0),
                            Description = reader.GetString(1),
                            Amount = reader.GetDecimal(2)
                        };

                        if (item.ItemType == "Income")
                        {
                            detail.IncomeItems.Add(item);
                        }
                        else
                        {
                            detail.DeductionItems.Add(item);
                        }
                    }
                }
            }
        }
        return Ok(detail);
    }
}