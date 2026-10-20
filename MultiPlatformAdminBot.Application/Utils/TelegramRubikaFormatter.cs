using System.Collections.Generic;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Application.Utils
{
    public static class TelegramRubikaFormatter
    {
        /// <summary>
        /// Raw plain text of the message. Rubika formatting is offset-based
        /// metadata over this exact string, so it must be sent unmodified.
        /// </summary>
        public static string GetPlainText(Message message) => message.Text ?? message.Caption ?? "";

        public static object GetRubikaMetadata(Message message)
        {
            var entities = message.Entities ?? message.CaptionEntities;
            if (entities == null || entities.Length == 0)
                return null;

            var parts = new List<object>();
            foreach (var entity in entities)
            {
                string rubikaType = null;
                switch (entity.Type)
                {
                    case MessageEntityType.Bold:
                        rubikaType = "Bold";
                        break;
                    case MessageEntityType.Italic:
                        rubikaType = "Italic";
                        break;
                    case MessageEntityType.TextMention:
                    case MessageEntityType.Mention:
                        rubikaType = "MentionText";
                        break;
                    case MessageEntityType.TextLink:
                        rubikaType = "Link";
                        break;
                }

                if (rubikaType != null)
                {
                    if (rubikaType == "MentionText")
                    {
                        parts.Add(new {
                            type = rubikaType,
                            from_index = entity.Offset,
                            length = entity.Length,
                            mention_text_user_id = entity.User?.Id.ToString() ?? "0"
                        });
                    }
                    else if (rubikaType == "Link")
                    {
                        parts.Add(new {
                            type = rubikaType,
                            from_index = entity.Offset,
                            length = entity.Length,
                            link_url = entity.Url
                        });
                    }
                    else
                    {
                        parts.Add(new {
                            type = rubikaType,
                            from_index = entity.Offset,
                            length = entity.Length
                        });
                    }
                }
            }

            if (parts.Count == 0) return null;

            return new {
                meta_data_parts = parts
            };
        }
    }
}
