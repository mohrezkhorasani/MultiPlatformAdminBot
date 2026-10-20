using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entity;

namespace MultiPlatformAdminBot.Persistence
{
    public class BotTokenTBLConfiguration : IEntityTypeConfiguration<BotTokenTBL>
    {
        public void Configure(EntityTypeBuilder<BotTokenTBL> builder)
        {
            builder.HasKey(x => x.ID);
            builder.Property(x => x.PlatformName).HasMaxLength(100);
            builder.Property(x => x.TokenValue).HasMaxLength(500);
        }
    }
}
