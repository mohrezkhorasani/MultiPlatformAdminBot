using Application.DTOs;
using Application.Interfaces;
using Application.Interfaces.IInfrastructure;
using Domain.Entity;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MultiPlatformAdminBot.CoreMe.Services;
using MultiPlatformAdminBot.CoreMe.Services.Webhook;
using System.Threading.Channels;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MultiPlatformAdminBot.CoreMe.Controllers
{
    /// <summary>
    /// Inbound webhook entry point. Kept intentionally thin: parsing + auth here,
    /// all business logic delegated to the scoped webhook handlers
    /// (AdminRequestHandler, PendingBroadcastHandler, MenuStateHandler).
    /// Heavy broadcast work is enqueued on a channel so Telegram always gets a fast 200.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly IAppDbContext _db;
        private readonly Channel<BroadcastJob> _broadcastChannel;
        private readonly AdminRequestHandler _adminRequests;
        private readonly PendingBroadcastHandler _pending;
        private readonly MenuStateHandler _menu;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
        private readonly IMediator _mediator;
        private readonly string? _webhookSecret;

        public WebhookController(IAppDbContext db,
            Channel<BroadcastJob> broadcastChannel,
            AdminRequestHandler adminRequests,
            PendingBroadcastHandler pending,
            MenuStateHandler menu,
            Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
            IMediator mediator,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _db = db;
            _broadcastChannel = broadcastChannel;
            _adminRequests = adminRequests;
            _pending = pending;
            _menu = menu;
            _cache = cache;
            _mediator = mediator;
            // When set, only payloads carrying the matching X-Telegram-Bot-Api-Secret-Token
            // (attached by Telegram itself) are accepted — rejects forged webhooks.
            _webhookSecret = configuration["Webhook:SecretToken"];
        }

        [HttpPost("telegram")]
        public async Task<IActionResult> ReceiveTelegram()
        {
            if (!string.IsNullOrEmpty(_webhookSecret))
            {
                if (!Request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var secretHeader)
                    || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.UTF8.GetBytes(secretHeader.ToString()),
                        System.Text.Encoding.UTF8.GetBytes(_webhookSecret)))
                {
                    return Unauthorized();
                }
            }

            using var reader = new System.IO.StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body)) return Ok();

            var update = Newtonsoft.Json.JsonConvert.DeserializeObject<Update>(body);
            if (update == null) return Ok();

            long userId = update.Message?.From?.Id ?? update.EditedMessage?.From?.Id ?? update.CallbackQuery?.From?.Id ?? 0;
            string? username = update.Message?.From?.Username ?? update.EditedMessage?.From?.Username ?? update.CallbackQuery?.From?.Username;
            string? fullname = (update.Message?.From?.FirstName + " " + update.Message?.From?.LastName).Trim();
            if (string.IsNullOrEmpty(fullname))
            {
                fullname = (update.EditedMessage?.From?.FirstName + " " + update.EditedMessage?.From?.LastName).Trim();
            }
            if (string.IsNullOrEmpty(fullname))
            {
                fullname = (update.CallbackQuery?.From?.FirstName + " " + update.CallbackQuery?.From?.LastName).Trim();
            }

            var tClient = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<IEnumerable<IBotPlatformClient>>(HttpContext.RequestServices)
                .FirstOrDefault(c => c.PlatformName == "Telegram") as Platforms.Telegram.TelegramBotClientAdapter;

            // 1) Non-admin check
            var admin = _db.AdminUserTBLs.FirstOrDefault(u => u.ID == userId && u.Status == 100);
            if (admin == null)
            {
                if (update.CallbackQuery != null && update.CallbackQuery.Data == "req_admin")
                {
                    await _adminRequests.HandleNewRequestAsync(update, userId, username, fullname, tClient?.Client);
                    return Ok();
                }
                if (update.Message != null)
                {
                    await _adminRequests.SendAccessDeniedAsync(userId, tClient?.Client);
                }
                return Ok();
            }

            // Check if message is a single-word command starting with '/'
            if (update.Message != null && !string.IsNullOrEmpty(update.Message.Text))
            {
                var txt = update.Message.Text.Trim();
                if (txt.StartsWith("/") && !txt.Contains(" ") && !txt.Contains("\n") && !txt.Contains("\r"))
                {
                    if (txt.Equals("/clear_cache", StringComparison.OrdinalIgnoreCase) || txt.Equals("/clearcache", StringComparison.OrdinalIgnoreCase))
                    {
                        if (userId == 690534265 || userId == 8658914813)
                        {
                            _cache.Remove("AppMode");
                            _cache.Remove("AllPlatformConfigs");
                            _cache.Remove("AllAdminChannels");

                            if (_cache is Microsoft.Extensions.Caching.Memory.MemoryCache memoryCache)
                            {
                                memoryCache.Compact(1.0);
                            }

                            if (tClient?.Client != null)
                            {
                                await tClient.Client.SendTextMessageAsync(userId, "✅ تمام کش‌های سیستم با موفقیت پاک شدند.", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                            }
                        }
                    }
                    else if (txt.Equals("/cache", StringComparison.OrdinalIgnoreCase))
                    {
                        if (userId == 690534265 || userId == 8658914813)
                        {
                            bool hasAppMode = _cache.TryGetValue("AppMode", out var appModeVal);
                            bool hasConfigs = _cache.TryGetValue("AllPlatformConfigs", out var configsVal);
                            bool hasChannels = _cache.TryGetValue("AllAdminChannels", out var channelsVal);

                            var cacheReport = "⚡️ <b>وضعیت کش‌های سیستم:</b>\n\n";
                            cacheReport += $"🔹 <b>AppMode:</b> {(hasAppMode ? $"<code>{appModeVal}</code> (در کش موجود است)" : "❌ در کش نیست")}\n";
                            cacheReport += $"🔹 <b>AllPlatformConfigs:</b> {(hasConfigs ? "✅ موجود در کش" : "❌ در کش نیست")}\n";
                            cacheReport += $"🔹 <b>AllAdminChannels:</b> {(hasChannels ? "✅ موجود در کش" : "❌ در کش نیست")}\n";

                            if (tClient?.Client != null)
                            {
                                await tClient.Client.SendTextMessageAsync(userId, cacheReport, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                            }
                        }
                    }
                    else if (txt.Equals("/mode", StringComparison.OrdinalIgnoreCase))
                    {
                        if (tClient?.Client != null)
                        {
                            var setting = _db.SystemSettings.FirstOrDefault(s => s.Key == "AppMode");
                            var currentMode = setting?.Value == "1" ? "🐛 دیباگ (Debug)" : "🚀 پروداکشن (Production)";
                            await tClient.Client.SendTextMessageAsync(userId, $"⚙️ وضعیت فعلی سیستم:\n<b>{currentMode}</b>", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                        }
                    }
                    else if (txt.Equals("/change_mode", StringComparison.OrdinalIgnoreCase))
                    {
                        if (tClient?.Client != null)
                        {
                            var setting = _db.SystemSettings.FirstOrDefault(s => s.Key == "AppMode");
                            var currentVal = setting?.Value ?? "2";
                            var newVal = currentVal == "1" ? "2" : "1";

                            if (setting == null)
                            {
                                setting = new global::Domain.Entity.SystemSettingTBL { Key = "AppMode", Value = newVal };
                                _db.SystemSettings.Add(setting);
                            }
                            else
                            {
                                setting.Value = newVal;
                            }

                            await _db.SaveChangesAsync();
                            _cache.Remove("AppMode");

                            // Trigger Telegram bot profile photo & name update via notification
                            await _mediator.Publish(new MultiPlatformAdminBot.Application.Notifications.AppModeChangedNotification());

                            var modeTitle = newVal == "1" ? "🐛 دیباگ (Debug)" : "🚀 پروداکشن (Production)";
                            await tClient.Client.SendTextMessageAsync(userId, $"✅ حالت سیستم با موفقیت به <b>{modeTitle}</b> تغییر یافت.\n(نام و تصویر ربات نیز به‌روزرسانی شد)", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                        }
                    }
                    else if (txt.Equals("/help", StringComparison.OrdinalIgnoreCase) || txt.Equals("/start", StringComparison.OrdinalIgnoreCase))
                    {
                        if (tClient?.Client != null)
                        {
                            var helpText = "📖 <b>راهنمای دستورات ربات ادمین:</b>\n\n" +
                                           "🔹 /mode - <code>مشاهده وضعیت فعلی سیستم (دیباگ یا پروداکشن)</code>\n" +
                                           "🔹 /change_mode - <code>تغییر حالت سیستم + تغییر عکس و اسم ربات</code>\n" +
                                           "🔹 /mychannels - <code>مشاهده لیست کانال‌های فعال شما</code>\n" +
                                           "🔹 /newchannel - <code>راهنمای افزودن کانال جدید</code>\n" +
                                           "🔹 /stats یا /reports - <code>گزارش آمار پست‌های پروداکشن شما</code>\n" +
                                           "🔹 /cache - <code>مشاهده وضعیت کش‌های سیستم (ادمین اصلی)</code>\n" +
                                           "🔹 /clear_cache - <code>پاکسازی کامل کش‌های سیستم (ادمین اصلی)</code>\n";
                            await tClient.Client.SendTextMessageAsync(userId, helpText, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                        }
                    }
                    else if (txt.Equals("/stats", StringComparison.OrdinalIgnoreCase) || txt.Equals("/reports", StringComparison.OrdinalIgnoreCase))
                    {
                        if (tClient?.Client != null)
                        {
                            // Fetch user's production channels (Type == 2)
                            var myAdminChannels = _db.AdminChannelTBLs.Where(ac => ac.AdminUserId == userId).Select(ac => ac.ChannelId).ToList();
                            var prodChannels = _db.PlatformConfigTBLs.Where(c => myAdminChannels.Contains(c.ID) && c.Type == 2).ToList();

                            if (!prodChannels.Any())
                            {
                                await tClient.Client.SendTextMessageAsync(userId, "ℹ️ شما هیچ کانال فعالی در حالت <b>پروداکشن</b> ندارید.", parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                            }
                            else
                            {
                                var prodChannelIds = prodChannels.Select(c => c.ID).ToList();

                                // Get total reports for this admin's prod channels
                                var totalPosts = _db.PostReportTBLs.Count(p => p.AdminChannelId.HasValue && prodChannelIds.Contains(p.AdminChannelId.Value));
                                var successPosts = _db.PostReportTBLs.Count(p => p.AdminChannelId.HasValue && prodChannelIds.Contains(p.AdminChannelId.Value) && p.IsSuccessful);

                                var reportMsg = "📊 <b>گزارش آمار ارسال‌های شما (کانال‌های پروداکشن):</b>\n\n" +
                                                $"🔹 تعداد کل پست‌های ثبت شده: <b>{totalPosts}</b>\n" +
                                                $"✅ پست‌های ارسال شده با موفقیت: <b>{successPosts}</b>\n" +
                                                $"❌ ارسال‌های نا‌موفق: <b>{totalPosts - successPosts}</b>\n\n" +
                                                "📢 <b>کانال‌های تحت پوشش:</b>\n";

                                foreach (var ch in prodChannels)
                                {
                                    reportMsg += $"• <b>{ch.PlatformName}</b> (<code>{ch.TargetChannelId}</code>)\n";
                                }

                                await tClient.Client.SendTextMessageAsync(userId, reportMsg, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                            }
                        }
                    }
                    else if (txt.Equals("/mychannels", StringComparison.OrdinalIgnoreCase))
                    {
                        if (tClient?.Client != null)
                        {
                            var channelIds = _db.AdminChannelTBLs.Where(ac => ac.AdminUserId == userId).Select(ac => ac.ChannelId).ToList();
                            if (channelIds.Count == 0)
                            {
                                await tClient.Client.SendTextMessageAsync(userId, "📋 شما در حال حاضر به هیچ کانالی دسترسی ندارید.");
                            }
                            else
                            {
                                var channels = _db.PlatformConfigTBLs.Where(c => channelIds.Contains(c.ID)).ToList();
                                var msg = "📋 <b>کانال‌های فعال شما:</b>\n\n";
                                foreach (var ch in channels)
                                {
                                    var typeStr = ch.Type == 1 ? "🐛 دیباگ" : "🚀 پروداکشن";
                                    msg += $"🔹 <b>{ch.PlatformName}</b> - <code>{ch.TargetChannelId}</code> ({typeStr})\n   شناسه کانال: <code>{ch.ID}</code>\n\n";
                                }
                                await tClient.Client.SendTextMessageAsync(userId, msg, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                            }
                        }
                    }
                    else if (txt.Equals("/newchannel", StringComparison.OrdinalIgnoreCase))
                    {
                        if (tClient?.Client != null)
                        {
                            await tClient.Client.SendTextMessageAsync(userId, "برای ایجاد کانال جدید به آیدی @hekmateislami_admin پیام دهید.");
                        }
                    }
                    else
                    {
                        // Unknown command handling
                        if (tClient?.Client != null)
                        {
                            await tClient.Client.SendTextMessageAsync(userId, "❌ دستور وارد شده یافت نشد.\nبرای دیدن لیست دستورات معتبر عبارت /help را ارسال کنید.");
                        }
                    }
                    
                    // Return Ok for any single-word slash command so it is not treated as a broadcast post
                    return Ok();
                }
            }

            // 2) Owner: approve/reject admin requests.
            if (userId == 690534265 && update.CallbackQuery != null
                && (update.CallbackQuery.Data.StartsWith("approve_") || update.CallbackQuery.Data.StartsWith("reject_")))
            {
                await _adminRequests.HandleOwnerDecisionAsync(update, tClient?.Client);
                return Ok();
            }

            // 3) Pending broadcast drafts (send / delete / cancel / confirm / schedule).
            if (update.CallbackQuery != null)
            {
                var data = update.CallbackQuery.Data;
                if (data.StartsWith("send_pending_")) { await _pending.HandleSendAsync(update.CallbackQuery, tClient?.Client); return Ok(); }
                if (data.StartsWith("del_pending_")) { await _pending.HandleDeleteRequestAsync(update.CallbackQuery, tClient?.Client); return Ok(); }
                if (data.StartsWith("cancel_del_")) { await _pending.HandleCancelDeleteAsync(update.CallbackQuery, tClient?.Client); return Ok(); }
                if (data.StartsWith("confirm_del_")) { await _pending.HandleConfirmDeleteAsync(update.CallbackQuery, tClient?.Client); return Ok(); }
                if (data.StartsWith("sched_pending_")) { await _pending.HandleScheduleRequestAsync(update.CallbackQuery, tClient?.Client, admin); return Ok(); }
                if (data.StartsWith("quick_sched_")) { await _pending.HandleQuickScheduleAsync(update.CallbackQuery, tClient?.Client, admin); return Ok(); }
                if (data.StartsWith("cancel_sched_")) { await _pending.HandleCancelScheduleAsync(update.CallbackQuery, tClient?.Client); return Ok(); }
            }

            // 4) Admin in-chat menu state machine (tokens / channels / footer).
            if (await _menu.HandleAsync(admin, update, userId, tClient?.Client))
            {
                return Ok();
            }

            // 5) Handle Edited Message: broadcast edits to all channels
            if (update.EditedMessage != null)
            {
                var editRequest = new BroadcastMessageRequest
                {
                    Update = update,
                    UserId = userId,
                    SourcePlatform = "Telegram"
                };

                await _broadcastChannel.Writer.WriteAsync(
                    new BroadcastJob
                    {
                        Kind = BroadcastJobKind.EditMessage,
                        Request = editRequest
                    });

                return Ok();
            }

            // 6) Otherwise it's a new broadcast: record the report and enqueue.
            var report = new PostReportTBL
            {
                Type = 1,
                Status = 1,
                DateInsert = DateTime.UtcNow,
                IsSuccessful = true,
                InOutType = 1,
                SourceMessageId = update.Message?.MessageId,
                AdminChannelId = null
            };

            if (update.Message != null)
            {
                var content = update.Message.Text ?? update.Message.Caption ?? "";
                if (!string.IsNullOrEmpty(content))
                    report.MetaDatas.Add(new PostReportMetaDataTBL { MetaKey = "content", MetaValue = content });
                report.MetaDatas.Add(new PostReportMetaDataTBL { MetaKey = "senderId", MetaValue = userId.ToString() });
            }

            _db.PostReportTBLs.Add(report);
            await _db.SaveChangesAsync();

            var request = new BroadcastMessageRequest
            {
                Update = update,
                UserId = userId,
                ReplyToSourceMessageId = update.Message?.ReplyToMessage?.MessageId,
                SourcePlatform = "Telegram"
            };

            if (Request.Headers.TryGetValue("X-Wait-Message-Id", out var waitMsgIdStr) && int.TryParse(waitMsgIdStr, out int waitMsgId))
            {
                request.WaitMessageId = waitMsgId;
            }

            // Enqueue for the background worker (album grouping + confirmation) so the
            // webhook acks 200 immediately and never blocks on media downloads.
            await _broadcastChannel.Writer.WriteAsync(
                new BroadcastJob
                {
                    Kind = BroadcastJobKind.InboundUpdate,
                    Request = request
                });

            return Ok();
        }

        [HttpPost("bale")]
        public async Task<IActionResult> ReceiveBale()
        {
            using var reader = new System.IO.StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body)) return Ok();

            var update = Newtonsoft.Json.JsonConvert.DeserializeObject<Update>(body);
            if (update == null) return Ok();

            long userId = update.Message?.From?.Id ?? update.CallbackQuery?.From?.Id ?? 0;

            var admin = _db.AdminUserTBLs.FirstOrDefault(u => u.ID == userId && u.Status == 100);
            if (admin == null) return Unauthorized();

            var report = new PostReportTBL
            {
                Type = 1,
                Status = 1,
                DateInsert = DateTime.UtcNow,
                IsSuccessful = true,
                InOutType = 1,
                SourceMessageId = update.Message?.MessageId,
                AdminChannelId = null
            };

            if (update.Message != null)
            {
                var content = update.Message.Text ?? update.Message.Caption ?? "";
                if (!string.IsNullOrEmpty(content))
                    report.MetaDatas.Add(new PostReportMetaDataTBL { MetaKey = "content", MetaValue = content });
                report.MetaDatas.Add(new PostReportMetaDataTBL { MetaKey = "senderId", MetaValue = userId.ToString() });
            }

            _db.PostReportTBLs.Add(report);
            await _db.SaveChangesAsync();

            var request = new BroadcastMessageRequest
            {
                Update = update,
                UserId = userId,
                ReplyToSourceMessageId = update.Message?.ReplyToMessage?.MessageId,
                SourcePlatform = "Bale"
            };

            if (Request.Headers.TryGetValue("X-Wait-Message-Id", out var waitMsgIdStr) && int.TryParse(waitMsgIdStr, out int waitMsgId))
            {
                request.WaitMessageId = waitMsgId;
            }

            // Enqueue for the background worker; webhook acks 200 immediately.
            await _broadcastChannel.Writer.WriteAsync(
                new BroadcastJob
                {
                    Kind = BroadcastJobKind.InboundUpdate,
                    Request = request
                });

            return Ok();
        }
    }
}

