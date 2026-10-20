using Application.DTOs;
using Application.Interfaces.UseCases;
using MultiPlatformAdminBot.CoreMe.Presenter;
using MultiPlatformAdminBot.Common;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MultiPlatformAdminBot.CoreMe.Services
{
    public enum BroadcastJobKind { InboundUpdate, PendingBroadcast, EditMessage }

    /// <summary>
    /// A unit of work for the background broadcast pipeline.
    /// - InboundUpdate: a webhook request that must be aggregated (album grouping / confirmation).
    /// - PendingBroadcast: a confirmed broadcast (single or album) that must be delivered.
    /// </summary>
    public sealed class BroadcastJob
    {
        public BroadcastJobKind Kind { get; init; }
        public BroadcastMessageRequest? Request { get; init; }
        public List<BroadcastMessageRequest>? Requests { get; init; }
        public List<long>? RetryConfigIds { get; init; }
    }

    /// <summary>
    /// Background worker that executes broadcasts OUTSIDE the webhook request,
    /// so the webhook can acknowledge 200 immediately (avoids Telegram webhook
    /// timeouts/retries during long media downloads + multi-platform uploads).
    ///
    /// Jobs are processed sequentially by design (per-platform rate limits);
    /// parallelism per platform is a later hardening step.
    /// </summary>
    public class BroadcastWorkerService : BackgroundService
    {
        private readonly Channel<BroadcastJob> _channel;
        private readonly IServiceProvider _serviceProvider;

        public BroadcastWorkerService(Channel<BroadcastJob> channel, IServiceProvider serviceProvider)
        {
            _channel = channel;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var sp = scope.ServiceProvider;

                    switch (job.Kind)
                    {
                        case BroadcastJobKind.InboundUpdate:
                            var aggregator = sp.GetRequiredService<IAlbumAggregatorService>();
                            await aggregator.ProcessRequestAsync(job.Request!);
                            break;

                        case BroadcastJobKind.PendingBroadcast:
                            var useCase = sp.GetRequiredService<IBroadcastMessageUseCase>();
                            var presenter = sp.GetRequiredService<ApiPresenter<BaseResponseResponse>>();
                            var reqs = job.Requests!;
                            if (reqs.Count == 1) await useCase.HandleAsync(reqs[0], presenter);
                            else await useCase.HandleAlbumAsync(reqs, presenter);
                            break;

                        case BroadcastJobKind.EditMessage:
                            var editUseCase = sp.GetRequiredService<IBroadcastMessageUseCase>();
                            if (job.Request != null)
                            {
                                await editUseCase.HandleEditMessageAsync(job.Request);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // A failed job must never kill the worker loop.
                    Statics.Log(ex);
                }
            }
        }
    }
}
