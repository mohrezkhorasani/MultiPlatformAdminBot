using Domain.Entity;
using MediatR;
using MultiPlatformAdminBot.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace MultiPlatformAdminBot.CoreMe.Services.Webhook
{
    /// <summary>
    /// The bot's in-chat state machine for admins: main menu, token management,
    /// channel management and footer settings. Mutations publish the same
    /// cache-invalidation notifications as the panel API controllers.
    /// </summary>
    public class MenuStateHandler
    {
        private readonly BotDbContext _db;
        private readonly IMediator _mediator;
        private readonly Channel<BroadcastJob> _broadcastChannel;
        private readonly IServiceProvider _serviceProvider;

        public MenuStateHandler(BotDbContext db, IMediator mediator, Channel<BroadcastJob> broadcastChannel, IServiceProvider serviceProvider)
        {
            _db = db;
            _mediator = mediator;
            _broadcastChannel = broadcastChannel;
            _serviceProvider = serviceProvider;
        }

        private static ReplyKeyboardMarkup MainKeyboard => new(new[]
        {
            new[] { new KeyboardButton("📢 مدیریت کانال‌ها"), new KeyboardButton("🤖 مدیریت توکن‌ها") },
            new[] { new KeyboardButton("📅 پیام‌های زمان‌بندی‌شده"), new KeyboardButton("📝 تنظیم فوتر") },
            new[] { new KeyboardButton("🏠 خانه") }
        }) { ResizeKeyboard = true };

        /// <summary>
        /// Routes an admin's message/callback through the state machine.
        /// Returns true when the update was fully handled here.
        /// </summary>
        public async Task<bool> HandleAsync(AdminUserTBL admin, Telegram.Bot.Types.Update update, long userId, ITelegramBotClient? tClient)
        {
            string msgText = update.Message?.Text ?? "";
            var callback = update.CallbackQuery;

            if (callback != null)
            {
                if (callback.Data != null && callback.Data.StartsWith("retry_broadcast_"))
                {
                    var parts = callback.Data.Split('_');
                    if (parts.Length >= 3 && long.TryParse(parts[2], out long blogId))
                    {
                        var bLog = _db.BroadcastLogTBLs.FirstOrDefault(b => b.ID == blogId && b.AdminUserId == userId);
                        if (bLog != null)
                        {
                            var failedDeliveries = _db.DeliveryLogTBLs
                                .Where(d => d.BroadcastLogId == blogId && d.Status != 2) // Status != 2 means not succeeded yet
                                .ToList();

                            if (failedDeliveries.Any())
                            {
                                int? waitMsgId = null;
                                if (tClient != null)
                                {
                                    var waitMsg = await tClient.SendTextMessageAsync(userId, "⏳ ارسال مجدد به ناموفق‌ها آغاز شد...");
                                    waitMsgId = waitMsg.MessageId;
                                }

                                _ = Task.Run(async () =>
                                {
                                    using var scope = _serviceProvider.CreateScope();
                                    var useCase = scope.ServiceProvider.GetRequiredService<global::Application.Interfaces.UseCases.IBroadcastMessageUseCase>();
                                    var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

                                    int successCount = 0;
                                    var errorLogsList = new System.Collections.Generic.List<string>();

                                    foreach (var del in failedDeliveries)
                                    {
                                        bool ok = await useCase.RetryDeliveryAsync(del.ID);
                                        if (ok)
                                        {
                                            successCount++;
                                        }
                                        else
                                        {
                                            // Fetch updated entity to get LastError
                                            var updatedDel = db.DeliveryLogTBLs.FirstOrDefault(d => d.ID == del.ID);
                                            string err = updatedDel?.LastError ?? "خطای نامشخص در ارسال API";
                                            errorLogsList.Add($"[{del.PlatformName} - {del.TargetChannelId}]: {err}");
                                        }
                                    }

                                    int remainingFailed = failedDeliveries.Count - successCount;
                                    string resultSummary = $"✅ ارسال مجدد پایان یافت.\nموفق: {successCount}\nناموفق: {remainingFailed}";

                                    InlineKeyboardMarkup? retryMarkup = null;
                                    if (remainingFailed > 0)
                                    {
                                        retryMarkup = new InlineKeyboardMarkup(new[]
                                        {
                                            InlineKeyboardButton.WithCallbackData("🔄 ارسال مجدد به ناموفق‌ها", $"retry_broadcast_{blogId}")
                                        });
                                    }

                                    if (tClient != null)
                                    {
                                        if (waitMsgId.HasValue)
                                            await tClient.EditMessageTextAsync(userId, waitMsgId.Value, resultSummary, replyMarkup: retryMarkup);
                                        else
                                            await tClient.SendTextMessageAsync(userId, resultSummary, replyMarkup: retryMarkup);

                                        // If there are errors, send detailed error log message to admin
                                        if (errorLogsList.Any())
                                        {
                                            string fullErrorMsg = "⚠️ جزئیات خطاهای ارسال مجدد:\n" + string.Join("\n", errorLogsList);
                                            await tClient.SendTextMessageAsync(userId, fullErrorMsg, replyMarkup: retryMarkup);
                                        }
                                    }
                                });

                                if (tClient != null) await tClient.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, callback.Message.Text + "\n(ارسال مجدد درخواست شد)");
                            }
                            else
                            {
                                if (tClient != null) await tClient.AnswerCallbackQueryAsync(callback.Id, "تمام ارسال‌های این پیام با موفقیت انجام شده‌اند.", showAlert: true);
                            }
                        }
                    }
                    if (tClient != null) await tClient.AnswerCallbackQueryAsync(callback.Id);
                    return true;
                }
            }

            if (msgText == "/start" || msgText == "🏠 خانه")
            {
                admin.CurrentRoute = "Home";
                await _db.SaveChangesAsync();
                if (tClient != null) await tClient.SendTextMessageAsync(userId, "به منوی اصلی خوش آمدید. پیام خود را برای ارسال همزمان بفرستید.", replyMarkup: MainKeyboard);
                return true;
            }

            if (admin.CurrentRoute == "Home")
            {
                if (msgText == "🤖 مدیریت توکن‌ها")
                {
                    admin.CurrentRoute = "ManageTokens";
                    await _db.SaveChangesAsync();
                    var myTokens = _db.BotTokenTBLs.Where(t => t.OwnerId == userId).ToList();
                    string res = $"شما {myTokens.Count} توکن ثبت کرده‌اید.\nبرای افزودن توکن جدید پلتفرم را انتخاب کنید:";
                    var ik = new InlineKeyboardMarkup(new[] {
                        new [] { InlineKeyboardButton.WithCallbackData("➕ بله", "addtok_Bale"), InlineKeyboardButton.WithCallbackData("➕ ایتا", "addtok_Eitaa") },
                        new [] { InlineKeyboardButton.WithCallbackData("➕ روبیکا", "addtok_Rubika"), InlineKeyboardButton.WithCallbackData("➕ تلگرام", "addtok_Telegram") }
                    });
                    if (tClient != null) await tClient.SendTextMessageAsync(userId, res, replyMarkup: ik);
                    return true;
                }
                if (msgText == "📢 مدیریت کانال‌ها")
                {
                    admin.CurrentRoute = "ManageChannels";
                    await _db.SaveChangesAsync();
                    var myChannels = _db.PlatformConfigTBLs.Where(c => c.BotToken.OwnerId == userId).ToList();
                    string res = $"شما {myChannels.Count} از {admin.MaxTotalChannels} کانال مجاز را ثبت کرده‌اید.\n";
                    var ik = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("➕ افزودن کانال جدید", "addchannel_start") }
                    });
                    if (tClient != null) await tClient.SendTextMessageAsync(userId, res, replyMarkup: ik);
                    return true;
                }
                if (msgText == "📅 پیام‌های زمان‌بندی‌شده")
                {
                    var scheduledList = _db.PendingBroadcastTBLs
                        .Where(p => p.AdminUserId == userId && p.Status == 3)
                        .OrderBy(p => p.ScheduledAtUtc)
                        .ToList();

                    if (!scheduledList.Any())
                    {
                        if (tClient != null) await tClient.SendTextMessageAsync(userId, "ℹ️ شما در حال حاضر هیچ پیام زمان‌بندی‌شده‌ای در صف انتظار ندارید.");
                        return true;
                    }

                    string listMsg = "📅 <b>لیست پیام‌های زمان‌بندی‌شده شما:</b>\n\n";
                    var buttons = new List<InlineKeyboardButton[]>();

                    int idx = 1;
                    foreach (var item in scheduledList)
                    {
                        string timeStr = item.ScheduledAtUtc.HasValue 
                            ? MultiPlatformAdminBot.Common.Utils.PersianDateTimeParser.ToPersianDateTimeString(item.ScheduledAtUtc.Value) 
                            : "نامشخص";

                        listMsg += $"{idx}. ⏰ <b>{timeStr}</b> (شناسه: <code>{item.ID}</code>)\n";
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"❌ لغو پیام {idx}", $"cancel_sched_{item.ID}") });
                        idx++;
                    }

                    var ik = new InlineKeyboardMarkup(buttons);
                    if (tClient != null) await tClient.SendTextMessageAsync(userId, listMsg, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: ik);
                    return true;
                }
                if (msgText == "📝 تنظیم فوتر")
                {
                    admin.CurrentRoute = "AddFooterWait";
                    await _db.SaveChangesAsync();
                    string cur = string.IsNullOrWhiteSpace(admin.FooterText) ? "ندارد" : admin.FooterText;
                    if (tClient != null) await tClient.SendTextMessageAsync(userId, $"فوتر فعلی شما:\n{cur}\n\nلطفا متن فوتر جدید خود را بفرستید (یا برای لغو 🏠 خانه را بزنید):");
                    return true;
                }
                return false;
            }

            if (admin.CurrentRoute == "ManageTokens")
            {
                if (callback != null && callback.Data.StartsWith("addtok_"))
                {
                    string plat = callback.Data.Split('_')[1];
                    if (_db.BotTokenTBLs.Any(t => t.OwnerId == userId && t.PlatformName == plat))
                    {
                        if (tClient != null) await tClient.AnswerCallbackQueryAsync(callback.Id, $"شما قبلا یک توکن برای {plat} ثبت کرده‌اید.", showAlert: true);
                        return true;
                    }
                    admin.CurrentRoute = $"AddTokenWait_{plat}";
                    await _db.SaveChangesAsync();
                    if (tClient != null) await tClient.SendTextMessageAsync(userId, $"لطفا توکن ربات {plat} خود را بفرستید:");
                    return true;
                }
                return false;
            }

            if (admin.CurrentRoute.StartsWith("AddTokenWait_"))
            {
                if (!string.IsNullOrEmpty(msgText))
                {
                    string plat = admin.CurrentRoute.Split('_')[1];
                    _db.BotTokenTBLs.Add(new BotTokenTBL { OwnerId = userId, PlatformName = plat, TokenValue = msgText, IsActive = true, Status = 1, DateInsert = DateTime.UtcNow });
                    admin.CurrentRoute = "Home";
                    await _db.SaveChangesAsync();
                    await _mediator.Publish(new Application.Notifications.PlatformConfigChangedNotification());
                    if (tClient != null) await tClient.SendTextMessageAsync(userId, $"توکن {plat} با موفقیت ذخیره شد.", replyMarkup: MainKeyboard);
                }
                return true;
            }

            if (admin.CurrentRoute == "AddFooterWait")
            {
                if (!string.IsNullOrWhiteSpace(msgText))
                {
                    admin.FooterText = msgText;
                    admin.CurrentRoute = "Home";
                    await _db.SaveChangesAsync();
                    if (tClient != null) await tClient.SendTextMessageAsync(userId, "فوتر شما با موفقیت ثبت شد.", replyMarkup: MainKeyboard);
                }
                return true;
            }

            if (admin.CurrentRoute == "ManageChannels")
            {
                if (callback != null && callback.Data == "addchannel_start")
                {
                    var myTokens = _db.BotTokenTBLs.Where(t => t.OwnerId == userId).ToList();
                    if (!myTokens.Any())
                    {
                        if (tClient != null) await tClient.AnswerCallbackQueryAsync(callback.Id, "ابتدا یک توکن اضافه کنید.", showAlert: true);
                        return true;
                    }
                    var currentChannelCount = _db.PlatformConfigTBLs.Count(c => c.BotToken.OwnerId == userId);
                    if (currentChannelCount >= admin.MaxTotalChannels)
                    {
                        if (tClient != null) await tClient.AnswerCallbackQueryAsync(callback.Id, "شما به حداکثر مجاز تعداد کانال رسیده‌اید.", showAlert: true);
                        return true;
                    }
                    admin.CurrentRoute = "AddChannelWaitPlatform";
                    await _db.SaveChangesAsync();

                    var buttons = myTokens.Select(t => InlineKeyboardButton.WithCallbackData(t.PlatformName, $"seltok_{t.ID}")).ToArray();
                    var ik = new InlineKeyboardMarkup(buttons.Chunk(2));
                    if (tClient != null) await tClient.SendTextMessageAsync(userId, "برای کدام پلتفرم کانال اضافه می‌کنید؟ (انتخاب توکن)", replyMarkup: ik);
                    return true;
                }
                return false;
            }

            if (admin.CurrentRoute == "AddChannelWaitPlatform")
            {
                if (callback != null && callback.Data.StartsWith("seltok_"))
                {
                    string tokIdStr = callback.Data.Split('_')[1];
                    if (long.TryParse(tokIdStr, out long tokId))
                    {
                        var tok = _db.BotTokenTBLs.FirstOrDefault(t => t.ID == tokId && t.OwnerId == userId);
                        if (tok != null)
                        {
                            var tokenChannelsCount = _db.PlatformConfigTBLs.Count(c => c.BotTokenId == tok.ID);
                            if (tokenChannelsCount >= admin.MaxChannelsPerToken)
                            {
                                if (tClient != null) await tClient.AnswerCallbackQueryAsync(callback.Id, "حداکثر کانال مجاز برای این توکن پر شده است.", showAlert: true);
                                return true;
                            }
                            admin.CurrentRoute = $"AddChannelWaitId_{tokId}";
                            await _db.SaveChangesAsync();
                            if (tClient != null) await tClient.SendTextMessageAsync(userId, "حالا آیدی کانال (با @) را بفرستید:");
                        }
                    }
                    return true;
                }
                return false;
            }

            if (admin.CurrentRoute.StartsWith("AddChannelWaitId_"))
            {
                if (!string.IsNullOrEmpty(msgText))
                {
                    string tokIdStr = admin.CurrentRoute.Split('_')[1];
                    if (long.TryParse(tokIdStr, out long tokId))
                    {
                        var tok = _db.BotTokenTBLs.FirstOrDefault(t => t.ID == tokId && t.OwnerId == userId);
                        if (tok != null)
                        {
                            var chan = new PlatformConfigTBL { PlatformName = tok.PlatformName, BotTokenId = tok.ID, TargetChannelId = msgText, IsActive = true, Status = 1, DateInsert = DateTime.UtcNow };
                            _db.PlatformConfigTBLs.Add(chan);
                            await _db.SaveChangesAsync();

                            _db.AdminChannelTBLs.Add(new AdminChannelTBL { AdminUserId = userId, ChannelId = chan.ID, DateInsert = DateTime.UtcNow, Status = 1, Type = 1 });
                            admin.CurrentRoute = "Home";
                            await _db.SaveChangesAsync();

                            // Bot-menu mutations bypass the panel controllers: invalidate caches here too.
                            await _mediator.Publish(new Application.Notifications.PlatformConfigChangedNotification());
                            await _mediator.Publish(new Application.Notifications.AdminChannelChangedNotification(userId));

                            if (tClient != null) await tClient.SendTextMessageAsync(userId, "کانال با موفقیت اضافه شد.", replyMarkup: MainKeyboard);
                        }
                    }
                }
                return true;
            }

            if (admin.CurrentRoute.StartsWith("WaitScheduleTime_"))
            {
                string pidStr = admin.CurrentRoute.Split('_')[1];
                if (long.TryParse(pidStr, out long pid))
                {
                    if (MultiPlatformAdminBot.Common.Utils.PersianDateTimeParser.TryParseSchedule(msgText, DateTime.UtcNow, out DateTime targetUtc, out string error))
                    {
                        var p = _db.PendingBroadcastTBLs.FirstOrDefault(x => x.ID == pid);
                        if (p != null && (p.Status == 0 || p.Status == 3))
                        {
                            p.Status = 3; // Scheduled
                            p.ScheduledAtUtc = targetUtc;
                            admin.CurrentRoute = "Home";
                            await _db.SaveChangesAsync();

                            string shamsiText = MultiPlatformAdminBot.Common.Utils.PersianDateTimeParser.ToPersianDateTimeString(targetUtc);
                            string confirmation = $"✅ <b>پیام با موفقیت برای زمان زیر تنظیم شد:</b>\n\n" +
                                                   $"📅 زمان انتشار: <b>{shamsiText}</b>\n\n" +
                                                   "پیام در موعد مقرر به کانال‌ها ارسال خواهد شد.";

                            var ik = new InlineKeyboardMarkup(new[]
                            {
                                new [] { InlineKeyboardButton.WithCallbackData("❌ لغو ارسال زمان‌بندی‌شده", $"cancel_sched_{pid}") }
                            });

                            if (tClient != null) await tClient.SendTextMessageAsync(userId, confirmation, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: ik);
                        }
                        else
                        {
                            admin.CurrentRoute = "Home";
                            await _db.SaveChangesAsync();
                            if (tClient != null) await tClient.SendTextMessageAsync(userId, "❌ پیام مورد نظر یافت نشد یا وضعیت آن نامعتبر است.", replyMarkup: MainKeyboard);
                        }
                    }
                    else
                    {
                        if (tClient != null) await tClient.SendTextMessageAsync(userId, $"⚠️ {error}\nلطفا مجدداً تلاش کنید یا برای انصراف 🏠 خانه را بزنید:");
                    }
                }
                return true;
            }

            return false;
        }
    }
}
