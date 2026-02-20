using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace be.Controllers;

[ApiController]
[Route("api/[controller]")]
// [Authorize(Roles = "Admin")] // << ถ้ากลับไปใช้ระบบ Token ค่อยเปิด
public class DashboardController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public DashboardController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                           ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // GET: api/dashboard/stats
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        int userCount = 0;
        int employeeCount = 0;
        int departmentCount = 0;
        int jobTitleCount = 0;

        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();

            // Query 4 ตารางพร้อมกันโดยใช้ UNION ALL เพื่อลดการเชื่อมต่อ
            string sql = @"
                SELECT 'Users', COUNT(*) FROM AppUsers UNION ALL
                SELECT 'Employees', COUNT(*) FROM Employees UNION ALL
                SELECT 'Departments', COUNT(*) FROM Departments UNION ALL
                SELECT 'JobTitles', COUNT(*) FROM JobTitles;
            ";

            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                try
                {
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string type = reader.GetString(0);
                            int count = reader.GetInt32(1);

                            if (type == "Users") userCount = count;
                            else if (type == "Employees") employeeCount = count;
                            else if (type == "Departments") departmentCount = count;
                            else if (type == "JobTitles") jobTitleCount = count;
                        }
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"SQL Error getting stats: {ex.Message}");
                    return StatusCode(500, "Database error occurred.");
                }
            }
        }

        // ส่งข้อมูลสถิติกลับไปเป็น JSON
        return Ok(new
        {
            UserCount = userCount,
            EmployeeCount = employeeCount,
            DepartmentCount = departmentCount,
            JobTitleCount = jobTitleCount
        });
    }
}