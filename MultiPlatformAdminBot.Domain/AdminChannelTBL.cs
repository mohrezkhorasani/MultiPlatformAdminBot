using System;

namespace Domain.Entity
{
    public class AdminChannelTBL : MainEntity
    {
        public long AdminUserId { get; set; }
        public AdminUserTBL AdminUser { get; set; }

        public long ChannelId { get; set; }
        public PlatformConfigTBL Channel { get; set; }

        // MainEntity Implementation
        public long ID { get; set; }
        public int Status { get; set; }
        public int Type { get; set; }
        public string? Explain { get; set; }
        public DateTime DateInsert { get; set; }
    }
}
