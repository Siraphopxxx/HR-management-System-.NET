using System.ComponentModel.DataAnnotations;

namespace be.Models;

public class ProjectReadModel
{
    public int ProjectId { get; set; }
    public required string ProjectName { get; set; }
    public string? ProjectDescription { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public required string Status { get; set; }
    public int? ProjectManagerEmployeeId { get; set; } // (*** ใหม่ ***)
}

public class ProjectWriteModel
{
    [Required]
    [MaxLength(255)]
    public required string ProjectName { get; set; }

    public string? ProjectDescription { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    [Required]
    public required string Status { get; set; }

    public int? ProjectManagerEmployeeId { get; set; } // (*** ใหม่ ***)
}


// (*** ก๊อบปี้ 2 class นี้ ไป "เพิ่ม" ใน ProjectModels.cs ***)

// Model สำหรับ "อ่าน" ลูกทีม
public class ProjectMemberReadModel
{
    public int ProjectMemberId { get; set; } // (ID ของแถว M:M)
    public int EmployeeId { get; set; }
    public required string FullName { get; set; }
    public required string EmployeeNumber { get; set; }
    public string? JobTitle { get; set; }
}

// Model สำหรับ "เขียน" (เพิ่มลูกทีม)
public class ProjectMemberWriteModel
{
    [Required]
    public int ProjectId { get; set; }
    [Required]
    public int EmployeeId { get; set; }
}

// (*** ก๊อบปี้ 2 class นี้ ไป "เพิ่ม" ใน ProjectModels.cs ***)

// Model สำหรับ "อ่าน" งาน (Task)
public class ProjectTaskReadModel
{
    public int TaskId { get; set; }
    public int ProjectId { get; set; }
    public required string TaskName { get; set; }
    public required string Status { get; set; }
    public DateTime? DueDate { get; set; }

    // (ข้อมูลของคนที่ถูกสั่งงาน)
    public int? AssignedToEmployeeId { get; set; }
    public string? AssignedToEmployeeName { get; set; } // (เช่น "สมชาย ใจดี")
}

// (*** ก๊อบปี้อันนี้ ไป "ทับ" ProjectTaskWriteModel เก่า ***)

// Model สำหรับ "เขียน" (สร้างงาน)
public class ProjectTaskWriteModel
{
    [Required]
    public int ProjectId { get; set; }

    [Required]
    public required string TaskName { get; set; }
    public string? TaskDescription { get; set; }

    // (*** ลบ Status ออกไปแล้ว -> เราจะบังคับ 'To Do' ใน Controller ***)

    public DateTime? DueDate { get; set; }

    public int? AssignedToEmployeeId { get; set; }
}


// (*** ก๊อบปี้ 2 class นี้ ไป "เพิ่ม" ใน ProjectModels.cs ***)

// Model สำหรับ "อ่าน" งานของฉัน (My Tasks)
public class MyTaskReadModel
{
    public int TaskId { get; set; }
    public required string TaskName { get; set; }
    public required string Status { get; set; }
    public DateTime? DueDate { get; set; }

    // (ข้อมูลโปรเจกต์)
    public int ProjectId { get; set; }
    public required string ProjectName { get; set; }
}

// Model สำหรับ "อัปเดต" สถานะ
public class UpdateTaskStatusModel
{
    [Required]
    public required string NewStatus { get; set; } // (To Do, In Progress, Done)
}