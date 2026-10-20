using System.IO;
using System.Threading.Tasks;
using Application.Interfaces.IInfrastructure;
using Moq;
using Xunit;

namespace MultiPlatformAdminBot.Tests
{
    public class IBotPlatformClientTests
    {
        [Fact]
        public async Task SendTextMessageAsync_Should_Accept_ReplyToMessageId_And_Return_StringMessageId()
        {
            // Arrange
            var mockClient = new Mock<IBotPlatformClient>();
            string expectedTargetId = "channel_1";
            string expectedText = "Hello";
            string expectedReplyId = "reply_123";
            string expectedReturnId = "msg_456";

            mockClient.Setup(c => c.SendTextMessageAsync(expectedTargetId, expectedText, "html", null, null, expectedReplyId))
                      .ReturnsAsync((true, expectedReturnId));

            // Act
            var result = await mockClient.Object.SendTextMessageAsync(expectedTargetId, expectedText, "html", null, null, expectedReplyId);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(expectedReturnId, result.MessageId);
        }

        [Fact]
        public async Task SendPhotoAsync_Should_Accept_ReplyToMessageId_And_Return_StringMessageId()
        {
            var mockClient = new Mock<IBotPlatformClient>();
            var stream = new MemoryStream();
            
            mockClient.Setup(c => c.SendPhotoAsync("target", stream, "file.jpg", "caption", "html", null, "reply_id"))
                      .ReturnsAsync((true, "photo_123"));

            var result = await mockClient.Object.SendPhotoAsync("target", stream, "file.jpg", "caption", "html", null, "reply_id");

            Assert.True(result.Success);
            Assert.Equal("photo_123", result.MessageId);
        }

        [Fact]
        public async Task EditMessageTextAsync_Should_Return_True_When_Successful()
        {
            var mockClient = new Mock<IBotPlatformClient>();
            mockClient.Setup(c => c.EditMessageTextAsync("channel_1", 12345, "Edited Text", "html", null))
                      .ReturnsAsync(true);

            var result = await mockClient.Object.EditMessageTextAsync("channel_1", 12345, "Edited Text", "html", null);

            Assert.True(result);
        }

        [Fact]
        public async Task EditMessageTextAsync_Should_Accept_Rubika_64Bit_String_MessageId()
        {
            var mockClient = new Mock<IBotPlatformClient>();
            string rubika64BitMsgId = "1935118240704994929";

            mockClient.Setup(c => c.EditMessageTextAsync("@HekmatTest", rubika64BitMsgId, "Edited Text", "html", null, null))
                      .ReturnsAsync((true, (string?)null));

            var result = await mockClient.Object.EditMessageTextAsync("@HekmatTest", rubika64BitMsgId, "Edited Text", "html", null, null);

            Assert.True(result.Success);
            Assert.Null(result.Error);
        }
    }
}
