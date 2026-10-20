using Application.Interfaces.IInfrastructure;
using System.IO;
using System.Threading.Tasks;

namespace Platforms.Rubika
{
    public class RubikaBotClient : IBotPlatformClient
    {
        public string PlatformName => "Rubika";
        
        private readonly HttpClient _httpClient;
        private readonly string _botToken;
        private readonly string _baseUrl = "https://botapi.rubika.ir/v3";
        // Cache resolved @username -> numeric chat_id to avoid repeated API calls
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _chatIdCache
            = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();

        public RubikaBotClient(HttpClient httpClient, Application.Interfaces.IAppDbContext dbContext)
        {
            _httpClient = httpClient;
            var tokenEntity = System.Linq.Enumerable.FirstOrDefault(dbContext.BotTokenTBLs, t => t.PlatformName == "Rubika");
            _botToken = tokenEntity?.TokenValue ?? "RUBIKA_BOT_TOKEN_PLACEHOLDER";
            // Log partial token to verify the correct token is being used on server
            var partial = (_botToken?.Length ?? 0) > 8 ? _botToken.Substring(0, 4) + "..." + _botToken.Substring(_botToken.Length - 4) : _botToken;
            LogRubika($"RubikaBotClient initialized. Token={partial}");
        }

        private void LogRubika(string message)
        {
            try
            {
                var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }
                
                // Format: MM-dd-yyyy.txt or dd-MM-yyyy.txt as requested e.g. 10-03-2026.txt
                var fileName = $"{System.DateTime.Now:MM-dd-yyyy}.txt";
                var filePath = Path.Combine(logsDir, fileName);
                
                var logLine = $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{System.Environment.NewLine}";
                File.AppendAllText(filePath, logLine);
            }
            catch { }
        }

        private async Task<(bool Success, string Json)> PostAsync(string method, object data)
        {
            var reqJson = System.Text.Json.JsonSerializer.Serialize(data);
            LogRubika($"REQUEST [{method}]: {reqJson}");

            var content = new StringContent(reqJson, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/{_botToken}/{method}", content);
            if (!response.IsSuccessStatusCode)
            {
                LogRubika($"RESPONSE [{method}] HTTP Failed ({response.StatusCode})");
                return (false, "");
            }
            var json = await response.Content.ReadAsStringAsync();
            LogRubika($"RESPONSE [{method}]: {json}");

            bool isOk = IsSuccessResponse(json);
            return (isOk, json);
        }

        private string AppendLinksToText(string text, Telegram.Bot.Types.Message originalMessage)
        {
            var entities = originalMessage.Entities ?? originalMessage.CaptionEntities;
            if (entities != null)
            {
                var links = new System.Collections.Generic.List<string>();
                foreach (var entity in entities)
                {
                    if (entity.Type == Telegram.Bot.Types.Enums.MessageEntityType.TextLink && !string.IsNullOrEmpty(entity.Url))
                    {
                        if (!links.Contains(entity.Url))
                            links.Add(entity.Url);
                    }
                }
                if (links.Count > 0)
                {
                    text += "\n\n🔗 لینک‌ها:\n" + string.Join("\n", links);
                }
            }
            return text;
        }

        public async Task<(bool Success, string? MessageId)> SendTextMessageAsync(string targetId, string text, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null, string? replyToMessageId = null)
        {
            object metadata = null;
            if (originalMessage != null)
            {
                var plainOriginal = Application.Utils.TelegramRubikaFormatter.GetPlainText(originalMessage);
                var plainFull = System.Text.RegularExpressions.Regex.Replace(text ?? "", "<.*?>", string.Empty);
                var footerSuffix = plainFull.StartsWith(plainOriginal, System.StringComparison.Ordinal)
                    ? plainFull.Substring(plainOriginal.Length)
                    : "";
                text = plainOriginal + footerSuffix;
                metadata = Application.Utils.TelegramRubikaFormatter.GetRubikaMetadata(originalMessage);
            }
            else
            {
                text = System.Text.RegularExpressions.Regex.Replace(text ?? "", "<.*?>", string.Empty);
            }

            object payload;
            if (metadata != null)
            {
                payload = replyToMessageId != null
                    ? new { chat_id = targetId, text = text, metadata = metadata, reply_to_message_id = replyToMessageId }
                    : new { chat_id = targetId, text = text, metadata = metadata };
            }
            else
            {
                payload = replyToMessageId != null
                    ? new { chat_id = targetId, text = text, reply_to_message_id = replyToMessageId }
                    : new { chat_id = targetId, text = text };
            }

            var (ok, json) = await PostAsync("sendMessage", payload);
            string? msgId = null;
            if (ok && !string.IsNullOrEmpty(json))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("message_id", out var midProp))
                {
                    msgId = midProp.ToString();
                }
            }

