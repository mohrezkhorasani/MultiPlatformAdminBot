using Domain.Entity;
using MultiPlatformAdminBot.Common;
using MultiPlatformAdminBot.Persistence;
using System.Threading.Channels;
using Telegram.Bot;

namespace MultiPlatformAdminBot.CoreMe.Services.Webhook
{
    /// <summary>
    /// Telegram pending-broadcast flow: send_pending / del_pending / cancel_del /
    /// confirm_del callbacks for drafts stored in PendingBroadcastTBL.
    /// </summary>
    public class PendingBroadcastHandler
    {
        private readonly BotDbContext _db;
        private readonly Channel<BroadcastJob> _broadcastChannel;

        public PendingBroadcastHandler(BotDbContext db, Channel<BroadcastJob> broadcastChannel)
        {
            _db = db;
            _broadcastChannel = broadcastChannel;
        }

        public async Task HandleSendAsync(Telegram.Bot.Types.CallbackQuery callback, ITelegramBotClient? tClient)
        {
            try
            {

                if (!long.TryParse(callback.Data.Split('_')[2], out long pid)) return;

                //var p = _db.PendingBroadcastTBLs.FirstOrDefault(x => x.ID == pid);
                var p1 = _db.PendingBroadcastTBLs.Where(e => e.ID == pid);
                var p = p1.FirstOrDefault();
                if (p != null && p.Status == 0)
                {
                    // Claim the pending row atomically so a retried webhook cannot
                    // enqueue the same broadcast twice (status 0 -> 1 is the guard).
                    p.Status = 1;
                    await _db.SaveChangesAsync();

                    var reqs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<global::Application.DTOs.BroadcastMessageRequest>>(p.UpdatesJson);
                    if (reqs != null && reqs.Count > 0)
                    {
                        // Heavy work (media download + multi-platform upload) runs in
                        // the background worker; the webhook acks 200 immediately.
                        await _broadcastChannel.Writer.WriteAsync(
                            new BroadcastJob
                            {
                                Kind = BroadcastJobKind.PendingBroadcast,
                                Requests = reqs
                            });
                    }
                    if (tClient != null) await tClient.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, "✅ پیام جهت ارسال ثبت شد.");
                }
            }
            catch (Exception e)
            {
                Statics.Log(e);
            }
        }

