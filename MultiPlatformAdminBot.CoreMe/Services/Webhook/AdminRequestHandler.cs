using Domain.Entity;
using MultiPlatformAdminBot.Persistence;
using Telegram.Bot;

namespace MultiPlatformAdminBot.CoreMe.Services.Webhook
{
    /// <summary>
    /// Telegram admin self-service: non-admins request access (req_admin),
    /// the owner approves or rejects those requests (approve_/reject_).
    /// </summary>
    public class AdminRequestHandler
    {
        private readonly BotDbContext _db;

        public AdminRequestHandler(BotDbContext db)
        {
            _db = db;
        }

        /// <summary>Handles the req_admin callback from a non-admin user.</summary>
        public async Task HandleNewRequestAsync(Telegram.Bot.Types.Update update, long userId,
            string? username, string? fullname, ITelegramBotClient? tClient)
        {
            if (update.CallbackQuery == null) return;

            var existingReq = _db.AdminRequestTBLs.OrderByDescending(r => r.DateInsert).FirstOrDefault(r => r.UserId == userId);
            if (existingReq != null && existingReq.Status == 2 && existingReq.BanExpiryDate > DateTime.UtcNow)
            {
                if (tClient != null) await tClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "درخواست شما قبلا رد شده است و تا یک ماه بن هستید.", showAlert: true);
                return;
            }
            if (existingReq != null && existingReq.Status == 0)
            {
                if (tClient != null) await tClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "درخواست شما در حال بررسی است.", showAlert: true);
                return;
            }

            _db.AdminRequestTBLs.Add(new AdminRequestTBL { UserId = userId, Username = username, FullName = fullname, Status = 0, Type = 1, DateInsert = DateTime.UtcNow });
            await _db.SaveChangesAsync();

            if (tClient == null) return;

            await tClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "درخواست شما ثبت شد و برای مالک ارسال گردید.", showAlert: true);

            var inlineKeyboard = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
                new []
                {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("✅ تایید", $"approve_{userId}"),
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("❌ رد کردن", $"reject_{userId}")
                }
            });
            await tClient.SendTextMessageAsync("690534265", $"درخواست ادمینی جدید:\nنام: {fullname}\nآیدی: {userId}\nیوزرنیم: @{username}", replyMarkup: inlineKeyboard);
        }

        /// <summary>Sends the "request admin access" button to a non-admin.</summary>
        public async Task SendAccessDeniedAsync(long userId, ITelegramBotClient? tClient)
        {
            if (tClient == null) return;
            var inlineKeyboard = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
                Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("درخواست ادمینی", "req_admin")
            });
            await tClient.SendTextMessageAsync(userId, "شما دسترسی ادمین ندارید. برای ارسال درخواست روی دکمه زیر کلیک کنید.", replyMarkup: inlineKeyboard);
        }

        /// <summary>Owner-side approve_/reject_ callback handling.</summary>
        public async Task HandleOwnerDecisionAsync(Telegram.Bot.Types.Update update, ITelegramBotClient? tClient)
        {
            var callback = update.CallbackQuery;
            if (callback == null) return;

            var targetIdStr = callback.Data.Split('_')[1];
            if (!long.TryParse(targetIdStr, out long targetId)) return;

            var req = _db.AdminRequestTBLs.OrderByDescending(r => r.DateInsert).FirstOrDefault(r => r.UserId == targetId && r.Status == 0);
            if (req != null)
            {
                if (callback.Data.StartsWith("approve_"))
                {
                    req.Status = 1;
                    if (!_db.AdminUserTBLs.Any(u => u.ID == targetId))
                    {
                        _db.AdminUserTBLs.Add(new AdminUserTBL { ID = targetId, Status = 100, Type = 1, DateInsert = DateTime.UtcNow, Explain = "Approved Admin", FullName = req.FullName, Username = req.Username, CurrentRoute = "Home", HasPanelAccess = false });
                    }
                    await _db.SaveChangesAsync();
                    if (tClient != null)
                    {
                        await tClient.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, $"درخواست {req.FullName} تایید شد.");
                        await tClient.SendTextMessageAsync(targetId, "تبریک! درخواست ادمینی شما تایید شد.");
                    }
                }
                else
                {
                    req.Status = 2;
                    req.BanExpiryDate = DateTime.UtcNow.AddMonths(1);
                    await _db.SaveChangesAsync();
                    if (tClient != null)
                    {
                        await tClient.EditMessageTextAsync(callback.Message.Chat.Id, callback.Message.MessageId, $"درخواست {req.FullName} رد شد.");
                        await tClient.SendTextMessageAsync(targetId, "درخواست ادمینی شما رد شد و تا یک ماه آینده امکان ارسال مجدد ندارید.");
                    }
                }
            }
            if (tClient != null) await tClient.AnswerCallbackQueryAsync(callback.Id);
        }
    }
}
