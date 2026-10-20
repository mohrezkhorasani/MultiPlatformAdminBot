using System;
using System.Collections.Generic;

namespace Domain.Entity
{
    public class BotTokenTBL : MainEntity
    {
        public long ID { get; set; }
        public int Status { get; set; }
        public int Type { get; set; }
        public string? Explain { get; set; }
        public DateTime DateInsert { get; set; }

        public string? PlatformName { get; set; } // Telegram, Bale, Eitaa, Rubika
        public string? TokenValue { get; set; }
        public bool IsActive { get; set; }

        public long? OwnerId { get; set; }
        public AdminUserTBL? Owner { get; set; }

        public ICollection<PlatformConfigTBL>? Channels { get; set; }
    }
}
