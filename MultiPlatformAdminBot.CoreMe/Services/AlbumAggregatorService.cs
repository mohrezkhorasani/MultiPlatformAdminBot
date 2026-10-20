using Application.DTOs;
using Application.Interfaces.UseCases;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MultiPlatformAdminBot.CoreMe.Presenter;
using Domain.Entity;
using Newtonsoft.Json;
using System.Linq;
using Telegram.Bot;

namespace MultiPlatformAdminBot.CoreMe.Services
{
    public class AlbumState
    {
        public List<BroadcastMessageRequest> Requests { get; set; } = new();
        public Timer Timer { get; set; }
    }

    public class AlbumAggregatorService : IAlbumAggregatorService
    {
        private readonly ConcurrentDictionary<string, AlbumState> _albums = new();
        private readonly IServiceProvider _serviceProvider;

        public AlbumAggregatorService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task ProcessRequestAsync(BroadcastMessageRequest request)
        {
            var mediaGroupId = request.Update.Message?.MediaGroupId;

            if (string.IsNullOrEmpty(mediaGroupId))
            {
                // Single message, process immediately (by asking for confirmation)
                await AskForConfirmationAsync(new List<BroadcastMessageRequest> { request });
                return;
            }

            // It's part of an album
            var state = _albums.GetOrAdd(mediaGroupId, groupId => 
            {
                return new AlbumState 
                {
                    Timer = new Timer(OnTimerFired, groupId, Timeout.Infinite, Timeout.Infinite)
                };
            });

            lock (state)
            {
                state.Requests.Add(request);
                // Reset timer to 3 seconds from now
                state.Timer.Change(3000, Timeout.Infinite);
            }
        }

        // Timer callbacks must stay fire-and-forget, but exceptions must be
        // caught here (an async void callback would crash the process on a throw).
        private void OnTimerFired(object stateInfo)
        {
            _ = HandleTimerFiredAsync(stateInfo as string);
        }

        private async Task HandleTimerFiredAsync(string groupId)
        {
            try
            {
                if (string.IsNullOrEmpty(groupId)) return;

                if (_albums.TryRemove(groupId, out var state))
                {
                    lock (state)
                    {
                        state.Timer?.Dispose();
                    }

                    if (state.Requests.Count == 0) return;

                    // Ask for confirmation
                    await AskForConfirmationAsync(state.Requests);
                }
            }
            catch (Exception ex)
            {
                MultiPlatformAdminBot.Common.Statics.Log(ex);
            }
        }

        private async Task AskForConfirmationAsync(List<BroadcastMessageRequest> requests)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<global::Application.Interfaces.IAppDbContext>();
            var clients = scope.ServiceProvider.GetRequiredService<IEnumerable<global::Application.Interfaces.IInfrastructure.IBotPlatformClient>>();
            
            var firstReq = requests.First();
            long userId = firstReq.UserId;
            string platform = firstReq.SourcePlatform;

            var pending = new PendingBroadcastTBL
            {
                AdminUserId = userId,
                UpdatesJson = JsonConvert.SerializeObject(requests),
                DateInsert = DateTime.UtcNow,
                Status = 0,
                Type = 1
            };

            db.PendingBroadcastTBLs.Add(pending);
            await db.SaveChangesAsync();

            var tClientAdp = clients.FirstOrDefault(c => c.PlatformName == "Telegram") as Platforms.Telegram.TelegramBotClientAdapter;
            var tClient = tClientAdp?.Client;
            
            if (tClient != null && platform == "Telegram")
            {
                // Delete all proxy-generated "لطفا صبر کنید..." messages
                foreach (var req in requests)
                {
                    if (req.WaitMessageId.HasValue && req.WaitMessageId.Value != 0)
                    {
                        try { await tClient.DeleteMessageAsync(userId, req.WaitMessageId.Value); } catch { }
                        req.WaitMessageId = null;
                    }
                }

                var ik = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
                {
                    new [] {
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("✅ ثبت و ارسال", $"send_pending_{pending.ID}"),
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("⏰ زمان‌بندی", $"sched_pending_{pending.ID}")
                    },
                    new [] {
                        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("❌ حذف", $"del_pending_{pending.ID}")
                    }
                });

                string msg = requests.Count > 1 
                    ? $"پیام آلبوم شما با {requests.Count} فایل دریافت شد. آیا مایل به ارسال هستید؟"
                    : "پیام شما دریافت شد. آیا مایل به ارسال هستید؟";
                    
                var confirmMsg = await tClient.SendTextMessageAsync(userId, msg, replyMarkup: ik);

                if (requests.Count > 0 && confirmMsg != null)
                {
                    requests[0].WaitMessageId = confirmMsg.MessageId;
                    pending.UpdatesJson = JsonConvert.SerializeObject(requests);
                    await db.SaveChangesAsync();
                }
            }
        }
    }
}
