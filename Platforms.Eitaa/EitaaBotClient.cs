using Application.Interfaces.IInfrastructure;
using MultiPlatformAdminBot.Common;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Platforms.Eitaa
{
    public class EitaaBotClient : IBotPlatformClient
    {
        public string PlatformName => "Eitaa";
        private readonly HttpClient _httpClient;
        private readonly string _botToken;

        public EitaaBotClient(HttpClient httpClient, Application.Interfaces.IAppDbContext dbContext)
        {
            _httpClient = httpClient;
            var tokenEntity = System.Linq.Enumerable.FirstOrDefault(dbContext.BotTokenTBLs, t => t.PlatformName == "Eitaa");
            _botToken = tokenEntity?.TokenValue ?? "EITAA_BOT_TOKEN_PLACEHOLDER";
        }

        private static async Task<(bool Success, string? MessageId)> CheckResponseAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode) return (false, null);
            try
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                bool isSuccess = false; if (doc.RootElement.TryGetProperty("ok", out var okProp)) isSuccess = okProp.ValueKind == System.Text.Json.JsonValueKind.True; else if (doc.RootElement.TryGetProperty("success", out var successProp)) isSuccess = successProp.ValueKind == System.Text.Json.JsonValueKind.True; string? msgId = null; if (isSuccess && doc.RootElement.TryGetProperty("message_id", out var midProp)) msgId = midProp.ToString(); else if (isSuccess && doc.RootElement.TryGetProperty("result", out var resProp) && resProp.TryGetProperty("message_id", out var rmidProp)) msgId = rmidProp.ToString(); return (isSuccess, msgId);
            }
            catch
            {
                return (response.IsSuccessStatusCode, null);
            }
        }

        public async Task<(bool Success, string? MessageId)> SendTextMessageAsync(string targetId, string text, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null, string? replyToMessageId = null)
        {
            if (parseMode == "html" && !string.IsNullOrEmpty(text))
            {
                var lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    int firstEndTag = lines[i].IndexOf("</a>");
                    if (firstEndTag != -1)
                    {
                        string before = lines[i].Substring(0, firstEndTag + 4);
                        string after = lines[i].Substring(firstEndTag + 4);
                        after = System.Text.RegularExpressions.Regex.Replace(after, @"<a[^>]*>|<\/a>", "");
                        lines[i] = before + after;
                    }
                }
                text = string.Join("\n", lines);
            }

            var url = $"https://eitaayar.ir/api/{_botToken}/sendMessage?chat_id={targetId}&text={System.Uri.EscapeDataString(text)}";
            if (!string.IsNullOrEmpty(parseMode))
            {
                url += $"&date=0&parse_mode={parseMode}&viewCountForDelete=";
            }
            var response = await _httpClient.GetAsync(url);
            return await CheckResponseAsync(response);
        }

        public async Task<(bool Success, string? Error)> EditMessageTextAsync(string targetId, string messageId, string text, string parseMode = "html", Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null, Telegram.Bot.Types.Message originalMessage = null)
        {
            try
            {
                if (parseMode == "html" && !string.IsNullOrEmpty(text))
                {
                    var lines = text.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        int firstEndTag = lines[i].IndexOf("</a>");
                        if (firstEndTag != -1)
                        {
                            string before = lines[i].Substring(0, firstEndTag + 4);
                            string after = lines[i].Substring(firstEndTag + 4);
                            after = System.Text.RegularExpressions.Regex.Replace(after, @"<a[^>]*>|<\/a>", "");
                            lines[i] = before + after;
                        }
                    }
                    text = string.Join("\n", lines);
                }

                var url = $"https://eitaayar.ir/api/{_botToken}/editMessageText?chat_id={targetId}&message_id={messageId}&text={System.Uri.EscapeDataString(text)}";
                if (!string.IsNullOrEmpty(parseMode))
                {
                    url += $"&parse_mode={parseMode}";
                }
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        bool isSuccess = false;
                        if (doc.RootElement.TryGetProperty("ok", out var okProp)) isSuccess = okProp.ValueKind == System.Text.Json.JsonValueKind.True;
                        else if (doc.RootElement.TryGetProperty("success", out var successProp)) isSuccess = successProp.ValueKind == System.Text.Json.JsonValueKind.True;

                        if (isSuccess) return (true, null);

                        string? errDesc = null;
                        if (doc.RootElement.TryGetProperty("description", out var descProp)) errDesc = descProp.GetString();
                        else if (doc.RootElement.TryGetProperty("message", out var msgProp)) errDesc = msgProp.GetString();

                        return (false, errDesc ?? json);
                    }
                    catch
                    {
                        return (true, null);
                    }
                }
                else
                {
                    return (false, $"HTTP {(int)response.StatusCode}: {json}");
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
            if (parseMode == "html" && !string.IsNullOrEmpty(caption))
            {
                var lines = caption.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    int firstEndTag = lines[i].IndexOf("</a>");
                    if (firstEndTag != -1)
                    {
                        string before = lines[i].Substring(0, firstEndTag + 4);
                        string after = lines[i].Substring(firstEndTag + 4);
                        after = System.Text.RegularExpressions.Regex.Replace(after, @"<a[^>]*>|<\/a>", "");
                        lines[i] = before + after;
                    }
                }
                caption = string.Join("\n", lines);
            }

            var content = new MultipartFormDataContent();
            content.Add(new StringContent(targetId), "chat_id");
            content.Add(new StringContent(caption), "caption");
            if (!string.IsNullOrEmpty(parseMode)) content.Add(new StringContent(parseMode), "parse_mode");
            if (!string.IsNullOrEmpty(replyToMessageId)) content.Add(new StringContent(replyToMessageId), "reply_to_message_id");

            var streamContent = new StreamContent(photoStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync($"https://eitaayar.ir/api/{_botToken}/sendFile", content);
            return await CheckResponseAsync(response);
        }

        public async Task<(bool Success, string? MessageId)> SendVideoAsync(string targetId, Stream videoStream, string fileName, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            if (parseMode == "html" && !string.IsNullOrEmpty(caption))
            {
                var lines = caption.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    int firstEndTag = lines[i].IndexOf("</a>");
                    if (firstEndTag != -1)
                    {
                        string before = lines[i].Substring(0, firstEndTag + 4);
                        string after = lines[i].Substring(firstEndTag + 4);
                        after = System.Text.RegularExpressions.Regex.Replace(after, @"<a[^>]*>|<\/a>", "");
                        lines[i] = before + after;
                    }
                }
                caption = string.Join("\n", lines);
            }

            var content = new MultipartFormDataContent();
            content.Add(new StringContent(targetId), "chat_id");
            content.Add(new StringContent(caption), "caption");
            if (!string.IsNullOrEmpty(parseMode)) content.Add(new StringContent(parseMode), "parse_mode");
            if (!string.IsNullOrEmpty(replyToMessageId)) content.Add(new StringContent(replyToMessageId), "reply_to_message_id");

            var streamContent = new StreamContent(videoStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync($"https://eitaayar.ir/api/{_botToken}/sendFile", content);
            return await CheckResponseAsync(response);
        }

        public async Task<(bool Success, string? MessageId)> SendDocumentAsync(string targetId, Stream docStream, string fileName, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            if (parseMode == "html" && !string.IsNullOrEmpty(caption))
            {
                var lines = caption.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    int firstEndTag = lines[i].IndexOf("</a>");
                    if (firstEndTag != -1)
                    {
                        string before = lines[i].Substring(0, firstEndTag + 4);
                        string after = lines[i].Substring(firstEndTag + 4);
                        after = System.Text.RegularExpressions.Regex.Replace(after, @"<a[^>]*>|<\/a>", "");
                        lines[i] = before + after;
                    }
                }
                caption = string.Join("\n", lines);
            }

            var content = new MultipartFormDataContent();
            content.Add(new StringContent(targetId), "chat_id");
            content.Add(new StringContent(caption), "caption");
            if (!string.IsNullOrEmpty(parseMode)) content.Add(new StringContent(parseMode), "parse_mode");
            if (!string.IsNullOrEmpty(replyToMessageId)) content.Add(new StringContent(replyToMessageId), "reply_to_message_id");

            var streamContent = new StreamContent(docStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync($"https://eitaayar.ir/api/{_botToken}/sendFile", content);
            return await CheckResponseAsync(response);
        }

        public async Task<(bool Success, string? MessageId)> SendMediaGroupAsync(string targetId, System.Collections.Generic.IEnumerable<Stream> mediaStreams, System.Collections.Generic.IEnumerable<string> fileNames, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            // Eitaa doesn't support true Albums via API yet.
            // As per user request, we only send the first photo/video of the album.
            using var streamsEnum = mediaStreams.GetEnumerator();
            using var namesEnum = fileNames.GetEnumerator();
            
            if (streamsEnum.MoveNext() && namesEnum.MoveNext())
            {
                var stream = streamsEnum.Current;
                var name = namesEnum.Current ?? "photo.jpg";
                bool isVideo = name.EndsWith(".mp4", System.StringComparison.OrdinalIgnoreCase);

                if (isVideo)
                    return await SendVideoAsync(targetId, stream, name, caption, parseMode, originalMessage);
                else
                    return await SendPhotoAsync(targetId, stream, name, caption, parseMode, originalMessage);
            }
            
            return (false, null);
        }
    }
}










