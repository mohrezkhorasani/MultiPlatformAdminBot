using MediatR;

namespace MultiPlatformAdminBot.Application.Notifications
{
    public class AppModeChangedNotification : INotification { }
    public class PlatformConfigChangedNotification : INotification { }
    public class AdminChannelChangedNotification : INotification
    {
        public long AdminUserId { get; }
        public AdminChannelChangedNotification(long adminUserId)
        {
            AdminUserId = adminUserId;
        }
    }
}
