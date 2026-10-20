using Microsoft.EntityFrameworkCore;
using Domain.Entity;
using Application.Interfaces;

namespace MultiPlatformAdminBot.Persistence
{
    public class BotDbContext : DbContext, IAppDbContext
    {
        public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) { }

        public DbSet<AdminUserTBL> AdminUserTBLs { get; set; }
        public DbSet<BotTokenTBL> BotTokenTBLs { get; set; }
        public DbSet<PlatformConfigTBL> PlatformConfigTBLs { get; set; }
        public DbSet<BroadcastLogTBL> BroadcastLogTBLs { get; set; }
        public DbSet<AdminChannelTBL> AdminChannelTBLs { get; set; }
        public DbSet<SystemSettingTBL> SystemSettings { get; set; }
        public DbSet<PostReportTBL> PostReportTBLs { get; set; }
        public DbSet<PostReportMetaDataTBL> PostReportMetaDataTBLs { get; set; }
        public DbSet<AdminRequestTBL> AdminRequestTBLs { get; set; }
        public DbSet<PendingBroadcastTBL> PendingBroadcastTBLs { get; set; }
        public DbSet<DeliveryLogTBL> DeliveryLogTBLs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new AdminUserTBLConfiguration());
            modelBuilder.ApplyConfiguration(new BotTokenTBLConfiguration());
            modelBuilder.ApplyConfiguration(new PlatformConfigTBLConfiguration());

            modelBuilder.Entity<BotTokenTBL>()
                .HasOne(b => b.Owner)
                .WithMany()
                .HasForeignKey(b => b.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.ApplyConfiguration(new BroadcastLogTBLConfiguration());
            modelBuilder.ApplyConfiguration(new DeliveryLogTBLConfiguration());
            
            modelBuilder.Entity<AdminChannelTBL>()
                .HasKey(ac => ac.ID);

            modelBuilder.Entity<AdminChannelTBL>()
                .HasIndex(ac => new { ac.AdminUserId, ac.ChannelId })
                .IsUnique();

            modelBuilder.Entity<AdminChannelTBL>()
                .HasOne(ac => ac.AdminUser)
                .WithMany(u => u.AdminChannels)
                .HasForeignKey(ac => ac.AdminUserId);

            modelBuilder.Entity<AdminChannelTBL>()
                .HasOne(ac => ac.Channel)
                .WithMany(c => c.AdminChannels)
                .HasForeignKey(ac => ac.ChannelId);

            modelBuilder.Entity<PlatformConfigTBL>()
                .HasOne(p => p.BotToken)
                .WithMany(t => t.Channels)
                .HasForeignKey(p => p.BotTokenId);

            modelBuilder.Entity<PostReportTBL>()
                .HasOne(p => p.AdminChannel)
                .WithMany()
                .HasForeignKey(p => p.AdminChannelId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PostReportMetaDataTBL>()
                .HasOne(m => m.PostReport)
                .WithMany(p => p.MetaDatas)
                .HasForeignKey(m => m.PostReportId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PendingBroadcastTBL>()
                ;
        }
    }
}
