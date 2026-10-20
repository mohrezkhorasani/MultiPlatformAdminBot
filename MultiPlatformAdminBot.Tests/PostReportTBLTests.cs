using Domain.Entity;
using Xunit;

namespace MultiPlatformAdminBot.Tests
{
    public class PostReportTBLTests
    {
        [Fact]
        public void PostReportTBL_Should_Have_DestinationMessageId_Property()
        {
            // Arrange
            var postReport = new PostReportTBL();
            var expectedMessageId = "1234567890_test_id";

            // Act
            postReport.DestinationMessageId = expectedMessageId;

            // Assert
            Assert.Equal(expectedMessageId, postReport.DestinationMessageId);
        }
    }
}
