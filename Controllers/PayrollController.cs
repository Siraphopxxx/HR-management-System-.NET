using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/payroll")]
public class PayrollController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public PayrollController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- 1. GET: api/payroll/history (ดึงประวัติสลิปที่เคยสร้าง) ---
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<PayslipHistoryModel>>> GetPayrollHistory()
    {
        var history = new List<PayslipHistoryModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // (JOIN กับ Employees เพื่อเอา "ชื่อ")
            string sql = @"
                SELECT 
                    p.PayslipId, (e.FirstName + ' ' + e.LastName) AS EmployeeName,
                    p.PayDate, p.TotalIncome, p.TotalDeductions, p.NetSalary, p.Status
                FROM Payslips p
                JOIN Employees e ON p.EmployeeId = e.EmployeeId
                ORDER BY p.PayDate DESC, EmployeeName;
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            history.Add(new PayslipHistoryModel
                            {
                                PayslipId = reader.GetInt32(0),
                                EmployeeName = reader.GetString(1),
                                PayDate = reader.GetDateTime(2),
                                TotalIncome = reader.GetDecimal(3),
                                TotalDeductions = reader.GetDecimal(4),
                                NetSalary = reader.GetDecimal(5),
                                Status = reader.GetString(6)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(history);
    }

    // (*** นี่คือ "เครื่องคำนวณจำลอง" ***)
    // --- 2. POST: api/payroll/run ---
    [HttpPost("run")]
    public async Task<ActionResult> RunPayroll([FromBody] RunPayrollInputModel model)
    {
        if (model.Month < 1 || model.Month > 12 || model.Year < 2020)
        {
            return BadRequest(new { message = "Invalid month or year." });
        }

        DateTime payDate = new DateTime(model.Year, model.Month, DateTime.DaysInMonth(model.Year, model.Month));
        DateTime startDate = new DateTime(model.Year, model.Month, 1);
        DateTime endDate = payDate;
        int employeesProcessed = 0;

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            // (1. เช็กว่า "เคยรัน" เดือนนี้ไปหรือยัง)
            string checkSql = "SELECT 1 FROM Payslips WHERE MONTH(PayDate) = @Month AND YEAR(PayDate) = @Year";
            using (SqlCommand checkCmd = new SqlCommand(checkSql, connection))
            {
                checkCmd.Parameters.AddWithValue("@Month", model.Month);
                checkCmd.Parameters.AddWithValue("@Year", model.Year);
                var existing = await checkCmd.ExecuteScalarAsync();
                if (existing != null)
                {
                    return Conflict(new { message = $"Payroll for {model.Month}/{model.Year} has already been run." });
                }
            }

            // (2. ดึง "พนักงาน" และ "เงินเดือน" ทั้งหมด)
            var employeesToPay = new List<(int EmployeeId, decimal CurrentSalary)>();
            string empSql = "SELECT EmployeeId, CurrentSalary FROM Employees WHERE CurrentSalary > 0";
            using (SqlCommand empCmd = new SqlCommand(empSql, connection))
            {
                using (SqlDataReader reader = await empCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        employeesToPay.Add((reader.GetInt32(0), reader.GetDecimal(1)));
                    }
                }
            }

            // (3. "Loop" พนักงานทุกคน... และ "สร้างสลิป (จำลอง)" ทีละคน)
            foreach (var emp in employeesToPay)
            {
                // (*** นี่คือ Logic "จำลอง" ***)
                // (ในระบบจริง: เราจะดึง OT, Bonus, ภาษี, ประกันสังคม... แต่ตอนนี้เรา "ปลอม" มัน)
                decimal totalIncome = emp.CurrentSalary; // (ปลอม: รายรับ = เงินเดือน)
                decimal totalDeductions = 1000.00m;     // (ปลอม: หัก 1000 บาท (ภาษี+ประกันสังคม))
                decimal netSalary = totalIncome - totalDeductions;

                // (*** เริ่ม Transaction ต่อ 1 พนักงาน ***)
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // (3.1 สร้าง "หัวสลิป" (Payslips))
                        int newPayslipId;
                        string payslipSql = @"
                            INSERT INTO Payslips (EmployeeId, PayPeriodStartDate, PayPeriodEndDate, PayDate, TotalIncome, TotalDeductions, NetSalary, Status)
                            OUTPUT INSERTED.PayslipId
                            VALUES (@EmployeeId, @StartDate, @EndDate, @PayDate, @TotalIncome, @TotalDeductions, @NetSalary, 'Finalized');
                        ";
                        using (SqlCommand pCmd = new SqlCommand(payslipSql, connection, transaction))
                        {
                            pCmd.Parameters.AddWithValue("@EmployeeId", emp.EmployeeId);
                            pCmd.Parameters.AddWithValue("@StartDate", startDate);
                            pCmd.Parameters.AddWithValue("@EndDate", endDate);
                            pCmd.Parameters.AddWithValue("@PayDate", payDate);
                            pCmd.Parameters.AddWithValue("@TotalIncome", totalIncome);
                            pCmd.Parameters.AddWithValue("@TotalDeductions", totalDeductions);
                            pCmd.Parameters.AddWithValue("@NetSalary", netSalary);
                            newPayslipId = (int)await pCmd.ExecuteScalarAsync();
                        }

                        // (3.2 สร้าง "รายการ" (PayslipItems))
                        // (รายการ "เงินเดือน")
                        string itemSql1 = "INSERT INTO PayslipItems (PayslipId, ItemType, Description, Amount) VALUES (@PayslipId, 'Income', 'Salary', @Amount)";
                        using (SqlCommand iCmd1 = new SqlCommand(itemSql1, connection, transaction))
                        {
                            iCmd1.Parameters.AddWithValue("@PayslipId", newPayslipId);
                            iCmd1.Parameters.AddWithValue("@Amount", totalIncome);
                            await iCmd1.ExecuteNonQueryAsync();
                        }

                        // (รายการ "หัก (ปลอม)")
                        string itemSql2 = "INSERT INTO PayslipItems (PayslipId, ItemType, Description, Amount) VALUES (@PayslipId, 'Deduction', 'Tax/SS (Mock)', @Amount)";
                        using (SqlCommand iCmd2 = new SqlCommand(itemSql2, connection, transaction))
                        {
                            iCmd2.Parameters.AddWithValue("@PayslipId", newPayslipId);
                            iCmd2.Parameters.AddWithValue("@Amount", totalDeductions);
                            await iCmd2.ExecuteNonQueryAsync();
                        }

                        // (Commit ถ้าทำสำเร็จ 3 ตาราง)
                        await transaction.CommitAsync();
                        employeesProcessed++;
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        Console.WriteLine($"Failed to process payroll for EmployeeId {emp.EmployeeId}: {ex.Message}");
                        // (ข้ามพนักงานคนนี้... แล้วไปทำคนต่อไป)
                    }
                } // (จบ Transaction)
            } // (จบ Loop)
        } // (จบ Connection)

        return Ok(new { message = $"Payroll for {model.Month}/{model.Year} completed. {employeesProcessed} payslips generated." });
    }
}