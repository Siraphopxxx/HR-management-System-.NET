using Microsoft.AspNetCore.Mvc;
using be.Models; // <<< *** นี่คือบรรทัดที่น่าจะขาดไป ***
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers; // << ตรวจสอบว่า namespace คือ 'be.Controllers'

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                           ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // POST: api/auth/simple-login
    [HttpPost("simple-login")]
    public async Task<ActionResult<object>> SimpleLogin([FromBody] SimpleLoginModel model) // <<< ถ้า using ถูกต้อง ตรงนี้จะไม่แดง
    {
        object? userResult = null;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                SELECT 
                    u.Id AS AppUserId, u.Name, u.Role, u.Password,
                    e.EmployeeId, e.EmployeeNumber, e.FirstName, e.LastName,
                    d.DepartmentName, 
                    j.TitleName AS Position 
                FROM AppUsers u
                LEFT JOIN Employees e ON e.AppUserId = u.Id
                LEFT JOIN Departments d ON e.DepartmentId = d.DepartmentId
                LEFT JOIN JobTitles j ON e.JobTitleId = j.JobTitleId
                WHERE u.Name = @Name";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Name", model.Name); // <<< ไม่แดง
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string storedPassword = reader.GetString(reader.GetOrdinal("Password"));
                            if (storedPassword == model.Password) // <<< ไม่แดง
                            {
                                userResult = new
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("AppUserId")),
                                    Name = reader.GetString(reader.GetOrdinal("Name")),
                                    Role = reader.GetString(reader.GetOrdinal("Role")),
                                    EmployeeId = reader.IsDBNull(reader.GetOrdinal("EmployeeId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                                    EmployeeNumber = reader.IsDBNull(reader.GetOrdinal("EmployeeNumber")) ? null : reader.GetString(reader.GetOrdinal("EmployeeNumber")),
                                    FirstName = reader.IsDBNull(reader.GetOrdinal("FirstName")) ? null : reader.GetString(reader.GetOrdinal("FirstName")),
                                    LastName = reader.IsDBNull(reader.GetOrdinal("LastName")) ? null : reader.GetString(reader.GetOrdinal("LastName")),
                                    Department = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                                    Position = reader.IsDBNull(reader.GetOrdinal("Position")) ? null : reader.GetString(reader.GetOrdinal("Position"))
                                };
                            }
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        if (userResult == null) return Unauthorized(new { message = "Invalid name or password." });
        return Ok(userResult);
    }
}