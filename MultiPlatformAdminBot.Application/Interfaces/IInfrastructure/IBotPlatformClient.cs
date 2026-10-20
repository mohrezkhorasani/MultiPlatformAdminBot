using System.IO;
using System.Threading.Tasks;

namespace Application.Interfaces.IInfrastructure
{
    public interface IBotPlatformClient
    {
        string PlatformName { get; }
        
        Task<(bool Success, string? MessageId)> SendTextMessageAsync(string targetId, string text, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null, string? replyToMessageId = null);
        Task<(bool Success, string? Error)> EditMessageTextAsync(string targetId, string messageId, string text, string parseMode = "html", Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null, Telegram.Bot.Types.Message originalMessage = null);
        async Task<bool> EditMessageTextAsync(string targetId, int messageId, string text, string parseMode = "html", Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null)
        {
            var (success, _) = await EditMessageTextAsync(targetId, messageId.ToString(), text, parseMode, replyMarkup);
            return success;
        }
        Task<(bool Success, string? MessageId)> SendPhotoAsync(string targetId, Stream photoStream, string fileName, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null);
        Task<(bool Success, string? MessageId)> SendVideoAsync(string targetId, Stream videoStream, string fileName, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null);
        Task<(bool Success, string? MessageId)> SendDocumentAsync(string targetId, Stream docStream, string fileName, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null);
        Task<(bool Success, string? MessageId)> SendMediaGroupAsync(string targetId, System.Collections.Generic.IEnumerable<Stream> mediaStreams, System.Collections.Generic.IEnumerable<string> fileNames, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null);
    }
}
