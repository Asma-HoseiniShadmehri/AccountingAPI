using System;

namespace AccountingAPI.Models
{
    public class CompanyHistory
    {
        public int Id { get; set; }
        public int OriginalId { get; set; }
        public string? NationalId { get; set; }
        public string? Name { get; set; }
        public string? X { get; set; }
        public string? Y { get; set; }
        public string? Tel { get; set; }
        public int? DeletedByUserId { get; set; }
        public DateTime DeletedDate { get; set; }
        public string? OperationType { get; set; }  // "Delete" یا "Update"
    }
}