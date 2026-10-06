using System; // فضای نام برای DateTime

namespace AccountingAPI.Models
{
    public class DatabaseEntity
    {
        public int Id { get; set; }
        public string? DbName { get; set; }
        public string? Number { get; set; }
        public string? Level { get; set; }
        public string? Memo { get; set; }
        public int CompanyId { get; set; }           // ← اضافه شد
        public Company? Company { get; set; }        // ← اضافه شد
        public DateTime RegisterDate { get; set; }
        public DateTime? DisableDate { get; set; }
        public DateTime? DeleteDate { get; set; }
    }
}