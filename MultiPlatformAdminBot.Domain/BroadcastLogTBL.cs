using System;

namespace Domain.Entity
{
    public class BroadcastLogTBL : MainEntity
    {
        public long ID { get; set; }
        public int Status { get; set; }
        public int Type { get; set; }
        public string? Explain { get; set; }
        public DateTime DateInsert { get; set; }

        public long AdminUserId { get; set; }
        public AdminUserTBL? AdminUser { get; set; }
        
        public string? ContentSummary { get; set; }
        public bool IsSuccessful { get; set; }
        public string? ErrorLog { get; set; }
    }
}