            return (ok, msgId);
        }

        public async Task<(bool Success, string? Error)> EditMessageTextAsync(string targetId, string messageId, string text, string parseMode = "html", Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null, Telegram.Bot.Types.Message originalMessage = null)
        {
            try
            {
                object metadata = null;
                if (originalMessage != null)
                {
                    var plainOriginal = Application.Utils.TelegramRubikaFormatter.GetPlainText(originalMessage);
                    var plainFull = System.Text.RegularExpressions.Regex.Replace(text ?? "", "<.*?>", string.Empty);
                    var footerSuffix = plainFull.StartsWith(plainOriginal, System.StringComparison.Ordinal)
                        ? plainFull.Substring(plainOriginal.Length)
                        : "";
                    text = plainOriginal + footerSuffix;
                    metadata = Application.Utils.TelegramRubikaFormatter.GetRubikaMetadata(originalMessage);
                }
                else
                {
                    text = System.Text.RegularExpressions.Regex.Replace(text ?? "", "<.*?>", string.Empty);
                }

                string lastError = "";

                async Task<(bool ok, string err)> TryEdit(string chatId, object meta)
                {
                    object payload = meta != null
                        ? new { chat_id = chatId, message_id = messageId, text = text, metadata = meta }
                        : new { chat_id = chatId, message_id = messageId, text = text };

                    var (ok, json) = await PostAsync("editMessageText", payload);
                    if (ok) return (true, "");

                    string errDesc = json;
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("status_det", out var sdProp)) errDesc = sdProp.GetString() ?? json;
                        else if (doc.RootElement.TryGetProperty("description", out var descProp)) errDesc = descProp.GetString() ?? json;
                    }
                    catch { }

                    return (false, errDesc);
                }

                // 1st attempt: targetId with metadata (if available)
                var (success, err) = await TryEdit(targetId, metadata);
                lastError = err;

                // If failed and metadata was provided, retry without metadata
                if (!success && metadata != null)
                {
                    LogRubika($"editMessageText failed with metadata for {targetId}, retrying without metadata...");
                    var resNoMeta = await TryEdit(targetId, null);
                    success = resNoMeta.ok;
                    if (!success) lastError = resNoMeta.err;
                }

                // If failed and targetId starts with @, resolve to numeric chat_id and retry
                if (!success && targetId.StartsWith("@"))
                {
                    LogRubika($"editMessageText failed for {targetId}, resolving to numeric chat_id...");
                    var resolvedId = _chatIdCache.GetOrAdd(targetId, _ => targetId);
                    if (resolvedId == targetId)
                    {
                        resolvedId = await ResolveChatIdAsync(targetId);
                        if (resolvedId != targetId)
                            _chatIdCache[targetId] = resolvedId;
                    }

                    if (resolvedId != targetId)
                    {
                        var resResolved = await TryEdit(resolvedId, metadata);
                        success = resResolved.ok;
                        if (!success)
                        {
                            lastError = resResolved.err;
                            if (metadata != null)
                            {
                                var resResolvedNoMeta = await TryEdit(resolvedId, null);
                                success = resResolvedNoMeta.ok;
                                if (!success) lastError = resResolvedNoMeta.err;
                            }
                        }
                    }
                }

                return (success, success ? null : lastError);
            }
            catch (Exception ex)
            {
                LogRubika($"EditMessageTextAsync exception: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<bool> EditMessageTextAsync(string targetId, int messageId, string text, string parseMode = "html", Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null)
        {
            var (success, _) = await EditMessageTextAsync(targetId, messageId.ToString(), text, parseMode, replyMarkup);
            return success;
        }

        private async Task<string> RequestUploadUrl(string type)
        {
            var req = new { type = type };
            var (ok, responseJson) = await PostAsync("requestSendFile", req);
            if (!ok || string.IsNullOrEmpty(responseJson)) throw new Exception("Rubika requestSendFile failed");
            
            using var doc = System.Text.Json.JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("upload_url", out var uploadUrl))
            {
                return uploadUrl.GetString();
            }
            if (root.TryGetProperty("upload_url", out var uploadUrlDirect))
            {
                return uploadUrlDirect.GetString();
            }
            throw new Exception("upload_url not found in Rubika response: " + responseJson);
        }

        private static string GetMimeType(string fileName)
        {
            var ext = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".mp4" => "video/mp4",
                ".mov" => "video/quicktime",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }

        private async Task<string> UploadFileAsync(string uploadUrl, Stream stream, string fileName)
        {
            LogRubika($"UPLOADING FILE [{fileName}] to {uploadUrl}");

            // Read stream into memory so we can set Content-Length (required by some Rubika servers)
            byte[] fileBytes;
            if (stream is MemoryStream ms)
                fileBytes = ms.ToArray();
            else
            {
                using var memStream = new MemoryStream();
                await stream.CopyToAsync(memStream);
                fileBytes = memStream.ToArray();
            }

            using var formData = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(fileBytes);
            byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetMimeType(fileName));
            formData.Add(byteContent, "file", fileName);

            var response = await _httpClient.PostAsync(uploadUrl, formData);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync();
                LogRubika($"UPLOAD FILE HTTP status failed ({response.StatusCode}): {errBody}");
                throw new Exception("Rubika UploadFile HTTP status failed");
            }
            var responseJson = await response.Content.ReadAsStringAsync();
            LogRubika($"UPLOAD FILE RESPONSE: {responseJson}");

            using var doc = System.Text.Json.JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("file_id", out var fileId))
            {
                return fileId.GetString();
            }
            if (root.TryGetProperty("file_id", out var fileIdDirect))
            {
                return fileIdDirect.GetString();
            }
            throw new Exception("file_id not found in Rubika upload response: " + responseJson);
        }

        // Resolve @username to actual channel_id if needed, as sendFile may reject usernames
        private async Task<string> ResolveChatIdAsync(string chatId)
        {
            if (!chatId.StartsWith("@"))
                return chatId;

            try
            {
                var (ok, json) = await PostAsync("getChat", new { chat_id = chatId });
                if (!ok || string.IsNullOrEmpty(json)) return chatId;

                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Try data.chat.chat_id
                if (root.TryGetProperty("data", out var data))
                {
                    if (data.TryGetProperty("chat", out var chat) && chat.TryGetProperty("chat_id", out var cid))
                    {
                        var resolved = cid.GetString();
                        LogRubika($"ResolveChatId: {chatId} => {resolved}");
                        return resolved ?? chatId;
                    }
                }
                // Try chat.chat_id directly
                if (root.TryGetProperty("chat", out var chatDirect) && chatDirect.TryGetProperty("chat_id", out var cidDirect))
                {
                    var resolved = cidDirect.GetString();
                    LogRubika($"ResolveChatId: {chatId} => {resolved}");
                    return resolved ?? chatId;
                }
            }
            catch (Exception ex)
            {
                LogRubika($"ResolveChatId failed for {chatId}: {ex.Message}");
            }

            return chatId;
        }

        // After successful upload, get the public download URL for fallback
        private async Task<string> GetDownloadUrlAsync(string fileId)
        {
            try
            {
                var (ok, json) = await PostAsync("getFile", new { file_id = fileId });
                if (!ok || string.IsNullOrEmpty(json)) return null;
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var data) && data.TryGetProperty("download_url", out var url))
                    return url.GetString();
                if (root.TryGetProperty("download_url", out var urlDirect))
                    return urlDirect.GetString();
            }
            catch (Exception ex)
            {
                LogRubika($"GetDownloadUrl failed: {ex.Message}");
            }
            return null;
        }

        private async Task<(bool Success, string? MessageId)> SendFileInternalAsync(string targetId, Stream stream, string fileName, string caption, string fileTypeEnum, Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            var uploadUrl = await RequestUploadUrl(fileTypeEnum);
            var fileId = await UploadFileAsync(uploadUrl, stream, fileName);

            object metadata = null;
            if (originalMessage != null)
            {
                var plainOriginal = Application.Utils.TelegramRubikaFormatter.GetPlainText(originalMessage);
                var plainFull = System.Text.RegularExpressions.Regex.Replace(caption ?? "", "<.*?>", string.Empty);
                var footerSuffix = plainFull.StartsWith(plainOriginal, System.StringComparison.Ordinal)
                    ? plainFull.Substring(plainOriginal.Length)
                    : "";
                caption = plainOriginal + footerSuffix;
                metadata = Application.Utils.TelegramRubikaFormatter.GetRubikaMetadata(originalMessage);
            }
            else if (!string.IsNullOrEmpty(caption))
            {
                caption = System.Text.RegularExpressions.Regex.Replace(caption, "<.*?>", string.Empty);
            }

            // Helper: try sendFile with a given chat_id
            async Task<(bool, string?)> TrySendFile(string chatId)
            {
                object payload;
                if (metadata != null)
                {
                    payload = replyToMessageId != null 
                        ? new { chat_id = chatId, file_id = fileId, text = caption, metadata = metadata, reply_to_message_id = replyToMessageId }
                        : new { chat_id = chatId, file_id = fileId, text = caption, metadata = metadata };
                }
                else
                {
                    payload = replyToMessageId != null
                        ? new { chat_id = chatId, file_id = fileId, text = caption ?? "", reply_to_message_id = replyToMessageId }
                        : new { chat_id = chatId, file_id = fileId, text = caption ?? "" };
                }

                var (ok2, json) = await PostAsync("sendFile", payload);
                string? msgId = null;
                if (ok2 && !string.IsNullOrEmpty(json))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("message_id", out var midProp))
                    {
                        msgId = midProp.ToString();
                    }
                }
                return (ok2, msgId);
            }

            // 1st attempt: use the original chatId
            var res = await TrySendFile(targetId);

            // 2nd attempt: resolve @username to numeric chat_id and retry directly
            string resolvedId = targetId;
            if (!res.Item1 && targetId.StartsWith("@"))
            {
                LogRubika($"sendFile failed for {targetId}, resolving to numeric chat_id...");
                resolvedId = _chatIdCache.GetOrAdd(targetId, _ => targetId);
                if (resolvedId == targetId)
                {
                    resolvedId = await ResolveChatIdAsync(targetId);
                    if (resolvedId != targetId)
                        _chatIdCache[targetId] = resolvedId;
                }

                if (resolvedId != targetId)
                    res = await TrySendFile(resolvedId);
            }

            return res;
        }

        public async Task<(bool Success, string? MessageId)> SendPhotoAsync(string targetId, Stream photoStream, string fileName, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            return await SendFileInternalAsync(targetId, photoStream, fileName, caption, "Image", originalMessage, replyToMessageId);
        }

        public async Task<(bool Success, string? MessageId)> SendVideoAsync(string targetId, Stream videoStream, string fileName, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            return await SendFileInternalAsync(targetId, videoStream, fileName, caption, "Video", originalMessage, replyToMessageId);
        }

        public async Task<(bool Success, string? MessageId)> SendDocumentAsync(string targetId, Stream docStream, string fileName, string caption, string parseMode = "html", Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            return await SendFileInternalAsync(targetId, docStream, fileName, caption, "File", originalMessage, replyToMessageId);
        }

        public async Task<(bool Success, string? MessageId)> SendMediaGroupAsync(
          string targetId,
          System.Collections.Generic.IEnumerable<Stream> mediaStreams,
          System.Collections.Generic.IEnumerable<string> fileNames,
          string caption,
          string parseMode = "html",
          Telegram.Bot.Types.Message originalMessage = null,
          string? replyToMessageId = null)
        {
            using var streamsEnum = mediaStreams.GetEnumerator();
            using var namesEnum = fileNames.GetEnumerator();

            // As requested: Only send the VERY FIRST item in the album for Rubika
            if (streamsEnum.MoveNext() && namesEnum.MoveNext())
            {
                var stream = streamsEnum.Current;
                var name = namesEnum.Current ?? "photo.jpg";
                bool isVideo = name.EndsWith(".mp4", System.StringComparison.OrdinalIgnoreCase);

                LogRubika($"SendMediaGroupAsync: Sending ONLY FIRST item ({name}) to Rubika chat {targetId}");

                if (isVideo)
                    return await SendVideoAsync(targetId, stream, name, caption, parseMode, originalMessage, replyToMessageId);
                else
                    return await SendPhotoAsync(targetId, stream, name, caption, parseMode, originalMessage, replyToMessageId);
            }

            return (false, null);
        }
        private static bool IsSuccessResponse(object result)
        {
            if (result == null) return false;

            if (result is string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return false;
                
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(s);
                    if (doc.RootElement.TryGetProperty("status", out var statusProp))
                    {
                        var statusStr = statusProp.GetString();
                        if (statusStr != null && statusStr.Equals("OK", System.StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                        return false; // If status exists but is not OK, it's an error
                    }
                    if (doc.RootElement.TryGetProperty("ok", out var okProp))
                    {
                        return okProp.GetBoolean();
                    }
                }
                catch
                {
                    // Fallback to text matching if not valid JSON
                    if (s.Contains("ERROR", System.StringComparison.OrdinalIgnoreCase) || 
                        s.Contains("\"ok\":false", System.StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("\"status\":\"failed\"", System.StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }

            return true;
        }
    }
}








