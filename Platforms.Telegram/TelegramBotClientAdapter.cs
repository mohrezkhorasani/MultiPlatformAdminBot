using Application.Interfaces.IInfrastructure;
using System.IO;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Platforms.Telegram
{
    public class TelegramBotClientAdapter : IBotPlatformClient
    {
        public string PlatformName => "Telegram";
        public ITelegramBotClient Client { get; }

        public TelegramBotClientAdapter(Application.Interfaces.IAppDbContext dbContext, System.Net.Http.IHttpClientFactory httpClientFactory)
        {
            var tokenEntity = System.Linq.Enumerable.FirstOrDefault(dbContext.BotTokenTBLs, t => t.PlatformName == "Telegram");
            string token = tokenEntity?.TokenValue ?? "TELEGRAM_BOT_TOKEN_PLACEHOLDER";
            var httpClient = httpClientFactory.CreateClient("TelegramProxyClient");
            Client = new TelegramBotClient(token, httpClient);

            // Register Bot Commands List for Telegram UI (AutoComplete / menu)
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var commands = new[]
                    {
                        new BotCommand { Command = "help", Description = "راهنمای دستورات ربات" },
                        new BotCommand { Command = "mode", Description = "مشاهده وضعیت فعلی (دیباگ یا پروداکشن)" },
                        new BotCommand { Command = "change_mode", Description = "تغییر وضعیت سیستم + عکس و اسم ربات" },
                        new BotCommand { Command = "mychannels", Description = "مشاهده لیست کانال‌های فعال شما" },
                        new BotCommand { Command = "newchannel", Description = "راهنمای ثبت کانال جدید" },
                        new BotCommand { Command = "stats", Description = "گزارش آمار پست‌های پروداکشن شما" },
                        new BotCommand { Command = "cache", Description = "مشاهده وضعیت کش‌های فعال (ادمین اصلی)" },
                        new BotCommand { Command = "clear_cache", Description = "پاکسازی کش سیستم (ادمین اصلی)" }
                    };
                    await Client.SetMyCommandsAsync(commands);
                }
                catch { }
            });
        }

        public async Task<(bool Success, string? MessageId)> SendTextMessageAsync(string targetId, string text, string parseMode = "html", global::Telegram.Bot.Types.Message originalMessage = null, global::Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null, string? replyToMessageId = null)
        {
            int? parsedId = null; if (int.TryParse(replyToMessageId, out int pid)) parsedId = pid; var msg = await Client.SendTextMessageAsync(targetId, text, parseMode: ParseMode.Html, replyMarkup: replyMarkup, replyToMessageId: parsedId);
            return (true, msg.MessageId.ToString());
        }

        public async Task<(bool Success, string? Error)> EditMessageTextAsync(string targetId, string messageId, string text, string parseMode = "html", global::Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null, global::Telegram.Bot.Types.Message originalMessage = null)
        {
            if (!int.TryParse(messageId, out int mid)) return (false, "شناسه پیام تلگرام نامعتبر است");
            
            bool isMedia = originalMessage != null && originalMessage.Type != global::Telegram.Bot.Types.Enums.MessageType.Text;

            if (isMedia)
            {
                try
                {
                    await Client.EditMessageCaptionAsync(targetId, mid, caption: text, parseMode: ParseMode.Html, replyMarkup: (global::Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)replyMarkup);
                    return (true, null);
                }
                catch (Exception exCap)
                {
                    try
                    {
                        await Client.EditMessageTextAsync(targetId, mid, text, parseMode: ParseMode.Html, replyMarkup: (global::Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)replyMarkup);
                        return (true, null);
                    }
                    catch
                    {
                        return (false, exCap.Message);
                    }
                }
            }
            else
            {
                try
                {
                    await Client.EditMessageTextAsync(targetId, mid, text, parseMode: ParseMode.Html, replyMarkup: (global::Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)replyMarkup);
                    return (true, null);
                }
                catch (Exception exText)
                {
                    try
                    {
                        await Client.EditMessageCaptionAsync(targetId, mid, caption: text, parseMode: ParseMode.Html, replyMarkup: (global::Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)replyMarkup);
                        return (true, null);
                    }
                    catch
                    {
                        return (false, exText.Message);
                    }
                }
            }
        }

        public async Task<bool> EditMessageTextAsync(string targetId, int messageId, string text, string parseMode = "html", global::Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null)
        {
            var (success, _) = await EditMessageTextAsync(targetId, messageId.ToString(), text, parseMode, replyMarkup);
            return success;
        }

        public async Task<(bool Success, string? MessageId)> SendPhotoAsync(string targetId, Stream photoStream, string fileName, string caption, string parseMode = "html", global::Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            int? parsedId = null; if (int.TryParse(replyToMessageId, out int pid)) parsedId = pid; var msg = await Client.SendPhotoAsync(targetId, new InputFileStream(photoStream, fileName), caption: caption, parseMode: ParseMode.Html, replyToMessageId: parsedId);
            return (true, msg.MessageId.ToString());
        }

        public async Task<(bool Success, string? MessageId)> SendVideoAsync(string targetId, Stream videoStream, string fileName, string caption, string parseMode = "html", global::Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            int? parsedId = null; if (int.TryParse(replyToMessageId, out int pid)) parsedId = pid; var msg = await Client.SendVideoAsync(targetId, new InputFileStream(videoStream, fileName), caption: caption, parseMode: ParseMode.Html, replyToMessageId: parsedId);
            return (true, msg.MessageId.ToString());
        }

        public async Task<(bool Success, string? MessageId)> SendDocumentAsync(string targetId, Stream docStream, string fileName, string caption, string parseMode = "html", global::Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            int? parsedId = null; if (int.TryParse(replyToMessageId, out int pid)) parsedId = pid; var msg = await Client.SendDocumentAsync(targetId, new InputFileStream(docStream, fileName), caption: caption, parseMode: ParseMode.Html, replyToMessageId: parsedId);
            return (true, msg.MessageId.ToString());
        }

        public async Task<(bool Success, string? MessageId)> SendMediaGroupAsync(string targetId, System.Collections.Generic.IEnumerable<Stream> mediaStreams, System.Collections.Generic.IEnumerable<string> fileNames, string caption, string parseMode = "html", global::Telegram.Bot.Types.Message originalMessage = null, string? replyToMessageId = null)
        {
            var media = new System.Collections.Generic.List<IAlbumInputMedia>();
            using var namesEnumerator = fileNames.GetEnumerator();
            
            bool isFirst = true;
            foreach (var stream in mediaStreams)
            {
                namesEnumerator.MoveNext();
                var originalName = namesEnumerator.Current ?? "photo.jpg";
                var name = $"{System.Guid.NewGuid().ToString().Substring(0,8)}_{originalName}";
                
                IAlbumInputMedia inputMedia;
                if (originalName.EndsWith(".mp4", System.StringComparison.OrdinalIgnoreCase))
                {
                    var videoMedia = new InputMediaVideo(new InputFileStream(stream, name));
                    if (isFirst) { videoMedia.Caption = caption; videoMedia.ParseMode = ParseMode.Html; }
                    inputMedia = videoMedia;
                }
                else
                {
                    var photoMedia = new InputMediaPhoto(new InputFileStream(stream, name));
                    if (isFirst) { photoMedia.Caption = caption; photoMedia.ParseMode = ParseMode.Html; }
                    inputMedia = photoMedia;
                }
                
                isFirst = false;
                media.Add(inputMedia);
            }
            
            int? parsedId = null; if (int.TryParse(replyToMessageId, out int pid)) parsedId = pid; var msgs = await Client.SendMediaGroupAsync(targetId, media, replyToMessageId: parsedId);
            return (true, msgs?[0]?.MessageId.ToString());
        }
    }
}



