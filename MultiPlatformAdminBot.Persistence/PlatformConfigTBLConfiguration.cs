using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entity;

namespace MultiPlatformAdminBot.Persistence
{
    public class PlatformConfigTBLConfiguration : IEntityTypeConfiguration<PlatformConfigTBL>
    {
        public void Configure(EntityTypeBuilder<PlatformConfigTBL> builder)
        {
            builder.ToTable("PlatformConfigTBL", "BotCore");
            builder.HasKey(x => x.ID);
            builder.Property(x => x.ID).UseIdentityColumn();
            builder.Property(e => e.Explain).IsRequired(false).HasMaxLength(3000);
            builder.Property(e => e.DateInsert).HasDefaultValueSql("SYSDATETIME()");
            
            builder.Property(e => e.PlatformName).HasMaxLength(50).IsRequired();
            builder.Property(e => e.TargetChannelId).HasMaxLength(100).IsRequired();
        }
    }
}
