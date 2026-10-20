namespace Domain.Entity
{
    public class DeliveryLogTBL : MainEntity
    {
        public long ID { get; set; }
        public int Status { get; set; }
        public int Type { get; set; }
        public string? Explain { get; set; }
        public DateTime DateInsert { get; set; }

        /// <summary>Link to the overall broadcast log (null if created ad-hoc).</summary>
        public long? BroadcastLogId { get; set; }
        public long AdminUserId { get; set; }
        public string PlatformName { get; set; }
        public string TargetChannelId { get; set; }

        // 1 = Text, 2 = Photo, 3 = Video, 4 = Document
        public int ContentType { get; set; }

        /// <summary>Full text (with footer, HTML) for text deliveries — used to retry without the original update.</summary>
        public string? ContentText { get; set; }
        /// <summary>Local path of the downloaded media for media deliveries (file may be cleaned up later).</summary>
        public string? MediaPath { get; set; }
        public string? MediaFileName { get; set; }

        // 1 = AwaitingRetry, 2 = RetriedSent, 3 = PermanentFailure
        public int AttemptCount { get; set; }
        public DateTime? NextRetryAt { get; set; }
        public string? LastError { get; set; }
    }
}
