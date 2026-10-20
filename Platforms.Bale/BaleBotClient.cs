using Application.Interfaces.IInfrastructure;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Platforms.Bale
{
    public class BaleBotClient : IBotPlatformClient
    {
        public string PlatformName => "Bale";
        private readonly HttpClient _httpClient;
        private readonly string _botToken;

        public BaleBotClient(HttpClient httpClient, Application.Interfaces.IAppDbContext dbContext)
        {
            _httpClient = httpClient;
            var tokenEntity = System.Linq.Enumerable.FirstOrDefault(dbContext.BotTokenTBLs, t => t.PlatformName == "Bale");
            _botToken = tokenEntity?.TokenValue ?? "BALE_BOT_TOKEN_PLACEHOLDER";
        }

        private static async Task<(bool Success, string? MessageId)> CheckResponseAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode) return (false, null);
            try
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("ok", out var okProp))
                {
                    bool isOk = okProp.GetBoolean(); string? msgId = null; if (isOk && doc.RootElement.TryGetProperty("result", out var resProp) && resProp.TryGetProperty("message_id", out var midProp)) msgId = midProp.ToString(); return (isOk, msgId);
                }
                return (true, null);
            }
            catch
            {
                return (response.IsSuccessStatusCode, null);
            }
        }

        public async Task<(bool Success, string? MessageId)> SendTextMessageAsync(string targetId, string text, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null, string? replyToMessageId = null)
        {
            if (originalMessage != null)
            {
                text = Application.Utils.TelegramMarkdownFormatter.GetMarkdownText(originalMessage);
                parseMode = null; // Bale uses markdown by default
            }

            var content = new MultipartFormDataContent();
            content.Add(new StringContent(targetId), "chat_id");
            content.Add(new StringContent(text), "text");
            if (!string.IsNullOrEmpty(parseMode)) content.Add(new StringContent(parseMode), "parse_mode");
            if (!string.IsNullOrEmpty(replyToMessageId)) content.Add(new StringContent(replyToMessageId), "reply_to_message_id");

            var response = await _httpClient.PostAsync($"https://tapi.bale.ai/bot{_botToken}/sendMessage", content);
            return await CheckResponseAsync(response); // Parse response if you want actual ID later
        }

        private static async Task<(bool Success, string? Error)> ParseResponseErrorAsync(HttpResponseMessage response)
        {
            try
            {
                var json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json))
                {
                    return (response.IsSuccessStatusCode, response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("ok", out var okProp) && okProp.GetBoolean())
                {
                    return (true, null);
                }

                if (doc.RootElement.TryGetProperty("description", out var descProp))
                {
                    return (false, descProp.GetString());
                }

                return (false, json);
            }
            catch
            {
                return (response.IsSuccessStatusCode, response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }
        }

        public async Task<(bool Success, string? Error)> EditMessageTextAsync(string targetId, string messageId, string text, string parseMode = "html", Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null, Telegram.Bot.Types.Message originalMessage = null)
        {
            try
            {
                string textToSend = text;
                string? pMode = parseMode;

                if (originalMessage != null)
                {
                    var mdOriginal = Application.Utils.TelegramMarkdownFormatter.GetMarkdownText(originalMessage);
                    var plainFull = System.Text.RegularExpressions.Regex.Replace(text ?? "", "<.*?>", string.Empty);
                    var plainOriginal = Application.Utils.TelegramRubikaFormatter.GetPlainText(originalMessage);
                    var footerSuffix = plainFull.StartsWith(plainOriginal, System.StringComparison.Ordinal)
                        ? plainFull.Substring(plainOriginal.Length)
                        : "";
                    textToSend = mdOriginal + footerSuffix;
                    pMode = null; // Bale default is markdown
                }

                // Helper to try editMessageCaption
                async Task<(bool Success, string? Error)> TryEditCaption(string captionVal, string? mode)
                {
                    using var capContent = new MultipartFormDataContent();
                    capContent.Add(new StringContent(targetId), "chat_id");
                    capContent.Add(new StringContent(messageId), "message_id");
                    capContent.Add(new StringContent(captionVal), "caption");
                    if (!string.IsNullOrEmpty(mode)) capContent.Add(new StringContent(mode), "parse_mode");

                    var capResp = await _httpClient.PostAsync($"https://tapi.bale.ai/bot{_botToken}/editMessageCaption", capContent);
                    return await ParseResponseErrorAsync(capResp);
                }

                // Helper to try editMessageText
                async Task<(bool Success, string? Error)> TryEditText(string textVal, string? mode)
                {
                    using var content = new MultipartFormDataContent();
                    content.Add(new StringContent(targetId), "chat_id");
                    content.Add(new StringContent(messageId), "message_id");
                    content.Add(new StringContent(textVal), "text");
                    if (!string.IsNullOrEmpty(mode)) content.Add(new StringContent(mode), "parse_mode");

                    var response = await _httpClient.PostAsync($"https://tapi.bale.ai/bot{_botToken}/editMessageText", content);
                    return await ParseResponseErrorAsync(response);
                }

                bool isMedia = originalMessage != null && originalMessage.Type != Telegram.Bot.Types.Enums.MessageType.Text;

                if (isMedia)
                {
                    // 1st: try editMessageCaption with markdown
                    var capRes = await TryEditCaption(textToSend, pMode);
                    if (capRes.Success) return (true, null);

                    // 2nd: try editMessageCaption with raw html
                    if (textToSend != text)
                    {
                        var capResHtml = await TryEditCaption(text, parseMode);
                        if (capResHtml.Success) return (true, null);
                    }

                    // 3rd: fallback to editMessageText just in case
                    var txtRes = await TryEditText(textToSend, pMode);
                    if (txtRes.Success) return (true, null);

                    return capRes;
                }
                else
                {
                    // 1st: try editMessageText with markdown/html
                    var txtRes = await TryEditText(textToSend, pMode);
                    if (txtRes.Success) return (true, null);

                    if (textToSend != text)
                    {
                        var txtResHtml = await TryEditText(text, parseMode);
                        if (txtResHtml.Success) return (true, null);
                    }

                    // 2nd: try editMessageCaption in case message was media
                    var capRes = await TryEditCaption(textToSend, pMode);
                    if (capRes.Success) return (true, null);

                    if (textToSend != text)
                    {
                        var capResHtml = await TryEditCaption(text, parseMode);
                        if (capResHtml.Success) return (true, null);
                    }

                    return txtRes;
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<bool> EditMessageTextAsync(string targetId, int messageId, string text, string parseMode = "html", Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null)
        {
            var (success, _) = await EditMessageTextAsync(targetId, messageId.ToString(), text, parseMode, replyMarkup);
            return success;
        }

        public async Task<(bool Success, string? MessageId)> SendPhotoAsync(string targetId, Stream photoStream, string fileName, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            if (originalMessage != null)
            {
                caption = Application.Utils.TelegramMarkdownFormatter.GetMarkdownText(originalMessage);
                parseMode = null;
            }

            var content = new MultipartFormDataContent();
            content.Add(new StringContent(targetId), "chat_id");
            content.Add(new StringContent(caption), "caption");
            if (!string.IsNullOrEmpty(parseMode)) content.Add(new StringContent(parseMode), "parse_mode");
            if (!string.IsNullOrEmpty(replyToMessageId)) content.Add(new StringContent(replyToMessageId), "reply_to_message_id");
            
            var streamContent = new StreamContent(photoStream);
            content.Add(streamContent, "photo", fileName);

            var response = await _httpClient.PostAsync($"https://tapi.bale.ai/bot{_botToken}/sendPhoto", content);
            return await CheckResponseAsync(response);
        }

        public async Task<(bool Success, string? MessageId)> SendVideoAsync(string targetId, Stream videoStream, string fileName, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            if (originalMessage != null)
            {
                caption = Application.Utils.TelegramMarkdownFormatter.GetMarkdownText(originalMessage);
                parseMode = null;
            }

            var content = new MultipartFormDataContent();
            content.Add(new StringContent(targetId), "chat_id");
            content.Add(new StringContent(caption), "caption");
            if (!string.IsNullOrEmpty(parseMode)) content.Add(new StringContent(parseMode), "parse_mode");
            if (!string.IsNullOrEmpty(replyToMessageId)) content.Add(new StringContent(replyToMessageId), "reply_to_message_id");
            
            var streamContent = new StreamContent(videoStream);
            content.Add(streamContent, "video", fileName);

            var response = await _httpClient.PostAsync($"https://tapi.bale.ai/bot{_botToken}/sendVideo", content);
            return await CheckResponseAsync(response);
        }

        public async Task<(bool Success, string? MessageId)> SendDocumentAsync(string targetId, Stream docStream, string fileName, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            if (originalMessage != null)
            {
                caption = Application.Utils.TelegramMarkdownFormatter.GetMarkdownText(originalMessage);
                parseMode = null;
            }

            var content = new MultipartFormDataContent();
            content.Add(new StringContent(targetId), "chat_id");
            content.Add(new StringContent(caption), "caption");
            if (!string.IsNullOrEmpty(parseMode)) content.Add(new StringContent(parseMode), "parse_mode");
            if (!string.IsNullOrEmpty(replyToMessageId)) content.Add(new StringContent(replyToMessageId), "reply_to_message_id");
            
            var streamContent = new StreamContent(docStream);
            content.Add(streamContent, "document", fileName);

            var response = await _httpClient.PostAsync($"https://tapi.bale.ai/bot{_botToken}/sendDocument", content);
            return await CheckResponseAsync(response);
        }

        public async Task<(bool Success, string? MessageId)> SendMediaGroupAsync(string targetId, System.Collections.Generic.IEnumerable<Stream> mediaStreams, System.Collections.Generic.IEnumerable<string> fileNames, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            if (originalMessage != null)
            {
                caption = Application.Utils.TelegramMarkdownFormatter.GetMarkdownText(originalMessage);
                parseMode = null;
            }

            // Test undocumented sendMediaGroup for Bale
            try 
            {
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(targetId), "chat_id");
                if (!string.IsNullOrEmpty(replyToMessageId)) content.Add(new StringContent(replyToMessageId), "reply_to_message_id");
                
                var mediaArray = new System.Collections.Generic.List<object>();
                using var namesEnum = fileNames.GetEnumerator();
                int index = 0;
                
                foreach (var stream in mediaStreams)
                {
                    namesEnum.MoveNext();
                    var name = namesEnum.Current ?? "photo.jpg";
                    bool isVideo = name.EndsWith(".mp4", System.StringComparison.OrdinalIgnoreCase);
                    
                    var mediaObj = new { 
                        type = isVideo ? "video" : "photo", 
                        media = $"attach://file{index}", 
                        caption = index == 0 ? caption : "", 
                        parse_mode = parseMode 
                    };
                    mediaArray.Add(mediaObj);
                    
                    if (stream.CanSeek) stream.Position = 0;
                    var streamContent = new StreamContent(stream);
                    content.Add(streamContent, $"file{index}", name);
                    index++;
                }
                
                content.Add(new StringContent(System.Text.Json.JsonSerializer.Serialize(mediaArray)), "media");

                var response = await _httpClient.PostAsync($"https://tapi.bale.ai/bot{_botToken}/sendMediaGroup", content);
                var res = await CheckResponseAsync(response);
                if (res.Success && res.MessageId != null) return res;
                if (res.Success)
                {
                    try
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("result", out var resultArr) && resultArr.ValueKind == System.Text.Json.JsonValueKind.Array && resultArr.GetArrayLength() > 0)
                        {
                            var firstMsg = resultArr[0];
                            if (firstMsg.TryGetProperty("message_id", out var midProp))
                            {
                                return (true, midProp.ToString());
                            }
                        }
                    }
                    catch { }
                    return (true, null);
                }
            }
            catch { }

            // Reset streams for fallback
            foreach (var stream in mediaStreams)
            {
                if (stream.CanSeek) stream.Position = 0;
            }

            // Fallback: send as individual photos/videos.
            using var namesEnumerator = fileNames.GetEnumerator();
            bool allSuccess = true;
            string? firstMessageId = null;
            foreach (var stream in mediaStreams)
            {
                namesEnumerator.MoveNext();
                var name = namesEnumerator.Current ?? "photo.jpg";
                bool isVideo = name.EndsWith(".mp4");
                
                (bool success, string? msgId) res = (false, null);
                if (isVideo) res = await SendVideoAsync(targetId, stream, name, caption, parseMode, originalMessage, replyToMessageId);
                else res = await SendPhotoAsync(targetId, stream, name, caption, parseMode, originalMessage, replyToMessageId);
                
                caption = ""; // Only send caption on the first item
                originalMessage = null; // Only format caption on first item
                replyToMessageId = null; // Only reply on first item
                if (!res.success) allSuccess = false;
                if (firstMessageId == null && res.msgId != null) firstMessageId = res.msgId;
            }
            return (allSuccess, firstMessageId);
        }
    }
}








