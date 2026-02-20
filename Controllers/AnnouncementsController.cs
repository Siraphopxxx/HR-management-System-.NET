using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnnouncementsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public AnnouncementsController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- 1. GET: api/announcements (ดึงทั้งหมด *ใหม่สุดก่อน*) ---
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AnnouncementReadModel>>> GetAnnouncements()
    {
        var announcements = new List<AnnouncementReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // (ORDER BY PostedDate DESC = เอาอันใหม่สุดขึ้นก่อน)
            string sql = "SELECT AnnouncementId, Title, Content, PostedDate FROM Announcements ORDER BY PostedDate DESC";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            announcements.Add(new AnnouncementReadModel
                            {
                                AnnouncementId = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                Content = reader.GetString(2),
                                PostedDate = reader.GetDateTime(3)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(announcements);
    }

    // --- 2. GET: api/announcements/{id} (ดึงอันเดียว) ---
    [HttpGet("{id}")]
    public async Task<ActionResult<AnnouncementReadModel>> GetAnnouncement(int id)
    {
        AnnouncementReadModel? announcement = null;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "SELECT AnnouncementId, Title, Content, PostedDate FROM Announcements WHERE AnnouncementId = @Id";
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
                            announcement = new AnnouncementReadModel
                            {
                                AnnouncementId = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                Content = reader.GetString(2),
                                PostedDate = reader.GetDateTime(3)
                            };
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        if (announcement == null) return NotFound();
        return Ok(announcement);
    }

    // --- 3. POST: api/announcements (สร้างใหม่) ---
    [HttpPost]
    public async Task<ActionResult<AnnouncementReadModel>> CreateAnnouncement([FromBody] AnnouncementWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int newId;
        DateTime newPostedDate;

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // (เราจะ OUTPUT ทั้ง ID และ PostedDate ที่ DB สร้าง)
            string sql = "INSERT INTO Announcements (Title, Content) OUTPUT INSERTED.AnnouncementId, INSERTED.PostedDate VALUES (@Title, @Content)";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Title", model.Title);
                command.Parameters.AddWithValue("@Content", model.Content);
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            newId = reader.GetInt32(0);
                            newPostedDate = reader.GetDateTime(1);
                        }
                        else
                        {
                            return StatusCode(500, "Failed to create announcement.");
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }

        var newAnnouncement = new AnnouncementReadModel
        {
            AnnouncementId = newId,
            Title = model.Title,
            Content = model.Content,
            PostedDate = newPostedDate
        };

        return CreatedAtAction(nameof(GetAnnouncement), new { id = newId }, newAnnouncement);
    }

    // --- 4. PUT: api/announcements/{id} (อัปเดต) ---
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAnnouncement(int id, [FromBody] AnnouncementWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "UPDATE Announcements SET Title = @Title, Content = @Content WHERE AnnouncementId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@Title", model.Title);
                command.Parameters.AddWithValue("@Content", model.Content);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Announcement ID {id} not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent(); // 204 No Content
    }

    // --- 5. DELETE: api/announcements/{id} (ลบ) ---
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAnnouncement(int id)
    {
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "DELETE FROM Announcements WHERE AnnouncementId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Announcement ID {id} not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent(); // 204 No Content
    }
}