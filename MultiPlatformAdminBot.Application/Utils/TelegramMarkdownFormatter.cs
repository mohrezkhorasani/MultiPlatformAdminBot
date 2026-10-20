using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Application.Utils
{
    public static class TelegramMarkdownFormatter
    {
        public static string GetMarkdownText(Message message)
        {
            var text = message.Text ?? message.Caption;
            var entities = message.Entities ?? message.CaptionEntities;

            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (entities == null || entities.Length == 0)
                return text;

            var sortedEntities = entities
                .OrderBy(e => e.Offset)
                .ThenByDescending(e => e.Length)
                .ToList();

            return ProcessEntities(text, sortedEntities, 0, text.Length);
        }

        private static string ProcessEntities(string text, List<MessageEntity> entities, int start, int end)
        {
            var sb = new StringBuilder();
            int currentIndex = start;

            var relevantEntities = entities.Where(e => e.Offset >= start && e.Offset + e.Length <= end).ToList();

            while (currentIndex < end)
            {
                var nextEntity = relevantEntities.FirstOrDefault(e => e.Offset >= currentIndex);

                if (nextEntity == null)
                {
                    sb.Append(text.Substring(currentIndex, end - currentIndex));
                    break;
                }

                if (nextEntity.Offset > currentIndex)
                {
                    sb.Append(text.Substring(currentIndex, nextEntity.Offset - currentIndex));
                }

                var nestedEntities = relevantEntities
                    .Where(e => e != nextEntity && e.Offset >= nextEntity.Offset && e.Offset + e.Length <= nextEntity.Offset + nextEntity.Length)
                    .ToList();

                string innerText = ProcessEntities(text, nestedEntities, nextEntity.Offset, nextEntity.Offset + nextEntity.Length);

                sb.Append(WrapWithMarkdown(innerText, nextEntity));

                currentIndex = nextEntity.Offset + nextEntity.Length;
            }

            return sb.ToString();
        }

        private static string WrapWithMarkdown(string innerText, MessageEntity entity)
        {
            // Bale's specific Markdown
            // Note: Bale documentation states space around bold/italic is recommended, but we can't easily inject space if it breaks word boundaries. 
            // Standard * and _ are used.
            switch (entity.Type)
            {
                case MessageEntityType.Bold:
                    return $" *{innerText}* ";
                case MessageEntityType.Italic:
                    return $" _{innerText}_ ";
                case MessageEntityType.TextLink:
                    return $"[{innerText}]({entity.Url})";
                case MessageEntityType.Code:
                case MessageEntityType.Pre:
                    return $"```{innerText}```";
                default:
                    return innerText;
            }
        }
    }
}
