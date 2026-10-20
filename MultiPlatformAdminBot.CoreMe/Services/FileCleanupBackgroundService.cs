using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Telegram.Bot;

namespace MultiPlatformAdminBot.CoreMe.Services
{
    public class FileCleanupBackgroundService : BackgroundService
    {
        private readonly ILogger<FileCleanupBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public FileCleanupBackgroundService(ILogger<FileCleanupBackgroundService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOldFilesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing file cleanup.");
                }

                // Wait 1 hour before next cleanup check
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private async Task CleanupOldFilesAsync(CancellationToken stoppingToken)
        {
            string wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string uploadsPath = Path.Combine(wwwrootPath, "uploads");

            if (!Directory.Exists(uploadsPath))
            {
                return;
            }

            var files = Directory.GetFiles(uploadsPath);
            var oldFiles = new List<string>();

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (DateTime.Now - fileInfo.CreationTime > TimeSpan.FromHours(24))
                {
                    oldFiles.Add(file);
                }
            }

            if (!oldFiles.Any())
            {
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<global::Application.Interfaces.IAppDbContext>();
            
            var telegramTokenEntity = dbContext.BotTokenTBLs.FirstOrDefault(t => t.PlatformName == "Telegram");
            if (telegramTokenEntity == null || string.IsNullOrEmpty(telegramTokenEntity.TokenValue))
            {
                _logger.LogWarning("Telegram token not found, skipping report but deleting files.");
                DeleteFiles(oldFiles);
                return;
            }

            var httpClientFactory = scope.ServiceProvider.GetRequiredService<System.Net.Http.IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("TelegramProxyClient");
            var telegramClient = new TelegramBotClient(telegramTokenEntity.TokenValue, httpClient);

            long[] adminIds = new long[] { 690534265, 8658914813 };
            string caption = "(فایل های پیوست از سرور پاکسازی شدند #پاکسازی)";

            // Chunk files by 10 for albums
            for (int i = 0; i < oldFiles.Count; i += 10)
            {
                var chunk = oldFiles.Skip(i).Take(10).ToList();
                var albumGroup = new List<Telegram.Bot.Types.IAlbumInputMedia>();
                var streams = new List<Stream>();

                try
                {
                    for (int j = 0; j < chunk.Count; j++)
                    {
                        var filePath = chunk[j];
                        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        streams.Add(stream);
                        var ext = Path.GetExtension(filePath).ToLower();
                        var fileName = Path.GetFileName(filePath);
                        if (ext == ".mp4" || ext == ".mkv" || ext == ".avi")
                        {
                            var videoMedia = new Telegram.Bot.Types.InputMediaVideo(new Telegram.Bot.Types.InputFileStream(stream, fileName));
                            if (j == 0) videoMedia.Caption = caption;
                            albumGroup.Add(videoMedia);
                        }
                        else
                        {
                            var photoMedia = new Telegram.Bot.Types.InputMediaPhoto(new Telegram.Bot.Types.InputFileStream(stream, fileName));
                            if (j == 0) photoMedia.Caption = caption;
                            albumGroup.Add(photoMedia);
                        }
                    }

                    foreach (var adminId in adminIds)
                    {
                        try
                        {
                            await telegramClient.SendMediaGroupAsync(new Telegram.Bot.Types.ChatId(adminId), albumGroup, cancellationToken: stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to send cleanup report to admin {adminId}");
                        }
                    }
                }
                finally
                {
                    foreach (var s in streams)
                    {
                        s.Dispose();
                    }
                }

                // Delete the chunk
                DeleteFiles(chunk);
            }
        }

        private void DeleteFiles(List<string> files)
        {
            foreach (var file in files)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to delete old file: {file}");
                }
            }
        }
    }
}

