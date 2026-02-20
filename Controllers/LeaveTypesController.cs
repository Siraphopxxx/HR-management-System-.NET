using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaveTypesController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public LeaveTypesController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- 1. GET: api/leavetypes (ดึงทั้งหมด) ---
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeaveTypeReadModel>>> GetLeaveTypes()
    {
        var leaveTypes = new List<LeaveTypeReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "SELECT LeaveTypeId, LeaveTypeName, Description, IsPaid FROM LeaveTypes ORDER BY LeaveTypeName";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            leaveTypes.Add(new LeaveTypeReadModel
                            {
                                LeaveTypeId = reader.GetInt32(0),
                                LeaveTypeName = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                IsPaid = reader.GetBoolean(3)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(leaveTypes);
    }

    // --- 2. GET: api/leavetypes/{id} (ดึงอันเดียว) ---
    [HttpGet("{id}")]
    public async Task<ActionResult<LeaveTypeReadModel>> GetLeaveType(int id)
    {
        LeaveTypeReadModel? leaveType = null;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "SELECT LeaveTypeId, LeaveTypeName, Description, IsPaid FROM LeaveTypes WHERE LeaveTypeId = @Id";
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
                            leaveType = new LeaveTypeReadModel
                            {
                                LeaveTypeId = reader.GetInt32(0),
                                LeaveTypeName = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                IsPaid = reader.GetBoolean(3)
                            };
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        if (leaveType == null) return NotFound();
        return Ok(leaveType);
    }

    // --- 3. POST: api/leavetypes (สร้างใหม่) ---
    [HttpPost]
    public async Task<ActionResult<LeaveTypeReadModel>> CreateLeaveType([FromBody] LeaveTypeWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int newLeaveTypeId;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "INSERT INTO LeaveTypes (LeaveTypeName, Description, IsPaid) OUTPUT INSERTED.LeaveTypeId VALUES (@Name, @Desc, @IsPaid)";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Name", model.LeaveTypeName);
                command.Parameters.AddWithValue("@Desc", (object?)model.Description ?? DBNull.Value);
                command.Parameters.AddWithValue("@IsPaid", model.IsPaid);
                try
                {
                    await connection.OpenAsync();
                    newLeaveTypeId = (int)await command.ExecuteScalarAsync();
                }
                catch (SqlException ex)
                {
                    if (ex.Number == 2627) // UNIQUE constraint violation
                    {
                        return Conflict(new { message = $"Leave Type Name '{model.LeaveTypeName}' already exists." });
                    }
                    Console.WriteLine(ex.Message); return StatusCode(500, "Database error.");
                }
            }
        }

        var newLeaveType = new LeaveTypeReadModel
        {
            LeaveTypeId = newLeaveTypeId,
            LeaveTypeName = model.LeaveTypeName,
            Description = model.Description,
            IsPaid = model.IsPaid
        };

        return CreatedAtAction(nameof(GetLeaveType), new { id = newLeaveTypeId }, newLeaveType);
    }

    // --- 4. PUT: api/leavetypes/{id} (อัปเดต) ---
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLeaveType(int id, [FromBody] LeaveTypeWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "UPDATE LeaveTypes SET LeaveTypeName = @Name, Description = @Desc, IsPaid = @IsPaid WHERE LeaveTypeId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@Name", model.LeaveTypeName);
                command.Parameters.AddWithValue("@Desc", (object?)model.Description ?? DBNull.Value);
                command.Parameters.AddWithValue("@IsPaid", model.IsPaid);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Leave Type ID {id} not found." });
                    }
                }
                catch (SqlException ex)
                {
                    if (ex.Number == 2627) // UNIQUE constraint violation
                    {
                        return Conflict(new { message = $"Leave Type Name '{model.LeaveTypeName}' already exists for another entry." });
                    }
                    Console.WriteLine(ex.Message); return StatusCode(500, "Database error.");
                }
            }
        }
        return NoContent(); // 204 No Content
    }

    // --- 5. DELETE: api/leavetypes/{id} (ลบ) ---
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLeaveType(int id)
    {
        // กฎ: เช็กก่อนว่ามี "ใบลา" (LeaveRequests) ใช้อันนี้อยู่หรือไม่
        int requestCount = 0;
        using (SqlConnection checkConn = new SqlConnection(_connectionString))
        {
            string checkSql = "SELECT COUNT(*) FROM LeaveRequests WHERE LeaveTypeId = @Id";
            using (SqlCommand checkCmd = new SqlCommand(checkSql, checkConn))
            {
                checkCmd.Parameters.AddWithValue("@Id", id);
                try
                {
                    await checkConn.OpenAsync();
                    requestCount = (int)await checkCmd.ExecuteScalarAsync();
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }

        if (requestCount > 0)
        {
            return Conflict(new { message = $"Cannot delete: {requestCount} leave request(s) are using this type." });
        }

        // ถ้าไม่มี ก็ลบได้
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "DELETE FROM LeaveTypes WHERE LeaveTypeId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Leave Type ID {id} not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent(); // 204 No Content
    }
}