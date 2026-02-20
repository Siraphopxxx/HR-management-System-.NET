using Microsoft.Data.SqlClient;
using System.Data;

namespace be.Helpers;

public static class LeaveCalculationHelper
{
    // --- นี่คือฟังก์ชัน "สมอง" ของเรา ---
    public static async Task<int> CalculateBusinessDays(
        DateTime startDate,
        DateTime endDate,
        string connectionString)
    {
        // 1. ดึง "วันหยุดทั้งหมด" ที่อยู่ในช่วงลา
        var publicHolidays = await GetPublicHolidaysInRange(startDate, endDate, connectionString);

        int businessDays = 0;
        DateTime currentDate = startDate.Date;

        // 2. วน Loop ทีละวัน
        while (currentDate.Date <= endDate.Date)
        {
            // 3. เช็กว่าเป็น "วันทำงาน" หรือไม่
            if (currentDate.DayOfWeek != DayOfWeek.Saturday &&
                currentDate.DayOfWeek != DayOfWeek.Sunday &&
                !publicHolidays.Contains(currentDate.Date))
            {
                // ถ้าใช่... ก็นับ 1
                businessDays++;
            }

            // (เลื่อนไปวันถัดไป)
            currentDate = currentDate.AddDays(1);
        }

        return businessDays;
    }

    // (ฟังก์ชันย่อย: ดึงวันหยุดจาก DB)
    private static async Task<HashSet<DateTime>> GetPublicHolidaysInRange(
        DateTime startDate,
        DateTime endDate,
        string connectionString)
    {
        var holidays = new HashSet<DateTime>();
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            string sql = "SELECT HolidayDate FROM PublicHolidays WHERE HolidayDate >= @StartDate AND HolidayDate <= @EndDate";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@StartDate", startDate.Date);
                command.Parameters.AddWithValue("@EndDate", endDate.Date);
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            holidays.Add(reader.GetDateTime(0).Date);
                        }
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"Error fetching holidays for calculation: {ex.Message}");
                    // (ถ้าดึงวันหยุดไม่ขึ้น ก็ปล่อยให้มันนับวันหยุดเป็นวันทำงานไปก่อน)
                }
            }
        }
        return holidays;
    }
}