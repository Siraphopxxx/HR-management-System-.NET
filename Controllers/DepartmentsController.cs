using Microsoft.AspNetCore.Mvc;
using be.Models; // << ใช้ Model ที่เพิ่งสร้าง
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers; // << ตรวจสอบ namespace

[ApiController]
[Route("api/[controller]")]
// [Authorize(Roles = "Admin")] // << ถ้ากลับไปใช้ระบบ Token ค่อยเปิด
public class DepartmentsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public DepartmentsController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                           ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // --- 1. GET: api/departments (ดึงข้อมูลแผนกทั้งหมด) ---
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DepartmentReadModel>>> GetDepartments()
    {
        var departments = new List<DepartmentReadModel>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "SELECT DepartmentId, DepartmentName FROM Departments ORDER BY DepartmentName";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            departments.Add(new DepartmentReadModel
                            {
                                DepartmentId = reader.GetInt32(0),
                                DepartmentName = reader.GetString(1)
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return Ok(departments);
    }

    // --- 2. GET: api/departments/{id} (ดึงข้อมูลแผนกเดียว) ---
    [HttpGet("{id}")]
    public async Task<ActionResult<DepartmentReadModel>> GetDepartment(int id)
    {
        DepartmentReadModel? department = null;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "SELECT DepartmentId, DepartmentName FROM Departments WHERE DepartmentId = @Id";
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
                            department = new DepartmentReadModel
                            {
                                DepartmentId = reader.GetInt32(0),
                                DepartmentName = reader.GetString(1)
                            };
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        if (department == null) return NotFound();
        return Ok(department);
    }

    // --- 3. POST: api/departments (สร้างแผนกใหม่) ---
    [HttpPost]
    public async Task<ActionResult<DepartmentReadModel>> CreateDepartment([FromBody] DepartmentWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // (Optional: เช็คชื่อซ้ำ)
        // ... (โค้ดเช็คชื่อซ้ำ) ...

        int newDepartmentId;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            // ใช้ OUTPUT INSERTED.DepartmentId เพื่อดึง ID ที่เพิ่งสร้าง
            string sql = "INSERT INTO Departments (DepartmentName) OUTPUT INSERTED.DepartmentId VALUES (@Name)";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Name", model.DepartmentName);
                try
                {
                    await connection.OpenAsync();
                    // ใช้ ExecuteScalarAsync เพื่อดึงค่า ID ที่ OUTPUT ออกมา
                    newDepartmentId = (int)await command.ExecuteScalarAsync();
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }

        // สร้าง Object ที่จะส่งกลับ
        var newDepartment = new DepartmentReadModel
        {
            DepartmentId = newDepartmentId,
            DepartmentName = model.DepartmentName
        };

        // ส่ง 201 Created พร้อมข้อมูลใหม่
        return CreatedAtAction(nameof(GetDepartment), new { id = newDepartmentId }, newDepartment);
    }

    // --- 4. PUT: api/departments/{id} (อัปเดตชื่อแผนก) ---
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDepartment(int id, [FromBody] DepartmentWriteModel model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // (Optional: เช็คชื่อซ้ำกับแผนกอื่น)
        // ...

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "UPDATE Departments SET DepartmentName = @Name WHERE DepartmentId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@Name", model.DepartmentName);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Department ID {id} not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent(); // 204 No Content - มาตรฐานสำหรับ Update สำเร็จ
    }

    // --- 5. DELETE: api/departments/{id} (ลบแผนก) ---
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        // *** กฎสำคัญ: เช็กก่อนว่ามีพนักงานใช้แผนกนี้อยู่หรือไม่ ***
        int employeeCount = 0;
        using (SqlConnection checkConn = new SqlConnection(_connectionString))
        {
            string checkSql = "SELECT COUNT(*) FROM Employees WHERE DepartmentId = @Id";
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

        // ถ้ามีพนักงานใช้ ให้ Reject
        if (employeeCount > 0)
        {
            return BadRequest(new { message = $"Cannot delete department: {employeeCount} employee(s) are still assigned to it." });
        }

        // ถ้าไม่มีพนักงาน ก็ลบได้
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = "DELETE FROM Departments WHERE DepartmentId = @Id";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                try
                {
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = $"Department ID {id} not found." });
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        return NoContent(); // 204 No Content - ลบสำเร็จ
    }
}