        public async Task HandleDeleteRequestAsync(Telegram.Bot.Types.CallbackQuery callback, ITelegramBotClient? tClient)
        {
            if (!long.TryParse(callback.Data.Split('_')[2], out long pid)) return;

            var p = _db.PendingBroadcastTBLs.FirstOrDefault(x => x.ID == pid);
            if (p != null && p.Status == 0)
            {
                var ik = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
                {
                    new [] {
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("بله، مطمئنم", $"confirm_del_{pid}"),
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("خیر", $"cancel_del_{pid}")
                    }
                });
                if (tClient != null) await tClient.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, "آیا از حذف این پیام اطمینان دارید؟", replyMarkup: ik);
            }
        }

        public async Task HandleCancelDeleteAsync(Telegram.Bot.Types.CallbackQuery callback, ITelegramBotClient? tClient)
        {
            var idStr = callback.Data.Split('_')[2];
            var ik = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
                new [] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("✅ ثبت و ارسال", $"send_pending_{idStr}"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("⏰ زمان‌بندی", $"sched_pending_{idStr}")
                },
                new [] {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("❌ حذف", $"del_pending_{idStr}")
                }
            });
            if (tClient != null) await tClient.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, "پیام شما دریافت شد. آیا مایل به ارسال هستید؟", replyMarkup: ik);
        }

        public async Task HandleScheduleRequestAsync(Telegram.Bot.Types.CallbackQuery callback, ITelegramBotClient? tClient, AdminUserTBL? admin)
        {
            if (!long.TryParse(callback.Data.Split('_')[2], out long pid)) return;

            var p = _db.PendingBroadcastTBLs.FirstOrDefault(x => x.ID == pid);
            if (p != null && p.Status == 0)
            {
                if (admin != null)
                {
                    admin.CurrentRoute = $"WaitScheduleTime_{pid}";
                    await _db.SaveChangesAsync();
                }

                var ik = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
                {
                    new [] {
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("⏰ ۱ ساعت بعد", $"quick_sched_{pid}_1h"),
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("⏰ ۲ ساعت بعد", $"quick_sched_{pid}_2h")
                    },
                    new [] {
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("🌙 امشب ساعت ۲۱:۰۰", $"quick_sched_{pid}_2100"),
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("☀️ فردا ساعت ۱۰:۰۰", $"quick_sched_{pid}_tomorrow10")
                    },
                    new [] {
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("🔙 انصراف", $"cancel_del_{pid}")
                    }
                });

                string help = "⏰ <b>تنظیم زمان ارسال خودکار</b>\n\n" +
                              "می‌توانید یکی از گزینه‌های سریع زیر را انتخاب کنید، یا <b>ساعت دلخواه</b> را بنویسید:\n\n" +
                              "• ارسال ساعت خالی: <code>11:00</code> یا <code>۱۴:۳۰</code>\n" +
                              "• ارسال تاریخ و ساعت شمسی: <code>1405/07/15 11:30</code>";

                if (tClient != null) await tClient.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, help, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: ik);
            }
        }

        public async Task HandleQuickScheduleAsync(Telegram.Bot.Types.CallbackQuery callback, ITelegramBotClient? tClient, AdminUserTBL? admin)
        {
            var parts = callback.Data.Split('_');
            if (parts.Length < 4) return;
            if (!long.TryParse(parts[2], out long pid)) return;
            string option = parts[3];

            var nowUtc = DateTime.UtcNow;
            DateTime targetUtc;

            if (option == "1h") targetUtc = nowUtc.AddHours(1);
            else if (option == "2h") targetUtc = nowUtc.AddHours(2);
            else if (option == "2100")
            {
                var tehranNow = nowUtc.AddHours(3.5);
                var targetTehran = new DateTime(tehranNow.Year, tehranNow.Month, tehranNow.Day, 21, 0, 0);
                if (targetTehran <= tehranNow) targetTehran = targetTehran.AddDays(1);
                targetUtc = targetTehran.AddHours(-3.5);
            }
            else if (option == "tomorrow10")
            {
                var tehranTomorrow = nowUtc.AddHours(3.5).AddDays(1);
                var targetTehran = new DateTime(tehranTomorrow.Year, tehranTomorrow.Month, tehranTomorrow.Day, 10, 0, 0);
                targetUtc = targetTehran.AddHours(-3.5);
            }
            else return;

            await ApplyScheduleAsync(pid, targetUtc, callback.Message.Chat.Id, callback.Message.MessageId, tClient, admin);
        }

        public async Task ApplyScheduleAsync(long pid, DateTime targetUtc, long chatId, int messageId, ITelegramBotClient? tClient, AdminUserTBL? admin)
        {
            var p = _db.PendingBroadcastTBLs.FirstOrDefault(x => x.ID == pid);
            if (p != null && (p.Status == 0 || p.Status == 3))
            {
                p.Status = 3; // Scheduled
                p.ScheduledAtUtc = targetUtc;
                if (admin != null) admin.CurrentRoute = "Home";
                await _db.SaveChangesAsync();

                string shamsiText = MultiPlatformAdminBot.Common.Utils.PersianDateTimeParser.ToPersianDateTimeString(targetUtc);
                string confirmation = $"✅ <b>پیام با موفقیت زمان‌بندی شد!</b>\n\n" +
                                       $"📅 زمان انتشار: <b>{shamsiText}</b>\n\n" +
                                       "این پیام در زمان تعیین‌شده به صورت خودکار به تمام کانال‌های مقصد فعال ارسال خواهد شد.";

                var ik = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
                {
                    new [] {
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("❌ لغو ارسال زمان‌بندی‌شده", $"cancel_sched_{pid}")
                    }
                });

                if (tClient != null) await tClient.EditMessageTextAsync(chatId, messageId, confirmation, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: ik);
            }
        }

        public async Task HandleCancelScheduleAsync(Telegram.Bot.Types.CallbackQuery callback, ITelegramBotClient? tClient)
        {
            if (!long.TryParse(callback.Data.Split('_')[2], out long pid)) return;

            var p = _db.PendingBroadcastTBLs.FirstOrDefault(x => x.ID == pid);
            if (p != null && p.Status == 3)
            {
                p.Status = 2; // Cancelled/Deleted
                await _db.SaveChangesAsync();

                if (tClient != null) await tClient.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, "❌ ارسال زمان‌بندی‌شده لغو شد.");
            }
        }

        public async Task HandleConfirmDeleteAsync(Telegram.Bot.Types.CallbackQuery callback, ITelegramBotClient? tClient)
        {
            if (!long.TryParse(callback.Data.Split('_')[2], out long pid)) return;

            var p = _db.PendingBroadcastTBLs.FirstOrDefault(x => x.ID == pid);
            if (p != null && p.Status == 0)
            {
                p.Status = 2;
                await _db.SaveChangesAsync();

                var reqs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<global::Application.DTOs.BroadcastMessageRequest>>(p.UpdatesJson);
                if (reqs != null && tClient != null)
                {
                    foreach (var r in reqs)
                    {
                        if (r.Update.Message != null)
                        {
                            try { await tClient.DeleteMessageAsync(callback.Message.Chat.Id, r.Update.Message.MessageId); } catch { }
                        }
                    }

                    var delReport = new PostReportTBL
                    {
                        DateInsert = DateTime.UtcNow,
                        Status = 1,
                        Type = 1,
                        IsSuccessful = true,
                        InOutType = 1, // Input
                        AdminChannelId = null,
                        Explain = "حذف شده",
                        SourceMessageId = reqs[0].Update.Message?.MessageId
                    };
                    _db.PostReportTBLs.Add(delReport);
                    await _db.SaveChangesAsync();
                }
                if (tClient != null) await tClient.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, "❌ پیام با موفقیت حذف شد.");
            }
        }
    }
}
