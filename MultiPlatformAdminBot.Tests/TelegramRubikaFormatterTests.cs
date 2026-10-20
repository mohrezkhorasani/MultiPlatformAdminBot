using Application.Utils;
using Newtonsoft.Json.Linq;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;

namespace MultiPlatformAdminBot.Tests
{
    public class TelegramRubikaFormatterTests
    {
        private static Message MakeMessage(string? text = null, MessageEntity[]? entities = null)
        {
            var msg = new Message
            {
                MessageId = 1,
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = 123 }
            };
            if (text != null)
            {
                msg.Text = text;
                msg.Entities = entities;
            }
            return msg;
        }

        private static MessageEntity Entity(MessageEntityType type, int offset, int length) =>
            new MessageEntity { Type = type, Offset = offset, Length = length };

        [Fact]
        public void GetPlainText_ReturnsText()
        {
            Assert.Equal("hello", TelegramRubikaFormatter.GetPlainText(MakeMessage("hello")));
        }

        [Fact]
        public void GetPlainText_EmptyWhenNoText()
        {
            Assert.Equal("", TelegramRubikaFormatter.GetPlainText(MakeMessage()));
        }

        [Fact]
        public void GetRubikaMetadata_NullWhenNoEntities()
        {
            Assert.Null(TelegramRubikaFormatter.GetRubikaMetadata(MakeMessage("plain")));
        }

        [Fact]
        public void GetRubikaMetadata_BoldEntity_ProducesPart()
        {
            var msg = MakeMessage("hello bold", new[] { Entity(MessageEntityType.Bold, 6, 4) });
            var j = JObject.FromObject(TelegramRubikaFormatter.GetRubikaMetadata(msg)!);
            var parts = (JArray)j["meta_data_parts"]!;
            Assert.Single(parts);
            Assert.Equal("Bold", (string)parts[0]["type"]!);
            Assert.Equal(6, (int)parts[0]["from_index"]!);
            Assert.Equal(4, (int)parts[0]["length"]!);
        }

        [Fact]
        public void GetRubikaMetadata_LinkEntity_IncludesUrl()
        {
            var link = Entity(MessageEntityType.TextLink, 0, 4);
            link.Url = "https://example.com";
            var msg = MakeMessage("site rest", new[] { link });
            var j = JObject.FromObject(TelegramRubikaFormatter.GetRubikaMetadata(msg)!);
            var part = (JArray)j["meta_data_parts"]!;
            Assert.Equal("Link", (string)part[0]["type"]!);
            Assert.Equal("https://example.com", (string)part[0]["link_url"]!);
        }

        [Fact]
        public void GetRubikaMetadata_UnsupportedEntity_ReturnsNull()
        {
            var msg = MakeMessage("has #tag", new[] { Entity(MessageEntityType.Hashtag, 4, 4) });
            Assert.Null(TelegramRubikaFormatter.GetRubikaMetadata(msg));
        }
    }
}
