using System;

namespace Domain.Entity
{
    public class PostReportMetaDataTBL : MainEntity
    {
        public long ID { get; set; }
        public int Status { get; set; }
        public int Type { get; set; }
        public string? Explain { get; set; }
        public DateTime DateInsert { get; set; }

        public long PostReportId { get; set; }
        public PostReportTBL? PostReport { get; set; }

        public string MetaKey { get; set; } = string.Empty;
        public string MetaValue { get; set; } = string.Empty;
    }
}
