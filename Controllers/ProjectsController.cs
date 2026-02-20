using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.ComponentModel.DataAnnotations; // (*** เพิ่มอันนี้ ***)

namespace be.Controllers;

// (*** ใหม่: Model สำหรับรับ Status ***)
public class UpdateStatusModel
{
    [Required]
    public required string Status { get; set; }
}

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public ProjectsController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- 1. GET: (เหมือนเดิม - Admin/HR ใช้) ---
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectReadModel>>> GetProjects()
    {
        // ... (โค้ด GetProjects (ดึงทั้งหมด) เหมือนเดิม) ...
        var projects = new List<ProjectReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "SELECT ProjectId, ProjectName, ProjectDescription, StartDate, EndDate, Status, ProjectManagerEmployeeId FROM Projects ORDER BY StartDate DESC";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            projects.Add(new ProjectReadModel
                            {
                                ProjectId = reader.GetInt32(0),
                                ProjectName = reader.GetString(1),
                                ProjectDescription = reader.IsDBNull(2) ? null : reader.GetString(2),
                                StartDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                                EndDate = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                                Status = reader.GetString(5),
                                ProjectManagerEmployeeId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(projects);
    }

    // --- 2. GET (by ID) (เหมือนเดิม) ---
    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectReadModel>> GetProject(int id)
    {
        // ... (โค้ด GetProject (ดึง 1 อัน) เหมือนเดิม) ...
        ProjectReadModel? project = null;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "SELECT ProjectId, ProjectName, ProjectDescription, StartDate, EndDate, Status, ProjectManagerEmployeeId FROM Projects WHERE ProjectId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            project = new ProjectReadModel
                            {
                                ProjectId = reader.GetInt32(0),
                                ProjectName = reader.GetString(1),
                                ProjectDescription = reader.IsDBNull(2) ? null : reader.GetString(2),
                                StartDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                                EndDate = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                                Status = reader.GetString(5),
                                ProjectManagerEmployeeId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6)
                            };
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        if (project == null) return NotFound();
        return Ok(project);
    }

    // --- 3. GET (managed-by) (เหมือนเดิม) ---
    [HttpGet("managed-by/{pmEmployeeId}")]
    public async Task<ActionResult<IEnumerable<ProjectReadModel>>> GetManagedProjects(int pmEmployeeId)
    {
        // ... (โค้ด GetManagedProjects (ของ PM) เหมือนเดิม) ...
        var projects = new List<ProjectReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                SELECT ProjectId, ProjectName, ProjectDescription, StartDate, EndDate, Status, ProjectManagerEmployeeId 
                FROM Projects 
                WHERE ProjectManagerEmployeeId = @pmEmployeeId
                ORDER BY StartDate DESC;
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@pmEmployeeId", pmEmployeeId);
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            projects.Add(new ProjectReadModel
                            {
                                ProjectId = reader.GetInt32(0),
                                ProjectName = reader.GetString(1),
                                ProjectDescription = reader.IsDBNull(2) ? null : reader.GetString(2),
                                StartDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                                EndDate = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                                Status = reader.GetString(5),
                                ProjectManagerEmployeeId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(projects);
    }

    // (*** 4. (ใหม่!) PUT: api/projects/{id}/update-status ***)
    // (API สำหรับ "ปุ่มกดเดียว" ของ PM)
    [HttpPut("{id}/update-status")]
    public async Task<IActionResult> UpdateProjectStatus(int id, [FromBody] UpdateStatusModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // (เช็กว่า Status ที่ส่งมา ถูกต้อง)
        if (model.Status != "Active" && model.Status != "On Hold" && model.Status != "Completed")
        {
            return BadRequest(new { message = "Invalid status value." });
        }

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "UPDATE Projects SET Status = @Status WHERE ProjectId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@Status", model.Status);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Project ID {id} not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(new { message = $"Project {id} status updated to {model.Status}." });
    }


    // --- 5. POST: (เหมือนเดิม - Admin/HR ใช้) ---
    [HttpPost]
    public async Task<ActionResult<ProjectReadModel>> CreateProject([FromBody] ProjectWriteModel model)
    {
        // ... (โค้ด CreateProject (ที่รับ PM ID) เหมือนเดิม) ...
        if (!ModelState.IsValid) return BadRequest(ModelState);
        int newId;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                INSERT INTO Projects (ProjectName, ProjectDescription, StartDate, EndDate, Status, ProjectManagerEmployeeId) 
                OUTPUT INSERTED.ProjectId
                VALUES (@ProjectName, @ProjectDescription, @StartDate, @EndDate, @Status, @ProjectManagerEmployeeId);
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@ProjectName", model.ProjectName);
                command.Parameters.AddWithValue("@ProjectDescription", (object?)model.ProjectDescription ?? DBNull.Value);
                command.Parameters.AddWithValue("@StartDate", (object?)model.StartDate ?? DBNull.Value);
                command.Parameters.AddWithValue("@EndDate", (object?)model.EndDate ?? DBNull.Value);
                command.Parameters.AddWithValue("@Status", model.Status);
                command.Parameters.AddWithValue("@ProjectManagerEmployeeId", (object?)model.ProjectManagerEmployeeId ?? DBNull.Value);
                try
                {
                    await connection.OpenAsync();
                    newId = (int)await command.ExecuteScalarAsync();
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        var newProject = new ProjectReadModel { ProjectId = newId, ProjectName = model.ProjectName, ProjectDescription = model.ProjectDescription, StartDate = model.StartDate, EndDate = model.EndDate, Status = model.Status, ProjectManagerEmployeeId = model.ProjectManagerEmployeeId };
        return CreatedAtAction(nameof(GetProject), new { id = newId }, newProject);
    }

    // --- 6. PUT: (เหมือนเดิม - Admin/HR ใช้) ---
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProject(int id, [FromBody] ProjectWriteModel model)
    {
        // ... (โค้ด UpdateProject (ที่รับ PM ID) เหมือนเดิม) ...
        if (!ModelState.IsValid) return BadRequest(ModelState);
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                UPDATE Projects SET 
                    ProjectName = @ProjectName, ProjectDescription = @ProjectDescription, 
                    StartDate = @StartDate, EndDate = @EndDate, Status = @Status,
                    ProjectManagerEmployeeId = @ProjectManagerEmployeeId 
                WHERE ProjectId = @Id;
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@ProjectName", model.ProjectName);
                command.Parameters.AddWithValue("@ProjectDescription", (object?)model.ProjectDescription ?? DBNull.Value);
                command.Parameters.AddWithValue("@StartDate", (object?)model.StartDate ?? DBNull.Value);
                command.Parameters.AddWithValue("@EndDate", (object?)model.EndDate ?? DBNull.Value);
                command.Parameters.AddWithValue("@Status", model.Status);
                command.Parameters.AddWithValue("@ProjectManagerEmployeeId", (object?)model.ProjectManagerEmployeeId ?? DBNull.Value);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Project ID {id} not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent();
    }

    // --- 7. DELETE: (เหมือนเดิม - Admin/HR ใช้) ---
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        // ... (โค้ด DeleteProject เหมือนเดิม) ...
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "DELETE FROM Projects WHERE ProjectId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Project ID {id} not found." });
                    }
                }
                catch (SqlException ex)
                {
                    if (ex.Number == 547) { return Conflict(new { message = "Cannot delete project: This project still has members or tasks assigned. Please remove them first." }); }
                    Console.WriteLine(ex.Message);
                    return StatusCode(500, "Database error.");
                }
            }
        }
        return NoContent();
    }
}