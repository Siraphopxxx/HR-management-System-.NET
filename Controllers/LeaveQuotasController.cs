using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaveQuotasController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public LeaveQuotasController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- 1. GET: api/leavequotas (ดึงทั้งหมด) ---
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeaveQuotaReadModel>>> GetLeaveQuotas()
    {
        var quotas = new List<LeaveQuotaReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // *** SQL JOIN กับตาราง LeaveTypes เพื่อเอาชื่อมาแสดง ***
            string sql = @"
                SELECT 
                    q.LeaveQuotaId, 
                    q.LeaveTypeId, 
                    t.LeaveTypeName, 
                    q.DefaultDays
                FROM LeaveQuotas q
                JOIN LeaveTypes t ON q.LeaveTypeId = t.LeaveTypeId
                ORDER BY t.LeaveTypeName;
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            quotas.Add(new LeaveQuotaReadModel
                            {
                                LeaveQuotaId = reader.GetInt32(0),
                                LeaveTypeId = reader.GetInt32(1),
                                LeaveTypeName = reader.GetString(2),
                                DefaultDays = reader.GetInt32(3)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(quotas);
    }

    // --- 2. (เราข้าม GET by ID ไปก่อน เพื่อง่าย) ---

    // --- 3. POST: api/leavequotas (สร้างใหม่) ---
    [HttpPost]
    public async Task<ActionResult<LeaveQuotaReadModel>> CreateLeaveQuota([FromBody] LeaveQuotaWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int newQuotaId;
        string leaveTypeName = ""; // (เราจะใช้สำหรับส่งค่ากลับ)

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            try
            {
                // 1. INSERT โควตา
                string sql = "INSERT INTO LeaveQuotas (LeaveTypeId, DefaultDays) OUTPUT INSERTED.LeaveQuotaId VALUES (@LeaveTypeId, @DefaultDays)";
                using (SqlCommand command = new SqlCommand(sql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@LeaveTypeId", model.LeaveTypeId);
                    command.Parameters.AddWithValue("@DefaultDays", model.DefaultDays);
                    newQuotaId = (int)await command.ExecuteScalarAsync();
                }

                // 2. ดึงชื่อ LeaveType กลับมา (เพื่อส่งกลับไป FE)
                string getNameSql = "SELECT LeaveTypeName FROM LeaveTypes WHERE LeaveTypeId = @LeaveTypeId";
                using (SqlCommand getNameCmd = new SqlCommand(getNameSql, connection, transaction))
                {
                    getNameCmd.Parameters.AddWithValue("@LeaveTypeId", model.LeaveTypeId);
                    leaveTypeName = (string)await getNameCmd.ExecuteScalarAsync();
                }

                await transaction.CommitAsync();
            }
            catch (SqlException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Number == 2627) // UNIQUE constraint violation
                {
                    return Conflict(new { message = "This Leave Type already has a quota assigned. Please edit the existing one." });
                }
                Console.WriteLine(ex.Message); return StatusCode(500, "Database error.");
            }
        }

        var newQuota = new LeaveQuotaReadModel
        {
            LeaveQuotaId = newQuotaId,
            LeaveTypeId = model.LeaveTypeId,
            LeaveTypeName = leaveTypeName,
            DefaultDays = model.DefaultDays
        };

        // (เราไม่มี GetById เลยส่ง 201 Created ธรรมดา)
        return StatusCode(201, newQuota);
    }

    // --- 4. PUT: api/leavequotas/{id} (อัปเดต) ---
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLeaveQuota(int id, [FromBody] LeaveQuotaWriteModel model)
    {
        // (หมายเหตุ: เราใช้ LeaveQuotaId (id) เพื่ออัปเดต แต่ใช้ LeaveTypeId จาก model)
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // (เราอัปเดตแค่จำนวนวัน ส่วน LeaveTypeId ไม่ควรเปลี่ยน)
            string sql = "UPDATE LeaveQuotas SET DefaultDays = @DefaultDays WHERE LeaveQuotaId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@DefaultDays", model.DefaultDays);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Quota ID {id} not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent(); // 204 No Content
    }

    // --- 5. DELETE: api/leavequotas/{id} (ลบ) ---
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLeaveQuota(int id)
    {
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "DELETE FROM LeaveQuotas WHERE LeaveQuotaId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Quota ID {id} not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent(); // 204 No Content
    }
}