using Domain.Entity;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IAppDbContext
    {
        DbSet<AdminUserTBL> AdminUserTBLs { get; set; }
        DbSet<BotTokenTBL> BotTokenTBLs { get; set; }
        DbSet<PlatformConfigTBL> PlatformConfigTBLs { get; set; }
        DbSet<BroadcastLogTBL> BroadcastLogTBLs { get; set; }
        DbSet<AdminChannelTBL> AdminChannelTBLs { get; set; }
        DbSet<SystemSettingTBL> SystemSettings { get; set; }
        DbSet<PostReportTBL> PostReportTBLs { get; set; }
        DbSet<PostReportMetaDataTBL> PostReportMetaDataTBLs { get; set; }
        DbSet<AdminRequestTBL> AdminRequestTBLs { get; set; }
        DbSet<DeliveryLogTBL> DeliveryLogTBLs { get; set; }
        DbSet<PendingBroadcastTBL> PendingBroadcastTBLs { get; set; }

        int SaveChanges();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
