using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/manager")]
public class ManagerController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public ManagerController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- GET: api/manager/my-team/{managerId} ---
    [HttpGet("my-team/{managerId}")]
    public async Task<ActionResult<IEnumerable<TeamMemberReadModel>>> GetMyTeam(int managerId)
    {
        var team = new List<TeamMemberReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // (JOIN กับ JobTitles เพื่อเอาชื่อตำแหน่งมา)
            string sql = @"
                SELECT 
                    e.EmployeeId,
                    e.EmployeeNumber,
                    e.FirstName,
                    e.LastName,
                    j.TitleName AS JobTitle,
                    e.Email,
                    e.PhoneNumber
                FROM Employees e
                LEFT JOIN JobTitles j ON e.JobTitleId = j.JobTitleId
                WHERE e.ManagerId = @ManagerId
                ORDER BY e.FirstName;
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
                            team.Add(new TeamMemberReadModel
                            {
                                EmployeeId = reader.GetInt32(0),
                                EmployeeNumber = reader.GetString(1),
                                FullName = $"{reader.GetString(2)} {reader.GetString(3)}",
                                JobTitle = reader.IsDBNull(4) ? "N/A" : reader.GetString(4),
                                Email = reader.IsDBNull(5) ? "N/A" : reader.GetString(5),
                                PhoneNumber = reader.IsDBNull(6) ? "N/A" : reader.GetString(6)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(team);
    }
}