using System.Threading.Tasks;
using Application.DTOs;
using Xunit;

namespace MultiPlatformAdminBot.Tests
{
    public class BroadcastMessageRequestTests
    {
        [Fact]
        public void BroadcastMessageRequest_Should_Have_ReplyToSourceMessageId_Property()
        {
            var request = new BroadcastMessageRequest();
            long expectedId = 99999;
            
            request.ReplyToSourceMessageId = expectedId;
            
            Assert.Equal(expectedId, request.ReplyToSourceMessageId);
        }
    }
}
