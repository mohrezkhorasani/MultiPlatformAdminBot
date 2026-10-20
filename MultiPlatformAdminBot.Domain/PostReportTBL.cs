using System;
using System.Collections.Generic;

namespace Domain.Entity
{
    public class PostReportTBL : MainEntity
    {
        public long ID { get; set; }
        public int Status { get; set; }
        public int Type { get; set; }
        public string? Explain { get; set; }
        public DateTime DateInsert { get; set; }

        public long? AdminChannelId { get; set; }
        public AdminChannelTBL? AdminChannel { get; set; }

        public bool IsSuccessful { get; set; }
        public string? ErrorLog { get; set; }
        public long? SourceMessageId { get; set; }
        
        public string? DestinationMessageId { get; set; }
        
        /// <summary>
        /// 1 = Input (from Webhook), 2 = Output (Broadcasted to Channels)
        /// </summary>
        public int InOutType { get; set; }
        
        public ICollection<PostReportMetaDataTBL> MetaDatas { get; set; } = new List<PostReportMetaDataTBL>();
    }
}
