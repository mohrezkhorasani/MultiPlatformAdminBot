using System;

namespace Domain.Entity
{
    public class AdminRequestTBL : MainEntity
    {
        public long ID { get; set; }
        public int Status { get; set; } // 0: Pending, 1: Approved, 2: Rejected
        public int Type { get; set; }
        public string? Explain { get; set; }
        public DateTime DateInsert { get; set; }

        public long UserId { get; set; }
        public string? Username { get; set; }
        public string? FullName { get; set; }
        
        public DateTime? BanExpiryDate { get; set; }
    }
}
