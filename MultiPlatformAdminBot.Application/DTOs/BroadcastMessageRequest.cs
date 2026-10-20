using Telegram.Bot.Types;

namespace Application.DTOs
{
    public class BroadcastMessageRequest
    {
        public Update Update { get; set; }
        public long UserId { get; set; }
        public string SourcePlatform { get; set; }
        public int? WaitMessageId { get; set; }
        public System.Collections.Generic.List<long>? RetryConfigIds { get; set; }
        public long? ReplyToSourceMessageId { get; set; }
    }
}
