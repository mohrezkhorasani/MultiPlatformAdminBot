using System;

namespace Domain.Entity
{
    public class AdminUserTBL : MainEntity
    {
        public long ID { get; set; }
        public int Status { get; set; }
        public int Type { get; set; }
        public string? Explain { get; set; }
        public DateTime DateInsert { get; set; }

        public string? Username { get; set; }
        public string? FullName { get; set; }
        
        public bool IsUploadingAlbum { get; set; }
        public string? CurrentAlbumId { get; set; }
        
        public string? PasswordHash { get; set; }

        public bool HasPanelAccess { get; set; }
        public int MaxChannelsPerToken { get; set; }
        public int MaxTotalChannels { get; set; }
        public string CurrentRoute { get; set; } = "Home";
        public long? OwnerId { get; set; }
        public string? FooterText { get; set; }

        public System.Collections.Generic.ICollection<AdminChannelTBL> AdminChannels { get; set; }
    }
}
