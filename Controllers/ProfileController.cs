using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public ProfileController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- GET: api/profile/{employeeId} ---
    [HttpGet("{employeeId}")]
    public async Task<ActionResult<MyProfileReadModel>> GetMyProfile(int employeeId)
    {
        MyProfileReadModel? profile = null;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // (SQL JOIN 5 ตาราง: Employees, AppUsers, Departments, JobTitles, และ Employees (อีกครั้ง) เพื่อหาชื่อ Manager)
            string sql = @"
                SELECT 
                    e.EmployeeNumber, e.FirstName, e.LastName,
                    e.DateOfBirth, e.Gender, e.Email, e.PhoneNumber,
                    e.Address_Street, e.Address_City, e.Address_State, e.Address_PostalCode, e.Address_Country,
                    e.CurrentSalary,
                    u.Name AS Username, u.Role,
                    d.DepartmentName,
                    j.TitleName AS JobTitleName,
                    ISNULL(m.FirstName + ' ' + m.LastName, 'N/A') AS ManagerName
                FROM Employees e
                LEFT JOIN AppUsers u ON e.AppUserId = u.Id
                LEFT JOIN Departments d ON e.DepartmentId = d.DepartmentId
                LEFT JOIN JobTitles j ON e.JobTitleId = j.JobTitleId
                LEFT JOIN Employees m ON e.ManagerId = m.EmployeeId -- (JOIN กับตัวเอง เพื่อหาชื่อ Manager)
                WHERE e.EmployeeId = @EmployeeId;
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@EmployeeId", employeeId);
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            profile = new MyProfileReadModel
                            {
                                EmployeeNumber = reader["EmployeeNumber"].ToString() ?? "",
                                FirstName = reader["FirstName"].ToString() ?? "",
                                LastName = reader["LastName"].ToString() ?? "",
                                DateOfBirth = reader.IsDBNull("DateOfBirth") ? (DateTime?)null : (DateTime?)reader["DateOfBirth"],
                                Gender = reader.IsDBNull("Gender") ? null : reader["Gender"].ToString(),
                                Email = reader.IsDBNull("Email") ? null : reader["Email"].ToString(),
                                PhoneNumber = reader.IsDBNull("PhoneNumber") ? null : reader["PhoneNumber"].ToString(),
                                Address_Street = reader.IsDBNull("Address_Street") ? null : reader["Address_Street"].ToString(),
                                Address_City = reader.IsDBNull("Address_City") ? null : reader["Address_City"].ToString(),
                                Address_State = reader.IsDBNull("Address_State") ? null : reader["Address_State"].ToString(),
                                Address_PostalCode = reader.IsDBNull("Address_PostalCode") ? null : reader["Address_PostalCode"].ToString(),
                                Address_Country = reader.IsDBNull("Address_Country") ? null : reader["Address_Country"].ToString(),
                                CurrentSalary = reader.IsDBNull("CurrentSalary") ? (decimal?)null : (decimal?)reader["CurrentSalary"],
                                Username = reader["Username"].ToString() ?? "",
                                Role = reader["Role"].ToString() ?? "",
                                DepartmentName = reader.IsDBNull("DepartmentName") ? null : reader["DepartmentName"].ToString(),
                                JobTitleName = reader.IsDBNull("JobTitleName") ? null : reader["JobTitleName"].ToString(),
                                ManagerName = reader["ManagerName"].ToString()
                            };
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        if (profile == null) return NotFound(new { message = "Profile not found." });
        return Ok(profile);
    }
}