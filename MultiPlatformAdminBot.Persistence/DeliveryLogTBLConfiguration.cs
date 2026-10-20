using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entity;

namespace MultiPlatformAdminBot.Persistence
{
    public class DeliveryLogTBLConfiguration : IEntityTypeConfiguration<DeliveryLogTBL>
    {
        public void Configure(EntityTypeBuilder<DeliveryLogTBL> builder)
        {
            builder.ToTable("DeliveryLogTBL", "BotCore");
            builder.HasKey(x => x.ID);
            builder.Property(x => x.ID).UseIdentityColumn();
            builder.Property(x => x.PlatformName).HasMaxLength(50).IsRequired();
            builder.Property(x => x.TargetChannelId).HasMaxLength(255).IsRequired();
            builder.Property(x => x.ContentText).HasMaxLength(4000).IsRequired(false);
            builder.Property(x => x.MediaPath).HasMaxLength(500).IsRequired(false);
            builder.Property(x => x.MediaFileName).HasMaxLength(255).IsRequired(false);
            builder.Property(x => x.LastError).HasMaxLength(1000).IsRequired(false);
            builder.Property(x => x.DateInsert).HasDefaultValueSql("SYSDATETIME()");

            builder.HasIndex(x => new { x.Status, x.NextRetryAt });
            builder.HasIndex(x => x.AdminUserId);
        }
    }
}
