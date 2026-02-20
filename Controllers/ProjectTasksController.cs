using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/project-tasks")]
public class ProjectTasksController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public ProjectTasksController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- 1. GET: (เหมือนเดิม) ---
    [HttpGet("{projectId}")]
    public async Task<ActionResult<IEnumerable<ProjectTaskReadModel>>> GetProjectTasks(int projectId)
    {
        // ... (โค้ด GetProjectTasks เหมือนเดิม) ...
        var tasks = new List<ProjectTaskReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                SELECT 
                    t.TaskId, t.ProjectId, t.TaskName, t.Status, t.DueDate,
                    t.AssignedToEmployeeId,
                    ISNULL(e.FirstName + ' ' + e.LastName, 'N/A') AS AssignedToEmployeeName
                FROM ProjectTasks t
                LEFT JOIN Employees e ON t.AssignedToEmployeeId = e.EmployeeId
                WHERE t.ProjectId = @ProjectId
                ORDER BY t.DueDate ASC;
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
                            tasks.Add(new ProjectTaskReadModel
                            {
                                TaskId = reader.GetInt32(0),
                                ProjectId = reader.GetInt32(1),
                                TaskName = reader.GetString(2),
                                Status = reader.GetString(3),
                                DueDate = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                                AssignedToEmployeeId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                                AssignedToEmployeeName = reader.GetString(6)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(tasks);
    }

    // --- 2. POST: (*** อัปเกรด: บังคับ 'To Do' ***) ---
    [HttpPost]
    public async Task<ActionResult<ProjectTaskReadModel>> CreateTask([FromBody] ProjectTaskWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int newTaskId;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                INSERT INTO ProjectTasks (ProjectId, TaskName, TaskDescription, Status, DueDate, AssignedToEmployeeId)
                OUTPUT INSERTED.TaskId
                VALUES (@ProjectId, @TaskName, @TaskDescription, @Status, @DueDate, @AssignedToEmployeeId);
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@ProjectId", model.ProjectId);
                command.Parameters.AddWithValue("@TaskName", model.TaskName);
                command.Parameters.AddWithValue("@TaskDescription", (object?)model.TaskDescription ?? DBNull.Value);

                // (*** นี่คือจุดที่แก้ไข: เรา "บังคับ" 'To Do' ***)
                command.Parameters.AddWithValue("@Status", "To Do");

                command.Parameters.AddWithValue("@DueDate", (object?)model.DueDate ?? DBNull.Value);
                command.Parameters.AddWithValue("@AssignedToEmployeeId", (object?)model.AssignedToEmployeeId ?? DBNull.Value);
                try
                {
                    await connection.OpenAsync();
                    newTaskId = (int)await command.ExecuteScalarAsync();
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }

        return StatusCode(201, new { taskId = newTaskId, message = "Task created (Status: To Do)." });
    }

    // --- 3. DELETE: (เหมือนเดิม) ---
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        // ... (โค้ด DeleteTask เหมือนเดิม) ...
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "DELETE FROM ProjectTasks WHERE TaskId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = "Task not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent();
    }
}