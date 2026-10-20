using Application.Interfaces.IInfrastructure;
using System.IO;
using System.Threading.Tasks;
using Telegram.Bot;

namespace MultiPlatformAdminBot.Infrastructure.Services
{
    public class TelegramMediaDownloader : ISourceMediaDownloader
    {
        private readonly ITelegramBotClient _client;

        public TelegramMediaDownloader(global::Application.Interfaces.IAppDbContext dbContext, System.Net.Http.IHttpClientFactory httpClientFactory)
        {
            var tokenEntity = System.Linq.Enumerable.FirstOrDefault(dbContext.BotTokenTBLs, t => t.PlatformName == "Telegram");
            var token = tokenEntity?.TokenValue ?? "TELEGRAM_BOT_TOKEN_PLACEHOLDER";
            var httpClient = httpClientFactory.CreateClient("TelegramProxyClient");
            _client = new TelegramBotClient(token, httpClient);
        }

        public async Task<string> DownloadFileAsync(string fileId, string sourcePlatform, System.IProgress<double> progress = null)
        {
            if (sourcePlatform != "Telegram") return null;

            var fileInfo = await _client.GetFileAsync(fileId);
            var extension = Path.GetExtension(fileInfo.FilePath);
            if (string.IsNullOrEmpty(extension)) extension = ".dat";

            string wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string uploadsPath = Path.Combine(wwwrootPath, "uploads");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            string fileName = $"{System.Guid.NewGuid()}{extension}";
            string filePath = Path.Combine(uploadsPath, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                long totalBytes = fileInfo.FileSize ?? 0;
                if (progress != null && totalBytes > 0)
                {
                    using var progressStream = new ProgressStream(fileStream, progress, totalBytes);
                    await _client.DownloadFileAsync(fileInfo.FilePath, progressStream);
                }
                else
                {
                    await _client.DownloadFileAsync(fileInfo.FilePath, fileStream);
                }
            }

            return filePath;
        }
    }
}
