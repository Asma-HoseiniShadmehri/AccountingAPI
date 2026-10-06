using System;

namespace AccountingAPI.Models
{
    public class DatabaseHistory
    {
        public int Id { get; set; }
        public int OriginalId { get; set; }
        public string? DbName { get; set; }
        public string? Number { get; set; } // سال مالی
        public string? Memo { get; set; }
        public DateTime RegisterDate { get; set; }
        public DateTime? DisableDate { get; set; }
        public DateTime? DeleteDate { get; set; }
        public int? DeletedByUserId { get; set; }
        public DateTime DeletedAt { get; set; }
        public string? OperationType { get; set; }
        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public string? FinancialYear { get; set; }
    }
}