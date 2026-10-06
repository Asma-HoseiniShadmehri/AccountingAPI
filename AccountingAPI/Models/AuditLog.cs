using System;

namespace AccountingAPI.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string? UserMobile { get; set; }
        public string? Endpoint { get; set; }
        public string? Method { get; set; }
        public string? RequestBody { get; set; }
        public string? ResponseStatus { get; set; }  // ← این خط را اضافه کنید
        public string? ClientIP { get; set; }
        public DateTime Timestamp { get; set; }
    }
}