using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/project-members")]
public class ProjectMembersController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public ProjectMembersController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- 1. GET: api/project-members/{projectId} (ดึงลูกทีมทั้งหมดในโปรเจกต์) ---
    [HttpGet("{projectId}")]
    public async Task<ActionResult<IEnumerable<ProjectMemberReadModel>>> GetProjectMembers(int projectId)
    {
        var members = new List<ProjectMemberReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // (JOIN 3 ตาราง: ProjectMembers -> Employees -> JobTitles)
            string sql = @"
                SELECT 
                    pm.ProjectMemberId,
                    e.EmployeeId,
                    e.FirstName,
                    e.LastName,
                    e.EmployeeNumber,
                    j.TitleName
                FROM ProjectMembers pm
                JOIN Employees e ON pm.EmployeeId = e.EmployeeId
                LEFT JOIN JobTitles j ON e.JobTitleId = j.JobTitleId
                WHERE pm.ProjectId = @ProjectId
                ORDER BY e.FirstName;
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@ProjectId", projectId);
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            members.Add(new ProjectMemberReadModel
                            {
                                ProjectMemberId = reader.GetInt32(0),
                                EmployeeId = reader.GetInt32(1),
                                FullName = $"{reader.GetString(2)} {reader.GetString(3)}",
                                EmployeeNumber = reader.GetString(4),
                                JobTitle = reader.IsDBNull(5) ? null : reader.GetString(5)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(members);
    }

    // --- 2. POST: api/project-members (เพิ่มคน) ---
    [HttpPost]
    public async Task<ActionResult<ProjectMemberReadModel>> AddProjectMember([FromBody] ProjectMemberWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int newProjectMemberId;
        ProjectMemberReadModel? newMember = null;

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            // (1. INSERT)
            string sql = "INSERT INTO ProjectMembers (ProjectId, EmployeeId) OUTPUT INSERTED.ProjectMemberId VALUES (@ProjectId, @EmployeeId)";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@ProjectId", model.ProjectId);
                command.Parameters.AddWithValue("@EmployeeId", model.EmployeeId);
                try
                {
                    newProjectMemberId = (int)await command.ExecuteScalarAsync();
                }
                catch (SqlException ex)
                {
                    if (ex.Number == 2627 || ex.Number == 2601) // (Unique constraint violation)
                    {
                        return Conflict(new { message = "This employee is already in this project." });
                    }
                    Console.WriteLine(ex.Message);
                    return StatusCode(500, "Database error.");
                }
            }

            // (2. ดึงข้อมูลที่เพิ่งสร้าง (รวมชื่อ) กลับไปให้ FE)
            string readSql = @"
                SELECT pm.ProjectMemberId, e.EmployeeId, e.FirstName, e.LastName, e.EmployeeNumber, j.TitleName
                FROM ProjectMembers pm
                JOIN Employees e ON pm.EmployeeId = e.EmployeeId
                LEFT JOIN JobTitles j ON e.JobTitleId = j.JobTitleId
                WHERE pm.ProjectMemberId = @Id;
            ";
            using (SqlCommand readCmd = new SqlCommand(readSql, connection))
            {
                readCmd.Parameters.AddWithValue("@Id", newProjectMemberId);
                using (SqlDataReader reader = await readCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        newMember = new ProjectMemberReadModel
                        {
                            ProjectMemberId = reader.GetInt32(0),
                            EmployeeId = reader.GetInt32(1),
                            FullName = $"{reader.GetString(2)} {reader.GetString(3)}",
                            EmployeeNumber = reader.GetString(4),
                            JobTitle = reader.IsDBNull(5) ? null : reader.GetString(5)
                        };
                    }
                }
            }
        }

        if (newMember == null) return StatusCode(500, "Failed to retrieve new member.");

        return StatusCode(201, newMember);
    }

    // --- 3. DELETE: api/project-members/{id} (ลบคน) ---
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProjectMember(int id)
    {
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "DELETE FROM ProjectMembers WHERE ProjectMemberId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = "Member not found." });
                    }
                }
                catch (SqlException ex)
                {
                    // (กัน Error ถ้าคนนี้ยังมี "งาน" (Task) ค้างอยู่)
                    if (ex.Number == 547)
                    {
                        return Conflict(new { message = "Cannot remove member: This member still has tasks assigned in this project." });
                    }
                    Console.WriteLine(ex.Message);
                    return StatusCode(500, "Database error.");
                }
            }
        }
        return NoContent();
    }
}