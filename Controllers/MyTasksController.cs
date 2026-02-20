using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/my-tasks")]
public class MyTasksController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public MyTasksController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- 1. GET: api/my-tasks/{employeeId} (ดึงงานทั้งหมด "ของฉัน") ---
    [HttpGet("{employeeId}")]
    public async Task<ActionResult<IEnumerable<MyTaskReadModel>>> GetMyTasks(int employeeId)
    {
        var tasks = new List<MyTaskReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // (JOIN 2 ตาราง: ProjectTasks -> Projects เพื่อเอา "ชื่อ" โปรเจกต์)
            string sql = @"
                SELECT 
                    t.TaskId, t.TaskName, t.Status, t.DueDate,
                    p.ProjectId, p.ProjectName
                FROM ProjectTasks t
                JOIN Projects p ON t.ProjectId = p.ProjectId
                WHERE t.AssignedToEmployeeId = @EmployeeId
                ORDER BY t.DueDate ASC;
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@EmployeeId", employeeId);
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tasks.Add(new MyTaskReadModel
                            {
                                TaskId = reader.GetInt32(0),
                                TaskName = reader.GetString(1),
                                Status = reader.GetString(2),
                                DueDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                                ProjectId = reader.GetInt32(4),
                                ProjectName = reader.GetString(5)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(tasks);
    }

    // --- 2. PUT: api/my-tasks/{taskId}/update-status/{employeeId} (อัปเดตสถานะ) ---
    [HttpPut("{taskId}/update-status/{employeeId}")]
    public async Task<IActionResult> UpdateMyTaskStatus(int taskId, int employeeId, [FromBody] UpdateTaskStatusModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // (ตรวจสอบ Status ที่ส่งมา)
        string newStatus = model.NewStatus;
        if (newStatus != "To Do" && newStatus != "In Progress" && newStatus != "Done")
        {
            return BadRequest(new { message = "Invalid status value." });
        }

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // (*** Security Check: เราจะ UPDATE "เฉพาะ" งานที่ taskId ตรง และ "เจ้าของ" (employeeId) ตรงกัน ***)
            string sql = @"
                UPDATE ProjectTasks 
                SET Status = @NewStatus 
                WHERE TaskId = @TaskId AND AssignedToEmployeeId = @EmployeeId;
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@NewStatus", newStatus);
                command.Parameters.AddWithValue("@TaskId", taskId);
                command.Parameters.AddWithValue("@EmployeeId", employeeId);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    // (ถ้า rowsAffected = 0 ➔ แปลว่า "ไม่เจองาน" หรือ "ไม่ใช่งานของคุณ")
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = "Task not found or you do not have permission to update it." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(new { message = $"Task {taskId} status updated to {newStatus}." });
    }
}