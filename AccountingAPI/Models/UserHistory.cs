using System;

namespace AccountingAPI.Models
{
    public class UserHistory
    {
        public int Id { get; set; }
        public int OriginalId { get; set; }
        public string? Mobile { get; set; }
        public string? Password { get; set; }
        public string? FullName { get; set; }
        public DateTime RegisterDate { get; set; }
        public DateTime? DisableDate { get; set; }
        public DateTime? DeleteDate { get; set; }
        public int? DeletedByUserId { get; set; }
        public DateTime DeletedAt { get; set; }
        public string? OperationType { get; set; }
        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }
    }
}