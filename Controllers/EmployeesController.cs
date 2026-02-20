using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public EmployeesController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- GET: api/employees/all-basic ---
    // (ดึงพนักงาน "ทุกคน" เพื่อใช้ใน Dropdown)
    [HttpGet("all-basic")]
    public async Task<ActionResult<IEnumerable<EmployeeBasicModel>>> GetAllEmployeesBasic()
    {
        var employees = new List<EmployeeBasicModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "SELECT EmployeeId, FirstName, LastName, EmployeeNumber FROM Employees ORDER BY FirstName";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            employees.Add(new EmployeeBasicModel
                            {
                                EmployeeId = reader.GetInt32(0),
                                FullName = $"{reader.GetString(1)} {reader.GetString(2)}",
                                EmployeeNumber = reader.GetString(3)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(employees);
    }
}