using Microsoft.AspNetCore.Mvc;
using be.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    // 1. Constructor (ต้องมี)
    public UsersController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // (Model เล็กๆ ไว้เก็บ ID คู่)
    private class ManagerIds
    {
        public int EmployeeId { get; set; }
        public int AppUserId { get; set; }
    }


    // --- (GetManagers: เหมือนเดิม - แก้บั๊ก NULL แล้ว) ---
    [HttpGet("managers")]
    public async Task<ActionResult<IEnumerable<object>>> GetManagers()
    {
        var managers = new List<object>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                SELECT 
                    e.EmployeeId, 
                    e.FirstName, 
                    e.LastName, 
                    e.EmployeeNumber
                FROM Employees e
                JOIN AppUsers u ON e.AppUserId = u.Id
                WHERE u.Role = 'Manager'
                ORDER BY e.FirstName;
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
                            string firstName = reader.IsDBNull(reader.GetOrdinal("FirstName")) ? "" : reader.GetString(reader.GetOrdinal("FirstName"));
                            string lastName = reader.IsDBNull(reader.GetOrdinal("LastName")) ? "" : reader.GetString(reader.GetOrdinal("LastName"));
                            string empNumber = reader.IsDBNull(reader.GetOrdinal("EmployeeNumber")) ? "N/A" : reader.GetString(reader.GetOrdinal("EmployeeNumber"));

                            managers.Add(new
                            {
                                employeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                                fullName = $"{firstName} {lastName}".Trim(),
                                employeeNumber = empNumber
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting managers: {ex.Message}");
                    return StatusCode(500, "Database error.");
                }
            }
        }
        return Ok(managers);
    }

    // --- (GetAllUsers: เหมือนเดิม) ---
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAllUsers()
    {
        var users = new List<object>();
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                SELECT 
                    e.EmployeeId, e.EmployeeNumber, e.FirstName, e.LastName,
                    d.DepartmentName, 
                    j.TitleName AS Position,
                    u.Role
                FROM Employees e
                LEFT JOIN AppUsers u ON e.AppUserId = u.Id
                LEFT JOIN Departments d ON e.DepartmentId = d.DepartmentId
                LEFT JOIN JobTitles j ON e.JobTitleId = j.JobTitleId
                ORDER BY e.FirstName;
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
                            users.Add(new
                            {
                                EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                                EmployeeNumber = reader.GetString(reader.GetOrdinal("EmployeeNumber")),
                                FullName = $"{reader.GetString(reader.GetOrdinal("FirstName"))} {reader.GetString(reader.GetOrdinal("LastName"))}",
                                Department = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? "N/A" : reader.GetString(reader.GetOrdinal("DepartmentName")),
                                Position = reader.IsDBNull(reader.GetOrdinal("Position")) ? "N/A" : reader.GetString(reader.GetOrdinal("Position")),
                                Role = reader.IsDBNull(reader.GetOrdinal("Role")) ? "N/A" : reader.GetString(reader.GetOrdinal("Role"))
                            });
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine($"SQL Error getting all users: {ex.Message}"); return StatusCode(500, "Database error occurred."); }
            }
        }
        return Ok(users);
    }

    // --- (GetUserByEmployeeNumber: เหมือนเดิม) ---
    [HttpGet("{employeeNumber}")]
    public async Task<ActionResult<object>> GetUserByEmployeeNumber(string employeeNumber)
    {
        object? userResult = null;
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            string sql = @"
                SELECT 
                    e.*, -- ดึงข้อมูล Employee ทั้งหมด
                    u.Name AS Username, u.Role, u.Password
                FROM Employees e
                JOIN AppUsers u ON e.AppUserId = u.Id
                WHERE e.EmployeeNumber = @EmployeeNumber;
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@EmployeeNumber", employeeNumber);
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            userResult = new
                            {
                                // ... (ข้อมูล Employees ทั้งหมด) ...
                                EmployeeId = (int)reader["EmployeeId"],
                                EmployeeNumber = (string)reader["EmployeeNumber"],
                                FirstName = (string)reader["FirstName"],
                                LastName = (string)reader["LastName"],
                                DateOfBirth = reader.IsDBNull("DateOfBirth") ? (DateTime?)null : (DateTime?)reader["DateOfBirth"],
                                Gender = reader.IsDBNull("Gender") ? null : (string)reader["Gender"],
                                Email = reader.IsDBNull("Email") ? null : (string)reader["Email"],
                                PhoneNumber = reader.IsDBNull("PhoneNumber") ? null : (string)reader["PhoneNumber"],
                                Address_Street = reader.IsDBNull("Address_Street") ? null : (string)reader["Address_Street"],
                                Address_City = reader.IsDBNull("Address_City") ? null : (string)reader["Address_City"],
                                Address_State = reader.IsDBNull("Address_State") ? null : (string)reader["Address_State"],
                                Address_PostalCode = reader.IsDBNull("Address_PostalCode") ? null : (string)reader["Address_PostalCode"],
                                Address_Country = reader.IsDBNull("Address_Country") ? null : (string)reader["Address_Country"],
                                CurrentSalary = reader.IsDBNull("CurrentSalary") ? (decimal?)null : (decimal?)reader["CurrentSalary"],
                                DepartmentId = reader.IsDBNull("DepartmentId") ? (int?)null : (int)reader["DepartmentId"],
                                JobTitleId = reader.IsDBNull("JobTitleId") ? (int?)null : (int)reader["JobTitleId"],
                                ManagerId = reader.IsDBNull("ManagerId") ? (int?)null : (int)reader["ManagerId"],
                                AppUserId = (int)reader["AppUserId"],
                                // ... (ข้อมูล AppUsers) ...
                                Username = (string)reader["Username"],
                                Role = (string)reader["Role"],
                                Password = (string)reader["Password"]
                            };
                        }
                    }
                }
                catch (SqlException ex) { Console.WriteLine(ex.Message); return StatusCode(500, "Database error."); }
            }
        }
        if (userResult == null) return NotFound();
        return Ok(userResult);
    }


    // --- 4. POST: api/users (Add User) ---
    // (*** แก้ไข: "กฎ C" + บังคับ ManagerId = NULL ***)
    [HttpPost]
    public async Task<ActionResult> AddUser([FromBody] AddUserModel newUser)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            int newAppUserId;
            int newEmployeeId; // (เราต้องการ ID นี้)

            try
            {
                // 1. เช็ก Name (Username) ซ้ำ
                string checkSql = "SELECT COUNT(*) FROM AppUsers WHERE Name = @Name";
                using (SqlCommand checkCmd = new SqlCommand(checkSql, connection, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@Name", newUser.Name);
                    if ((int)await checkCmd.ExecuteScalarAsync() > 0)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = $"Username '{newUser.Name}' already exists." });
                    }
                }

                // 2. เช็ก EmployeeNumber ซ้ำ
                checkSql = "SELECT COUNT(*) FROM Employees WHERE EmployeeNumber = @EmployeeNumber";
                using (SqlCommand checkCmd = new SqlCommand(checkSql, connection, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@EmployeeNumber", newUser.EmployeeNumber);
                    if ((int)await checkCmd.ExecuteScalarAsync() > 0)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = $"Employee Number '{newUser.EmployeeNumber}' already exists." });
                    }
                }

                // 3. เพิ่มข้อมูลใน AppUsers
                string sqlAppUser = "INSERT INTO AppUsers (Name, Password, Role) OUTPUT INSERTED.Id VALUES (@Name, @Password, @Role)";
                using (SqlCommand cmdAppUser = new SqlCommand(sqlAppUser, connection, transaction))
                {
                    cmdAppUser.Parameters.AddWithValue("@Name", newUser.Name);
                    cmdAppUser.Parameters.AddWithValue("@Password", newUser.Password); // (Plain text)
                    cmdAppUser.Parameters.AddWithValue("@Role", newUser.Role);
                    newAppUserId = (int)await cmdAppUser.ExecuteScalarAsync();
                }

                // 4. เพิ่มข้อมูลใน Employees
                string sqlEmployee = @"
                    INSERT INTO Employees (
                        EmployeeNumber, FirstName, LastName, DateOfBirth, Gender, Email, PhoneNumber,
                        Address_Street, Address_City, Address_State, Address_PostalCode, Address_Country,
                        CurrentSalary, DepartmentId, JobTitleId, ManagerId, AppUserId, HireDate
                    ) 
                    OUTPUT INSERTED.EmployeeId -- (ดึง ID พนักงานใหม่)
                    VALUES (
                        @EmployeeNumber, @FirstName, @LastName, @DateOfBirth, @Gender, @Email, @PhoneNumber,
                        @Address_Street, @Address_City, @Address_State, @Address_PostalCode, @Address_Country,
                        @CurrentSalary, @DepartmentId, @JobTitleId, @ManagerId, @AppUserId, GETDATE()
                    )";

                using (SqlCommand cmdEmployee = new SqlCommand(sqlEmployee, connection, transaction))
                {
                    // (เพิ่ม Parameters ... )
                    cmdEmployee.Parameters.AddWithValue("@EmployeeNumber", newUser.EmployeeNumber);
                    cmdEmployee.Parameters.AddWithValue("@FirstName", newUser.FirstName);
                    cmdEmployee.Parameters.AddWithValue("@LastName", newUser.LastName);
                    cmdEmployee.Parameters.AddWithValue("@AppUserId", newAppUserId);
                    cmdEmployee.Parameters.AddWithValue("@DateOfBirth", (object?)newUser.DateOfBirth ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Gender", (object?)newUser.Gender ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Email", (object?)newUser.Email ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@PhoneNumber", (object?)newUser.PhoneNumber ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Address_Street", (object?)newUser.Address_Street ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Address_City", (object?)newUser.Address_City ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Address_State", (object?)newUser.Address_State ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Address_PostalCode", (object?)newUser.Address_PostalCode ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Address_Country", (object?)newUser.Address_Country ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@CurrentSalary", (object?)newUser.CurrentSalary ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@DepartmentId", (object?)newUser.DepartmentId ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@JobTitleId", (object?)newUser.JobTitleId ?? DBNull.Value);

                    // (*** ใหม่: โลจิก "กฎ C" ที่คุณเพิ่งเพิ่ม ***)
                    // ถ้า User คนนี้กำลังจะถูก "สร้าง" เป็น Manager...
                    if (newUser.Role == "Manager")
                    {
                        // ...บังคับให้ ManagerId ของ "ตัวเอง" เป็น NULL (แม้ว่า FE จะส่งมาก็ตาม)
                        cmdEmployee.Parameters.AddWithValue("@ManagerId", DBNull.Value);
                    }
                    else
                    {
                        // ...ถ้าเป็นพนักงานธรรมดา ก็ใช้ค่าที่ส่งมาจากฟอร์ม
                        cmdEmployee.Parameters.AddWithValue("@ManagerId", (object?)newUser.ManagerId ?? DBNull.Value);
                    }
                    // (*** จบส่วนใหม่ ***)

                    newEmployeeId = (int)await cmdEmployee.ExecuteScalarAsync(); // (เก็บ EmployeeId ใหม่)
                }

                // 5. (ใหม่: "กฎ C" - ลดตำแหน่งคนเก่า และ ยึดแผนก)
                if (newUser.Role == "Manager" && newUser.DepartmentId.HasValue)
                {
                    // 5.1. หา AppUserId ของ Manager คนเก่า (ถ้ามี)
                    int? oldManagerAppUserId = null;
                    string findOldMgrSql = @"
                        SELECT u.Id 
                        FROM AppUsers u
                        JOIN Employees e ON u.Id = e.AppUserId
                        WHERE e.DepartmentId = @DepartmentId 
                          AND u.Role = 'Manager' 
                          AND e.EmployeeId != @NewManagerEmployeeId"; // (ต้องไม่ใช่คนที่เราเพิ่งแอด)

                    using (SqlCommand findCmd = new SqlCommand(findOldMgrSql, connection, transaction))
                    {
                        findCmd.Parameters.AddWithValue("@DepartmentId", newUser.DepartmentId.Value);
                        findCmd.Parameters.AddWithValue("@NewManagerEmployeeId", newEmployeeId);
                        var result = await findCmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            oldManagerAppUserId = (int)result;
                        }
                    }

                    // 5.2. ถ้าเจอ... ลดตำแหน่ง (Demote) คนเก่า
                    if (oldManagerAppUserId.HasValue)
                    {
                        string demoteSql = "UPDATE AppUsers SET Role = 'Employee' WHERE Id = @OldManagerAppUserId";
                        using (SqlCommand demoteCmd = new SqlCommand(demoteSql, connection, transaction))
                        {
                            demoteCmd.Parameters.AddWithValue("@OldManagerAppUserId", oldManagerAppUserId.Value);
                            await demoteCmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 5.3. อัปเดตลูกน้องทุกคน (รวมถึงคนเก่า) ให้ชี้มาที่ Manager ใหม่
                    string updateTeamSql = @"
                        UPDATE Employees 
                        SET ManagerId = @NewManagerEmployeeId 
                        WHERE DepartmentId = @DepartmentId 
                          AND EmployeeId != @NewManagerEmployeeId";

                    using (SqlCommand updateCmd = new SqlCommand(updateTeamSql, connection, transaction))
                    {
                        updateCmd.Parameters.AddWithValue("@NewManagerEmployeeId", newEmployeeId);
                        updateCmd.Parameters.AddWithValue("@DepartmentId", newUser.DepartmentId.Value);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
                // (*** จบส่วนใหม่ ***)

                await transaction.CommitAsync();

                return StatusCode(201, new { message = $"User {newUser.Name} created successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SQL Error adding user: {ex.Message}");
                await transaction.RollbackAsync();
                return StatusCode(500, "Database error occurred while adding user.");
            }
        }
    }

    // --- 5. PUT: api/users/{employeeNumber} (แก้ไข User) ---
    // (*** แก้ไข: "กฎ C" + บังคับ ManagerId = NULL ***)
    [HttpPut("{employeeNumber}")]
    public async Task<IActionResult> UpdateUser(string employeeNumber, [FromBody] AddUserModel updatedUser)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            int? appUserId = null;
            int? employeeId = null; // (ID ของคนที่เรากำลังจะแก้)

            try
            {
                // 1. หา AppUserId และ EmployeeId
                string findSql = "SELECT EmployeeId, AppUserId FROM Employees WHERE EmployeeNumber = @EmployeeNumber";
                using (SqlCommand findCmd = new SqlCommand(findSql, connection, transaction))
                {
                    findCmd.Parameters.AddWithValue("@EmployeeNumber", employeeNumber);
                    using (SqlDataReader reader = await findCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            employeeId = reader.GetInt32(0);
                            appUserId = reader.GetInt32(1);
                        }
                    }
                }

                if (!appUserId.HasValue || !employeeId.HasValue)
                {
                    await transaction.RollbackAsync();
                    return NotFound(new { message = $"EmployeeNumber {employeeNumber} not found." });
                }

                // 2. เช็ก Name (Username) ซ้ำ
                string checkSql = "SELECT COUNT(*) FROM AppUsers WHERE Name = @Name AND Id != @AppUserId";
                using (SqlCommand checkCmd = new SqlCommand(checkSql, connection, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@Name", updatedUser.Name);
                    checkCmd.Parameters.AddWithValue("@AppUserId", appUserId.Value);
                    if ((int)await checkCmd.ExecuteScalarAsync() > 0)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = $"Username '{updatedUser.Name}' already exists for another user." });
                    }
                }

                // 3. (ใหม่: "กฎ C" - ลดตำแหน่งคนเก่า)
                if (updatedUser.Role == "Manager" && updatedUser.DepartmentId.HasValue)
                {
                    // 3.1. หา AppUserId ของ Manager คนเก่า (ถ้ามี)
                    int? oldManagerAppUserId = null;
                    string findOldMgrSql = @"
                        SELECT u.Id 
                        FROM AppUsers u
                        JOIN Employees e ON u.Id = e.AppUserId
                        WHERE e.DepartmentId = @DepartmentId 
                          AND u.Role = 'Manager' 
                          AND e.EmployeeId != @CurrentEmployeeId"; // (ต้องไม่ใช่คนที่เรากำลังแก้)

                    using (SqlCommand findCmd = new SqlCommand(findOldMgrSql, connection, transaction))
                    {
                        findCmd.Parameters.AddWithValue("@DepartmentId", updatedUser.DepartmentId.Value);
                        findCmd.Parameters.AddWithValue("@CurrentEmployeeId", employeeId.Value);
                        var result = await findCmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            oldManagerAppUserId = (int)result;
                        }
                    }

                    // 3.2. ถ้าเจอ... ลดตำแหน่ง (Demote) คนเก่า
                    if (oldManagerAppUserId.HasValue)
                    {
                        string demoteSql = "UPDATE AppUsers SET Role = 'Employee' WHERE Id = @OldManagerAppUserId";
                        using (SqlCommand demoteCmd = new SqlCommand(demoteSql, connection, transaction))
                        {
                            demoteCmd.Parameters.AddWithValue("@OldManagerAppUserId", oldManagerAppUserId.Value);
                            await demoteCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
                // (*** จบส่วนใหม่ ***)


                // 4. อัปเดตตาราง AppUsers (ของคนปัจจุบัน)
                bool isChangingPassword = !string.IsNullOrEmpty(updatedUser.Password);
                string sqlAppUser = isChangingPassword ?
                    "UPDATE AppUsers SET Name = @Name, Password = @Password, Role = @Role WHERE Id = @AppUserId" :
                    "UPDATE AppUsers SET Name = @Name, Role = @Role WHERE Id = @AppUserId";

                using (SqlCommand cmdAppUser = new SqlCommand(sqlAppUser, connection, transaction))
                {
                    cmdAppUser.Parameters.AddWithValue("@Name", updatedUser.Name);
                    cmdAppUser.Parameters.AddWithValue("@Role", updatedUser.Role);
                    cmdAppUser.Parameters.AddWithValue("@AppUserId", appUserId.Value);
                    if (isChangingPassword)
                    {
                        cmdAppUser.Parameters.AddWithValue("@Password", updatedUser.Password); // (Plain text)
                    }
                    await cmdAppUser.ExecuteNonQueryAsync();
                }

                // 5. อัปเดตตาราง Employees (ของคนปัจจุบัน)
                string sqlEmployee = @"
                    UPDATE Employees SET 
                        EmployeeNumber = @EmployeeNumber, FirstName = @FirstName, LastName = @LastName, 
                        DateOfBirth = @DateOfBirth, Gender = @Gender, Email = @Email, PhoneNumber = @PhoneNumber,
                        Address_Street = @Address_Street, Address_City = @Address_City, Address_State = @Address_State, 
                        Address_PostalCode = @Address_PostalCode, Address_Country = @Address_Country,
                        CurrentSalary = @CurrentSalary, DepartmentId = @DepartmentId, 
                        JobTitleId = @JobTitleId, ManagerId = @ManagerId
                    WHERE EmployeeId = @EmployeeId";

                using (SqlCommand cmdEmployee = new SqlCommand(sqlEmployee, connection, transaction))
                {
                    // (เพิ่ม Parameters ... )
                    cmdEmployee.Parameters.AddWithValue("@EmployeeNumber", updatedUser.EmployeeNumber);
                    cmdEmployee.Parameters.AddWithValue("@FirstName", updatedUser.FirstName);
                    cmdEmployee.Parameters.AddWithValue("@LastName", updatedUser.LastName);
                    cmdEmployee.Parameters.AddWithValue("@DateOfBirth", (object?)updatedUser.DateOfBirth ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Gender", (object?)updatedUser.Gender ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Email", (object?)updatedUser.Email ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@PhoneNumber", (object?)updatedUser.PhoneNumber ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Address_Street", (object?)updatedUser.Address_Street ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Address_City", (object?)updatedUser.Address_City ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Address_State", (object?)updatedUser.Address_State ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Address_PostalCode", (object?)updatedUser.Address_PostalCode ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@Address_Country", (object?)updatedUser.Address_Country ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@CurrentSalary", (object?)updatedUser.CurrentSalary ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@DepartmentId", (object?)updatedUser.DepartmentId ?? DBNull.Value);
                    cmdEmployee.Parameters.AddWithValue("@JobTitleId", (object?)updatedUser.JobTitleId ?? DBNull.Value);

                    // (*** ใหม่: โลจิก "กฎ C" ที่คุณเพิ่งเพิ่ม ***)
                    // ถ้า User คนนี้กำลังจะถูก "อัปเดต" เป็น Manager...
                    if (updatedUser.Role == "Manager")
                    {
                        // ...บังคับให้ ManagerId ของ "ตัวเอง" เป็น NULL
                        cmdEmployee.Parameters.AddWithValue("@ManagerId", DBNull.Value);
                    }
                    else
                    {
                        // ...ถ้าเป็นพนักงานธรรมดา ก็ใช้ค่าที่ส่งมาจากฟอร์ม
                        cmdEmployee.Parameters.AddWithValue("@ManagerId", (object?)updatedUser.ManagerId ?? DBNull.Value);
                    }
                    // (*** จบส่วนใหม่ ***)

                    cmdEmployee.Parameters.AddWithValue("@EmployeeId", employeeId.Value); // <<< WHERE Id
                    await cmdEmployee.ExecuteNonQueryAsync();
                }

                // 6. (ใหม่: "กฎ C" - อัปเดตทีม)
                if (updatedUser.Role == "Manager" && updatedUser.DepartmentId.HasValue)
                {
                    string updateTeamSql = @"
                        UPDATE Employees 
                        SET ManagerId = @NewManagerEmployeeId 
                        WHERE DepartmentId = @DepartmentId 
                          AND EmployeeId != @NewManagerEmployeeId"; // (อัปเดตทุกคน "ยกเว้น" ตัวเอง)

                    using (SqlCommand updateCmd = new SqlCommand(updateTeamSql, connection, transaction))
                    {
                        updateCmd.Parameters.AddWithValue("@NewManagerEmployeeId", employeeId.Value); // (ID ของ Manager ที่กำลังถูกแก้)
                        updateCmd.Parameters.AddWithValue("@DepartmentId", updatedUser.DepartmentId.Value);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
                // (*** จบส่วนใหม่ ***)

                await transaction.CommitAsync();

                return Ok(new { message = $"User {updatedUser.Name} updated successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SQL Error updating user: {ex.Message}");
                await transaction.RollbackAsync();
                return StatusCode(500, "Database error occurred while updating user.");
            }
        }
    }


    // --- (DeleteUser: เหมือนเดิม) ---
    [HttpDelete("{employeeNumber}")]
    public async Task<IActionResult> DeleteUser(string employeeNumber)
    {
        // ... (โค้ด DeleteUser ทั้งหมดของคุณ เหมือนเดิม ไม่ต้องแก้) ...
        int? appUserId = null;
        int? employeeId = null;

        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            string findSql = "SELECT EmployeeId, AppUserId FROM Employees WHERE EmployeeNumber = @EmployeeNumber";
            using (SqlCommand findCmd = new SqlCommand(findSql, conn))
            {
                findCmd.Parameters.AddWithValue("@EmployeeNumber", employeeNumber);
                await conn.OpenAsync();
                using (SqlDataReader reader = await findCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        employeeId = reader.GetInt32(0);
                        appUserId = reader.GetInt32(1);
                    }
                }
            }
        }

        if (!appUserId.HasValue || !employeeId.HasValue)
        {
            return NotFound(new { message = $"EmployeeNumber {employeeNumber} not found." });
        }

        if (appUserId == 1)
        {
            return BadRequest(new { message = "Cannot delete the root Admin user." });
        }

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            try
            {
                // 3.1 ลบจาก Employees ก่อน
                string sqlEmp = "DELETE FROM Employees WHERE EmployeeId = @EmployeeId";
                using (SqlCommand cmdEmp = new SqlCommand(sqlEmp, connection, transaction))
                {
                    cmdEmp.Parameters.AddWithValue("@EmployeeId", employeeId.Value);
                    await cmdEmp.ExecuteNonQueryAsync();
                }

                // 3.2 ลบจาก AppUsers
                string sqlUser = "DELETE FROM AppUsers WHERE Id = @AppUserId";
                using (SqlCommand cmdUser = new SqlCommand(sqlUser, connection, transaction))
                {
                    cmdUser.Parameters.AddWithValue("@AppUserId", appUserId.Value);
                    await cmdUser.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return NoContent();
            }
            catch (SqlException ex)
            {
                if (ex.Number == 547) // Foreign Key violation
                {
                    await transaction.RollbackAsync();
                    return Conflict(new { message = "Cannot delete user: This user is set as a Manager for other employees. Please reassign their team first." });
                }
                Console.WriteLine($"SQL Error deleting user: {ex.Message}");
                await transaction.RollbackAsync();
                return StatusCode(500, "Database error occurred while deleting user.");
            }
        }
    }
}