using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace be.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MasterDataController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public MasterDataController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                           ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // GET: api/masterdata/departments
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var list = new List<object>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "SELECT DepartmentId, DepartmentName FROM Departments ORDER BY DepartmentName";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new
                            {
                                departmentId = reader.GetInt32(0),
                                departmentName = reader.GetString(1)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(list);
    }

    // GET: api/masterdata/jobtitles
    [HttpGet("jobtitles")]
    public async Task<IActionResult> GetJobTitles()
    {
        var list = new List<object>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "SELECT JobTitleId, TitleName FROM JobTitles ORDER BY TitleName";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new
                            {
                                jobTitleId = reader.GetInt32(0),
                                titleName = reader.GetString(1)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(list);
    }
}