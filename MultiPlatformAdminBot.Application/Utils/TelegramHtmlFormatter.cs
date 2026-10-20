using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Application.Utils
{
    public static class TelegramHtmlFormatter
    {
        public static string GetHtmlText(Message message)
        {
            var text = message.Text ?? message.Caption;
            var entities = message.Entities ?? message.CaptionEntities;

            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (entities == null || entities.Length == 0)
                return EscapeHtml(text);

            // Entities are measured in UTF-16 code units (which maps 1:1 with C# string index)
            // Sort entities by offset, then by length descending (to handle nesting correctly)
            var sortedEntities = entities
                .OrderBy(e => e.Offset)
                .ThenByDescending(e => e.Length)
                .ToList();

            var sb = new StringBuilder();
            int currentIndex = 0;

            // Simple recursive or stack-based approach is best for nested tags.
            // But since Telegram entities rarely nest wildly, we can process them sequentially
            // with a stack.
            return ProcessEntities(text, sortedEntities, 0, text.Length);
        }

        private static string ProcessEntities(string text, List<MessageEntity> entities, int start, int end)
        {
            var sb = new StringBuilder();
            int currentIndex = start;

            // Only consider entities completely within this range
            var relevantEntities = entities.Where(e => e.Offset >= start && e.Offset + e.Length <= end).ToList();

            while (currentIndex < end)
            {
                // Find the next entity that starts at or after currentIndex
                var nextEntity = relevantEntities.FirstOrDefault(e => e.Offset >= currentIndex);

                if (nextEntity == null)
                {
                    // No more entities, append the rest
                    sb.Append(EscapeHtml(text.Substring(currentIndex, end - currentIndex)));
                    break;
                }

                // Append text before the entity
                if (nextEntity.Offset > currentIndex)
                {
                    sb.Append(EscapeHtml(text.Substring(currentIndex, nextEntity.Offset - currentIndex)));
                }

                // Get nested entities
                var nestedEntities = relevantEntities
                    .Where(e => e != nextEntity && e.Offset >= nextEntity.Offset && e.Offset + e.Length <= nextEntity.Offset + nextEntity.Length)
                    .ToList();

                // Process the content inside the entity
                string innerText = ProcessEntities(text, nestedEntities, nextEntity.Offset, nextEntity.Offset + nextEntity.Length);

                // Wrap with HTML tag
                sb.Append(WrapWithHtmlTag(innerText, nextEntity));

                // Move index past this entity
                currentIndex = nextEntity.Offset + nextEntity.Length;
            }

            return sb.ToString();
        }

        private static string WrapWithHtmlTag(string innerHtml, MessageEntity entity)
        {
            switch (entity.Type)
            {
                case MessageEntityType.Bold:
                    return $"<b>{innerHtml}</b>";
                case MessageEntityType.Italic:
                    return $"<i>{innerHtml}</i>";
                case MessageEntityType.Underline:
                    return $"<u>{innerHtml}</u>";
                case MessageEntityType.Strikethrough:
                    return $"<s>{innerHtml}</s>";
                case MessageEntityType.Code:
                    return $"<code>{innerHtml}</code>";
                case MessageEntityType.Pre:
                    return $"<pre>{innerHtml}</pre>";
                case MessageEntityType.TextLink:
                    return $"<a href=\"{EscapeHtmlAttribute(entity.Url)}\">{innerHtml}</a>";
                case MessageEntityType.TextMention:
                    return $"<a href=\"tg://user?id={entity.User.Id}\">{innerHtml}</a>";
                case MessageEntityType.Spoiler:
                    return $"<tg-spoiler>{innerHtml}</tg-spoiler>";
                // Standard Mentions, Hashtags, Cashtags, BotCommands, PhoneNumbers etc. usually don't need wrapping 
                // because the text itself represents the entity, but you could wrap them if needed.
                case MessageEntityType.CustomEmoji:
                    // Custom emojis can't be rendered on Eitaa/Bale. Telegram uses the base emoji character as fallback (which is in the text).
                    // We just return the text (which contains the base emoji).
                    return innerHtml;
                default:
                    return innerHtml;
            }
        }

        private static string EscapeHtml(string text)
        {
            return text.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;");
        }

        private static string EscapeHtmlAttribute(string text)
        {
            return text.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;")
                       .Replace("\"", "&quot;");
        }
    }
}
