using System;

namespace Domain.Entity
{
    public class PlatformConfigTBL : MainEntity
    {
        public long ID { get; set; }
        public int Status { get; set; }
        public int Type { get; set; }
        public string? Explain { get; set; }
        public DateTime DateInsert { get; set; }

        public string? PlatformName { get; set; }
        public long? BotTokenId { get; set; }
        public BotTokenTBL BotToken { get; set; }
        public string? TargetChannelId { get; set; }
        public bool IsActive { get; set; }
        
        public System.Collections.Generic.ICollection<AdminChannelTBL> AdminChannels { get; set; }
    }
}
