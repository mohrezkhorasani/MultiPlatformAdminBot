using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using MultiPlatformAdminBot.Application.Notifications;

namespace MultiPlatformAdminBot.Application.Handlers
{
    public class CacheInvalidationHandler : 
        INotificationHandler<AppModeChangedNotification>,
        INotificationHandler<PlatformConfigChangedNotification>,
        INotificationHandler<AdminChannelChangedNotification>
    {
        private readonly IMemoryCache _cache;

        public CacheInvalidationHandler(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task Handle(AppModeChangedNotification notification, CancellationToken cancellationToken)
        {
            _cache.Remove("AppMode");
            return Task.CompletedTask;
        }

        public Task Handle(PlatformConfigChangedNotification notification, CancellationToken cancellationToken)
        {
            _cache.Remove("AllPlatformConfigs");
            return Task.CompletedTask;
        }

        public Task Handle(AdminChannelChangedNotification notification, CancellationToken cancellationToken)
        {
            _cache.Remove("AllAdminChannels");
            return Task.CompletedTask;
        }
    }
}
