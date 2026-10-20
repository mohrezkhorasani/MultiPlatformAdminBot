using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entity;

namespace MultiPlatformAdminBot.Persistence
{
    public class BroadcastLogTBLConfiguration : IEntityTypeConfiguration<BroadcastLogTBL>
    {
        public void Configure(EntityTypeBuilder<BroadcastLogTBL> builder)
        {
            builder.ToTable("BroadcastLogTBL", "BotCore");
            builder.HasKey(x => x.ID);
            builder.Property(x => x.ID).UseIdentityColumn();
            builder.Property(e => e.Explain).IsRequired(false).HasMaxLength(3000);
            builder.Property(e => e.DateInsert).HasDefaultValueSql("SYSDATETIME()");
            
            builder.HasOne(x => x.AdminUser)
                   .WithMany()
                   .HasForeignKey(x => x.AdminUserId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
