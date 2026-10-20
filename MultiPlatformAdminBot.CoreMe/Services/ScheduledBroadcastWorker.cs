using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MultiPlatformAdminBot.Common;
using MultiPlatformAdminBot.Persistence;

namespace MultiPlatformAdminBot.CoreMe.Services
{
    /// <summary>
    /// Periodic background service that queries PendingBroadcastTBLs for messages
    /// whose ScheduledAtUtc <= DateTime.UtcNow and enqueues them for delivery.
    /// </summary>
    public class ScheduledBroadcastWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Channel<BroadcastJob> _broadcastChannel;
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        public ScheduledBroadcastWorker(IServiceProvider serviceProvider, Channel<BroadcastJob> broadcastChannel)
        {
            _serviceProvider = serviceProvider;
            _broadcastChannel = broadcastChannel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndEnqueueDueBroadcastsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    Statics.Log(ex);
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public async Task CheckAndEnqueueDueBroadcastsAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

            var nowUtc = DateTime.UtcNow;

            // Find all pending drafts that are scheduled and due
            var dueDrafts = await db.PendingBroadcastTBLs
                .Where(p => p.Status == 3 && p.ScheduledAtUtc.HasValue && p.ScheduledAtUtc.Value <= nowUtc)
                .ToListAsync(cancellationToken);

            if (!dueDrafts.Any()) return;

            foreach (var draft in dueDrafts)
            {
                // Concurrency guard: Mark as Processing (4) so no other loop picks it up
                draft.Status = 4;
            }

            await db.SaveChangesAsync(cancellationToken);

            foreach (var draft in dueDrafts)
            {
                try
                {
                    var reqs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<BroadcastMessageRequest>>(draft.UpdatesJson);
                    if (reqs != null && reqs.Count > 0)
                    {
                        await _broadcastChannel.Writer.WriteAsync(
                            new BroadcastJob
                            {
                                Kind = BroadcastJobKind.PendingBroadcast,
                                Requests = reqs
                            }, cancellationToken);

                        // Mark as confirmed/sent (1)
                        draft.Status = 1;
                    }
                    else
                    {
                        draft.Status = 2; // Invalid payload
                    }
                }
                catch (Exception ex)
                {
                    Statics.Log(ex);
                    draft.Status = 2; // Error
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
