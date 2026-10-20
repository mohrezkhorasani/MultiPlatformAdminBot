using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entity;

namespace MultiPlatformAdminBot.Persistence
{
    public class AdminUserTBLConfiguration : IEntityTypeConfiguration<AdminUserTBL>
    {
        public void Configure(EntityTypeBuilder<AdminUserTBL> builder)
        {
            builder.ToTable("AdminUserTBL", "BotCore");
            builder.HasKey(x => x.ID);
            builder.Property(x => x.ID).ValueGeneratedNever(); // Chat ID is the ID
            builder.Property(e => e.Explain).IsRequired(false).HasMaxLength(3000);
            builder.Property(e => e.DateInsert).HasDefaultValueSql("SYSDATETIME()");
        }
    }
}
