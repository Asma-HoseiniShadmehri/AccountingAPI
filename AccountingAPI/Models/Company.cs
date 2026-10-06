using AccountingAPI.Models; // استفاده از مدل‌های پروژه (مانند User)

public class Company
{
    public int Id { get; set; } // شناسه یکتای شرکت (کلید اصلی)
    public string? NationalId { get; set; } // شناسه ملی شرکت (۱۱ رقم)
    public string? Name { get; set; } // نام شرکت
    public string? X { get; set; } // مختصات X (طول جغرافیایی)
    public string? Y { get; set; } // مختصات Y (عرض جغرافیایی)
    public string? Tel { get; set; } // شماره تلفن شرکت
    public int RegisteredByUserId { get; set; } // شناسه کاربر ثبت‌کننده (کلید خارجی)
    public User? RegisteredByUser { get; set; } // شیء کاربر ثبت‌کننده (رابطه)
    public DateTime RegisterDate { get; set; } // تاریخ ثبت شرکت
    public DateTime? DisableDate { get; set; } // تاریخ غیرفعال شدن (اختیاری)
    public DateTime? DeleteDate { get; set; } // تاریخ حذف شدن (اختیاری)
}