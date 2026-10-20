using Domain.Entity;
using Microsoft.EntityFrameworkCore;
using MultiPlatformAdminBot.Common;
using MultiPlatformAdminBot.Persistence;

namespace MultiPlatformAdminBot.CoreMe
{
    /// <summary>
    /// One-time database seeding. Runs on startup but is strictly idempotent:
    ///  - Applies pending migrations.
    ///  - Ensures the known admin rows exist (without any hard-coded passwords).
    ///  - Migrates legacy plaintext passwords to PBKDF2 hashes in place (once).
    ///  - Only when "Seeder:SeedDefaults" = "true": inserts default bot tokens
    ///    from configuration (never from source code) for a fresh deployment.
    /// </summary>
    public class DbSeeder
    {
        private readonly BotDbContext _db;
        private readonly IConfiguration _config;

        public DbSeeder(BotDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public void Seed()
        {
            _db.Database.Migrate();

            EnsureAdmin(8658914813, "Super Admin", hasPanelAccess: true);
            EnsureAdmin(690534265, "Owner", hasPanelAccess: true);
            EnsureAdmin(1067483648, "Bale Admin", hasPanelAccess: false);

            MigrateLegacyPasswords();

            var seedDefaults = string.Equals(_config["Seeder:SeedDefaults"], "true", StringComparison.OrdinalIgnoreCase);
            if (seedDefaults)
            {
                SeedDefaultToken("Telegram", "Seeder:TelegramToken");
                SeedDefaultToken("Bale", "Seeder:BaleToken");
                SeedDefaultToken("Eitaa", "Seeder:EitaaToken");
                SeedDefaultToken("Rubika", "Seeder:RubikaToken");
            }

            _db.SaveChanges();
        }

        private void EnsureAdmin(long id, string explain, bool hasPanelAccess)
        {
            var admin = _db.AdminUserTBLs.FirstOrDefault(u => u.ID == id);
            if (admin == null)
            {
                _db.AdminUserTBLs.Add(new AdminUserTBL
                {
                    ID = id,
                    Status = 100,
                    Type = 1,
                    DateInsert = DateTime.UtcNow,
                    Explain = explain,
                    HasPanelAccess = hasPanelAccess,
                    CurrentRoute = "Home",
                    MaxChannelsPerToken = 100,
                    MaxTotalChannels = 100
                });
                return;
            }

            // Keep privileges in sync; never touch the password here.
            admin.Status = 100;
            admin.HasPanelAccess = hasPanelAccess;
        }

        private void MigrateLegacyPasswords()
        {
            var admins = _db.AdminUserTBLs.Where(a => a.PasswordHash != null).ToList();
            foreach (var a in admins)
            {
                if (PasswordHasher.IsLegacyPlaintext(a.PasswordHash))
                {
                    a.PasswordHash = PasswordHasher.Hash(a.PasswordHash!);
                }
            }
        }

        private void SeedDefaultToken(string platformName, string configKey)
        {
            var tokenValue = _config[configKey];
            if (string.IsNullOrWhiteSpace(tokenValue)) return;
            if (_db.BotTokenTBLs.Any(t => t.PlatformName == platformName)) return;

            _db.BotTokenTBLs.Add(new BotTokenTBL
            {
                PlatformName = platformName,
                TokenValue = tokenValue,
                IsActive = true,
                Status = 1,
                DateInsert = DateTime.UtcNow
            });
        }
    }
}
