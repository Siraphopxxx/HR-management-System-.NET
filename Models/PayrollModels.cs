using System.ComponentModel.DataAnnotations;

namespace be.Models;

// Model สำหรับ "รับคำสั่ง" (Input) จาก HR
public class RunPayrollInputModel
{
    [Required]
    public int Month { get; set; }
    [Required]
    public int Year { get; set; }
}

// Model สำหรับ "แสดงประวัติ" (History) ในตาราง
public class PayslipHistoryModel
{
    public int PayslipId { get; set; }
    public required string EmployeeName { get; set; }
    public DateTime PayDate { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public required string Status { get; set; }
}


// (*** ก๊อบปี้ 3 class นี้ ไป "เพิ่ม" ใน PayrollModels.cs ***)

// Model สำหรับ "แสดงรายการ" ในสลิป (Income/Deduction)
public class PayslipItemModel
{
    public required string ItemType { get; set; }
    public required string Description { get; set; }
    public decimal Amount { get; set; }
}

// Model สำหรับ "แสดงสลิป (ฉบับเต็ม)"
public class PayslipDetailModel
{
    // (ข้อมูลหัวกระดาษ)
    public int PayslipId { get; set; }
    public required string EmployeeName { get; set; }
    public required string EmployeeNumber { get; set; }
    public required string DepartmentName { get; set; }
    public DateTime PayDate { get; set; }

    // (ข้อมูลสรุป)
    public decimal TotalIncome { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetSalary { get; set; }

    // (รายการ)
    public List<PayslipItemModel> IncomeItems { get; set; } = new();
    public List<PayslipItemModel> DeductionItems { get; set; } = new();
}

// Model สำหรับ "แสดงประวัติ" (หน้า List)
public class MyPayslipHistoryModel
{
    public int PayslipId { get; set; }
    public DateTime PayDate { get; set; }
    public decimal NetSalary { get; set; }
}