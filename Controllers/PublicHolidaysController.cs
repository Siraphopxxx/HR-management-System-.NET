using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicHolidaysController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public PublicHolidaysController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- 1. GET: api/publicholidays (ดึงทั้งหมด) ---
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PublicHolidayReadModel>>> GetPublicHolidays()
    {
        var holidays = new List<PublicHolidayReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // (เรียงตามวันที่ล่าสุด)
            string sql = "SELECT HolidayId, HolidayDate, HolidayName FROM PublicHolidays ORDER BY HolidayDate DESC";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            holidays.Add(new PublicHolidayReadModel
                            {
                                HolidayId = reader.GetInt32(0),
                                HolidayDate = reader.GetDateTime(1),
                                HolidayName = reader.GetString(2)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(holidays);
    }

    // --- 3. POST: api/publicholidays (สร้างใหม่) ---
    [HttpPost]
    public async Task<ActionResult<PublicHolidayReadModel>> CreatePublicHoliday([FromBody] PublicHolidayWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int newHolidayId;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "INSERT INTO PublicHolidays (HolidayDate, HolidayName) OUTPUT INSERTED.HolidayId VALUES (@Date, @Name)";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Date", model.HolidayDate.Date); // (เก็บเฉพาะ Date)
                command.Parameters.AddWithValue("@Name", model.HolidayName);
                try
                {
                    await connection.OpenAsync();
                    newHolidayId = (int)await command.ExecuteScalarAsync();
                }
                catch (SqlException ex)
                {
                    if (ex.Number == 2627) // UNIQUE constraint violation
                    {
                        return Conflict(new { message = $"The date '{model.HolidayDate.ToShortDateString()}' is already added as a holiday." });
                    }
                    Console.WriteLine(ex.Message); return StatusCode(500, "Database error.");
                }
            }
        }

        var newHoliday = new PublicHolidayReadModel
        {
            HolidayId = newHolidayId,
            HolidayDate = model.HolidayDate.Date,
            HolidayName = model.HolidayName
        };

        return StatusCode(201, newHoliday);
    }

    // --- 4. PUT: api/publicholidays/{id} (อัปเดต) ---
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePublicHoliday(int id, [FromBody] PublicHolidayWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "UPDATE PublicHolidays SET HolidayDate = @Date, HolidayName = @Name WHERE HolidayId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@Date", model.HolidayDate.Date);
                command.Parameters.AddWithValue("@Name", model.HolidayName);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Holiday ID {id} not found." });
                    }
                }
                catch (SqlException ex)
                {
                    if (ex.Number == 2627) // UNIQUE constraint violation
                    {
                        return Conflict(new { message = $"The date '{model.HolidayDate.ToShortDateString()}' already exists for another entry." });
                    }
                    Console.WriteLine(ex.Message); return StatusCode(500, "Database error.");
                }
            }
        }
        return NoContent(); // 204 No Content
    }

    // --- 5. DELETE: api/publicholidays/{id} (ลบ) ---
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePublicHoliday(int id)
    {
        // (ตารางนี้ไม่มี Foreign Key เช็ก สามารถลบได้เลย)
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "DELETE FROM PublicHolidays WHERE HolidayId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Holiday ID {id} not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent(); // 204 No Content
    }
}