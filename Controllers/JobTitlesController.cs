using Microsoft.AspNetCore.Mvc;
using be.Models; // << ใช้ Model ที่เพิ่งสร้าง
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers; // << ตรวจสอบ namespace

[ApiController]
[Route("api/[controller]")]
// [Authorize(Roles = "Admin")] // << ถ้ากลับไปใช้ระบบ Token ค่อยเปิด
public class JobTitlesController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public JobTitlesController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                           ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- 1. GET: api/jobtitles (ดึงข้อมูลตำแหน่งงานทั้งหมด) ---
    [HttpGet]
    public async Task<ActionResult<IEnumerable<JobTitleReadModel>>> GetJobTitles()
    {
        var jobTitles = new List<JobTitleReadModel>();
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
                            jobTitles.Add(new JobTitleReadModel
                            {
                                JobTitleId = reader.GetInt32(0),
                                TitleName = reader.GetString(1)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(jobTitles);
    }

    // --- 2. GET: api/jobtitles/{id} (ดึงข้อมูลตำแหน่งงานเดียว) ---
    [HttpGet("{id}")]
    public async Task<ActionResult<JobTitleReadModel>> GetJobTitle(int id)
    {
        JobTitleReadModel? jobTitle = null;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "SELECT JobTitleId, TitleName FROM JobTitles WHERE JobTitleId = @Id";
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
                            jobTitle = new JobTitleReadModel
                            {
                                JobTitleId = reader.GetInt32(0),
                                TitleName = reader.GetString(1)
                            };
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        if (jobTitle == null) return NotFound();
        return Ok(jobTitle);
    }

    // --- 3. POST: api/jobtitles (สร้างตำแหน่งงานใหม่) ---
    [HttpPost]
    public async Task<ActionResult<JobTitleReadModel>> CreateJobTitle([FromBody] JobTitleWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int newJobTitleId;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "INSERT INTO JobTitles (TitleName) OUTPUT INSERTED.JobTitleId VALUES (@Name)";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Name", model.TitleName);
                try
                {
                    await connection.OpenAsync();
                    newJobTitleId = (int)await command.ExecuteScalarAsync();
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }

        var newJobTitle = new JobTitleReadModel
        {
            JobTitleId = newJobTitleId,
            TitleName = model.TitleName
        };

        return CreatedAtAction(nameof(GetJobTitle), new { id = newJobTitleId }, newJobTitle);
    }

    // --- 4. PUT: api/jobtitles/{id} (อัปเดตชื่อตำแหน่งงาน) ---
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateJobTitle(int id, [FromBody] JobTitleWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "UPDATE JobTitles SET TitleName = @Name WHERE JobTitleId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@Name", model.TitleName);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"JobTitle ID {id} not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent();
    }

    // --- 5. DELETE: api/jobtitles/{id} (ลบตำแหน่งงาน) ---
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJobTitle(int id)
    {
        // *** กฎสำคัญ: เช็กก่อนว่ามีพนักงานใช้ตำแหน่งงานนี้อยู่หรือไม่ ***
        int employeeCount = 0;
        using (SqlConnection checkConn = new SqlConnection(_connectionString))
        {
            string checkSql = "SELECT COUNT(*) FROM Employees WHERE JobTitleId = @Id";
            using (SqlCommand checkCmd = new SqlCommand(checkSql, checkConn))
            {
                checkCmd.Parameters.AddWithValue("@Id", id);
                try
                {
                    await checkConn.OpenAsync();
                    employeeCount = (int)await checkCmd.ExecuteScalarAsync();
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }

        if (employeeCount > 0)
        {
            return BadRequest(new { message = $"Cannot delete job title: {employeeCount} employee(s) are still assigned to it." });
        }

        // ถ้าไม่มีพนักงาน ก็ลบได้
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "DELETE FROM JobTitles WHERE JobTitleId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"JobTitle ID {id} not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent();
    }
}