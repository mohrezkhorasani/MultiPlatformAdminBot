using System;
using Xunit;
using MultiPlatformAdminBot.Common.Utils;

namespace MultiPlatformAdminBot.Tests
{
    public class PersianDateTimeParserTests
    {
        [Theory]
        [InlineData("11:00")]
        [InlineData("۱۱:۰۰")]
        [InlineData("14:30")]
        [InlineData("۱۴:۳۰")]
        public void ParseTime_ValidTime_ShouldReturnUtcFuture(string input)
        {
            var nowUtc = new DateTime(2026, 10, 10, 6, 0, 0, DateTimeKind.Utc); // 09:30 AM Tehran
            bool success = PersianDateTimeParser.TryParseSchedule(input, nowUtc, out DateTime targetUtc, out string error);

            Assert.True(success, error);
            Assert.True(targetUtc > nowUtc);
        }

        [Fact]
        public void ParseRelative_OneHourLater_ShouldReturnOneHour()
        {
            var nowUtc = new DateTime(2026, 10, 10, 6, 0, 0, DateTimeKind.Utc);
            bool success = PersianDateTimeParser.TryParseSchedule("+1h", nowUtc, out DateTime targetUtc, out string error);

            Assert.True(success);
            Assert.Equal(nowUtc.AddHours(1), targetUtc);
        }

        [Fact]
        public void ParsePastTime_Today_ShouldRollOverToTomorrow()
        {
            // Now is 14:00 Tehran (10:30 UTC), input is 11:00 Tehran
            var nowUtc = new DateTime(2026, 10, 10, 10, 30, 0, DateTimeKind.Utc);
            bool success = PersianDateTimeParser.TryParseSchedule("11:00", nowUtc, out DateTime targetUtc, out string error);

            Assert.True(success);
            Assert.True(targetUtc > nowUtc);
            // Target should be 11:00 Tehran tomorrow (07:30 UTC next day)
            Assert.Equal(new DateTime(2026, 10, 11, 7, 30, 0, DateTimeKind.Utc), targetUtc);
        }

        [Fact]
        public void ParseInvalid_ShouldReturnFalse()
        {
            var nowUtc = DateTime.UtcNow;
            bool success = PersianDateTimeParser.TryParseSchedule("نامعتبر", nowUtc, out _, out string error);

            Assert.False(success);
            Assert.NotNull(error);
        }
    }
}
