using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entity
{
    [Table("PendingBroadcastTBLs")]
    public class PendingBroadcastTBL : MainEntity
    {
        public long ID { get; set; }
        
        // Admin ID who sent the message
        public long AdminUserId { get; set; }
        
        // JSON serialized List<Telegram.Bot.Types.Update>
        public string UpdatesJson { get; set; }
        
        public int Status { get; set; } // 0 = Pending, 1 = Confirmed, 2 = Deleted, 3 = Scheduled, 4 = Processing
        public int Type { get; set; } // 1 = Telegram, 2 = Bale etc.
        public DateTime DateInsert { get; set; }
        public string? Explain { get; set; }
        public DateTime? ScheduledAtUtc { get; set; }
    }
}
