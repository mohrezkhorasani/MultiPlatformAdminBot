using Application.DTOs;
using Application.Interfaces;
using Application.Interfaces.UseCases;
using Application.Interfaces.IInfrastructure;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Telegram.Bot.Types.Enums;
using System;
using Microsoft.Extensions.Caching.Memory;

namespace Application.UseCases
{
    public class BroadcastMessageUseCase : IBroadcastMessageUseCase
    {
        private readonly IEnumerable<IBotPlatformClient> _platformClients;
        private readonly IEnumerable<ISourceMediaDownloader> _mediaDownloaders;
        private readonly IAppDbContext _db;
        private readonly IMemoryCache _cache;

        public BroadcastMessageUseCase(
            IEnumerable<IBotPlatformClient> platformClients, 
            IEnumerable<ISourceMediaDownloader> mediaDownloaders,
            IAppDbContext db,
            IMemoryCache cache)
        {
            _platformClients = platformClients;
            _mediaDownloaders = mediaDownloaders;
            _db = db;
            _cache = cache;
        }

        public async Task HandleAsync(BroadcastMessageRequest request, IOutPutPort<GenericResponse<BaseResponseResponse>> outputPort)
        {
            var admin = await _db.AdminUserTBLs.FirstOrDefaultAsync(u => u.ID == request.UserId && u.Status == 100);
            if (admin == null) 
            {
                outputPort.Handle(new GenericResponse<BaseResponseResponse>(false, "Unauthorized Admin"));
                return;
            }

            var message = request.Update.Message;
            if (message == null) 
            {
                outputPort.Handle(new GenericResponse<BaseResponseResponse>(false, "No Message Content"));
                return;
            }

            var content = Application.Utils.TelegramHtmlFormatter.GetHtmlText(message);
            if (!string.IsNullOrWhiteSpace(admin.FooterText))
            {
                content += "\n\n" + admin.FooterText;
            }
            
            // 2. Extract Media Info
            string fileId = null;
            string fileName = "file";
            MessageType msgType = message.Type;

            if (msgType == MessageType.Photo && message.Photo != null)
            {
                fileId = message.Photo.Last().FileId;
                fileName = "photo.jpg";
            }
            else if (msgType == MessageType.Video && message.Video != null)
            {
                fileId = message.Video.FileId;
                fileName = message.Video.FileName ?? "video.mp4";
            }
            else if (msgType == MessageType.Document && message.Document != null)
            {
                fileId = message.Document.FileId;
                fileName = message.Document.FileName ?? "document.file";
            }
            else if (msgType == MessageType.Audio && message.Audio != null)
            {
                fileId = message.Audio.FileId;
                fileName = message.Audio.FileName ?? (!string.IsNullOrEmpty(message.Audio.Title) ? $"{message.Audio.Title}.mp3" : "audio.mp3");
            }
            else if (msgType == MessageType.Voice && message.Voice != null)
            {
                fileId = message.Voice.FileId;
                fileName = "voice.ogg";
            }

            var sourceClient = _platformClients.FirstOrDefault(c => c.PlatformName == request.SourcePlatform);
            string mediaFilePath = null;
            if (fileId != null)
            {
                var lastUpdate = System.DateTime.MinValue;
                var progress = new System.Progress<double>(async p => {
                    if (System.DateTime.Now - lastUpdate > System.TimeSpan.FromSeconds(2) || p >= 100) {
                        lastUpdate = System.DateTime.Now;
                        await SendOrUpdateStatusAsync(sourceClient, admin.ID, request.WaitMessageId, $"⏳ در حال دانلود: {p:F0}٪");
                    }
                });

                foreach(var d in _mediaDownloaders)
                {
                    mediaFilePath = await d.DownloadFileAsync(fileId, request.SourcePlatform, progress);
                    if (mediaFilePath != null) break;
                }
            }

            var allAdminChannels = await _cache.GetOrCreateAsync("AllAdminChannels", async entry => {
                // Backstop for any mutation path that forgets to publish invalidation.
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                return await _db.AdminChannelTBLs.ToListAsync();
            });

            var assignedChannelIds = allAdminChannels
                .Where(ac => ac.AdminUserId == admin.ID)
                .Select(ac => ac.ChannelId)
                .ToList();

            int appMode = await _cache.GetOrCreateAsync("AppMode", async entry => {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "AppMode");
                return setting != null && int.TryParse(setting.Value, out int m) ? m : 2;
            });

            var allPlatformConfigs = await _cache.GetOrCreateAsync("AllPlatformConfigs", async entry => {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                return await _db.PlatformConfigTBLs.ToListAsync();
            });

            var activeConfigsQuery = allPlatformConfigs
                .Where(p => p.IsActive && assignedChannelIds.Contains(p.ID));

            if (request.RetryConfigIds != null && request.RetryConfigIds.Any())
            {
                activeConfigsQuery = activeConfigsQuery.Where(p => request.RetryConfigIds.Contains(p.ID));
            }

            if (appMode == 1)
            {
                activeConfigsQuery = activeConfigsQuery.Where(p => p.Type == 1);
            }
            else if (appMode == 2)
            {
                activeConfigsQuery = activeConfigsQuery.Where(p => p.Type == 1 || p.Type == 2);
            }

            var activeConfigs = activeConfigsQuery.ToList();
            
            bool success = true;
            string errorLog = "";
            
            await SendOrUpdateStatusAsync(sourceClient, admin.ID, request.WaitMessageId, "⏳ فایل‌ها دریافت شد. شروع فرایند ارسال...");

            var distinctPlatforms = activeConfigs.Select(c => c.PlatformName).Distinct().ToList();
            var platformWaitMessages = new System.Collections.Generic.Dictionary<string, int?>();

            foreach(var pName in distinctPlatforms)
            {
                var msgRes = await sourceClient.SendTextMessageAsync(admin.ID.ToString(), $"🔄 {pName}: در حال ارسال...", "html");
                if (msgRes.Success) if(int.TryParse(msgRes.MessageId, out int pid)) platformWaitMessages[pName] = pid;
            }

            var bLog = new Domain.Entity.BroadcastLogTBL
            {
                AdminUserId = admin.ID,
                ContentSummary = msgType.ToString(),
                IsSuccessful = true,
                ErrorLog = "",
                DateInsert = System.DateTime.UtcNow,
                Status = 1,
                Type = 1
            };
            _db.BroadcastLogTBLs.Add(bLog);
            await _db.SaveChangesAsync();

            // Send channels in parallel. Distinct platforms are independent; within
            // a platform, sends are throttled by a per-platform gate (max 2 in
            // flight) to respect the platform API rate limits.
            var platformGates = new System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim>();
            var dbLock = new object();
            var sendTasks = new List<Task>();

            var channelReplyToIds = new Dictionary<long, string?>();
            if (request.ReplyToSourceMessageId.HasValue)
            {
                var sourceMsgId = request.ReplyToSourceMessageId.Value;
                var allAdminChannelIds = allAdminChannels.Select(ac => ac.ID).ToList();
                var previousReports = _db.PostReportTBLs
                    .Where(r => r.SourceMessageId == sourceMsgId && r.AdminChannelId != null && allAdminChannelIds.Contains(r.AdminChannelId.Value))
                    .ToList();
                foreach (var pr in previousReports)
                {
                    channelReplyToIds[pr.AdminChannelId.Value] = pr.DestinationMessageId;
                }
            }

            foreach(var config in activeConfigs)
            {
                var client = _platformClients.FirstOrDefault(c => c.PlatformName == config.PlatformName);
                if (client == null) continue;
                var gate = platformGates.GetOrAdd(config.PlatformName, _ => new SemaphoreSlim(2, 2));

                sendTasks.Add(Task.Run(async () =>
                {
                    Domain.Entity.PostReportTBL postReport = null;

                    var adminChannel = allAdminChannels.FirstOrDefault(ac => ac.AdminUserId == admin.ID && ac.ChannelId == config.ID);

                    if (adminChannel != null)
                    {
                        postReport = new Domain.Entity.PostReportTBL
                        {
                            AdminChannelId = adminChannel.ID,
                            IsSuccessful = true,
                            InOutType = 2,
                            DateInsert = System.DateTime.Now,
                            Status = 1,
                            Type = 1,
                            Explain = "",
                            SourceMessageId = request.Update.Message?.MessageId
                        };

                        string txt = request.Update.Message?.Text ?? request.Update.Message?.Caption ?? "";
                        string snd = request.Update.Message?.From?.Id.ToString() ?? admin.ID.ToString();

                        postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "content", MetaValue = txt, DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                        postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "senderId", MetaValue = snd, DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                        postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "targetChatId", MetaValue = config.TargetChannelId ?? "", DateInsert = System.DateTime.Now, Status = 1, Type = 1 });

                        if (msgType != MessageType.Text && !string.IsNullOrEmpty(fileId))
                        {
                            postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "fileId", MetaValue = fileId, DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                        }
                    }
                    string? replyToId = null;
                    if (adminChannel != null && channelReplyToIds.ContainsKey(adminChannel.ID))
                    {
                        replyToId = channelReplyToIds[adminChannel.ID];
                    }

                    // Per-channel result: a failure here must not corrupt the
                    // reports of the other channels.
                    bool configSucceeded = true;
                    string configError = "";
                    var delivery = new Domain.Entity.DeliveryLogTBL
                    {
                        BroadcastLogId = bLog.ID,
                        AdminUserId = admin.ID,
                        PlatformName = config.PlatformName,
                        TargetChannelId = config.TargetChannelId,
                        ContentType = msgType switch
                        {
                            MessageType.Photo => 2,
                            MessageType.Video => 3,
                            MessageType.Document => 4,
                            MessageType.Audio => 5,
                            MessageType.Voice => 6,
                            _ => 1
                        },
                        ContentText = msgType == MessageType.Text ? content : null,
                        MediaPath = mediaFilePath,
                        MediaFileName = fileName,
                        Status = 1,
                        Type = 1,
                        DateInsert = System.DateTime.UtcNow
                    };
                    await gate.WaitAsync();
                    try
                    {
                        if (msgType == MessageType.Text)
                        {
                            var (resOk, resMsgId) = await client.SendTextMessageAsync(config.TargetChannelId, content, "html", message, null, replyToId);
                            configSucceeded = resOk;
                            if (resOk && postReport != null) postReport.DestinationMessageId = resMsgId;
                            if (!configSucceeded) configError = "خطای HTTP یا پاسخناموفق از سرور API پلتفرم.";
                        }
                        else if (mediaFilePath != null)
                        {
                            using var mediaStream = new FileStream(mediaFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                            if (msgType == MessageType.Photo)
                            {
                                var pRes = await client.SendPhotoAsync(config.TargetChannelId, mediaStream, fileName, content, "html", message, replyToId);
                                configSucceeded = pRes.Success;
                                if (configSucceeded && postReport != null) postReport.DestinationMessageId = pRes.MessageId;
                            }
                            else if (msgType == MessageType.Video)
                            {
                                var vRes = await client.SendVideoAsync(config.TargetChannelId, mediaStream, fileName, content, "html", message, replyToId);
                                configSucceeded = vRes.Success;
                                if (configSucceeded && postReport != null) postReport.DestinationMessageId = vRes.MessageId;
                            }
                            else if (msgType == MessageType.Document || msgType == MessageType.Audio || msgType == MessageType.Voice)
                            {
                                var dRes = await client.SendDocumentAsync(config.TargetChannelId, mediaStream, fileName, content, "html", message, replyToId);
                                configSucceeded = dRes.Success;
                                if (configSucceeded && postReport != null) postReport.DestinationMessageId = dRes.MessageId;
                            }

                            if (!configSucceeded) configError = "پاسخ ارسال مدیا از پلتفرم ناموفق بود.";
                        }
                        else
                        {
                            configSucceeded = false;
                            configError = "فایل مدیا با موفقیت از سرور مبدأ دانلود نشد.";
                        }

                        if (configSucceeded)
                        {
                            delivery.Status = 2; // Success
                        }
                        else
                        {
                            MarkDeliveryFailed(delivery, configError, isPermanent: true);
                            if (postReport != null)
                            {
                                postReport.IsSuccessful = false;
                                postReport.ErrorLog = configError;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (postReport != null)
                        {
                            postReport.IsSuccessful = false;
                            postReport.ErrorLog = ex.Message;
                        }
                        configSucceeded = false;
                        configError = ex.Message;
                        MarkDeliveryFailed(delivery, ex.Message, isPermanent: IsPermanentDeliveryError(ex.Message));
                    }
                    finally
                    {
                        gate.Release();
                    }

                    // DbContext is not thread-safe: serialize all entity adds.
                    lock (dbLock)
                    {
                        _db.DeliveryLogTBLs.Add(delivery);
                        if (postReport != null)
                        {
                            _db.PostReportTBLs.Add(postReport);
                        }
                        if (!configSucceeded)
                        {
                            success = false;
                            errorLog += $"[{config.PlatformName} Error]: {configError}\n";
                        }
                    }
                }));
            }
            await Task.WhenAll(sendTasks);
            
            foreach (var pName in distinctPlatforms)
            {
                int? msgId = platformWaitMessages.ContainsKey(pName) ? platformWaitMessages[pName] : null;
                if (msgId.HasValue)
                {
                    var platformConfigs = activeConfigs.Where(c => c.PlatformName == pName).ToList();
                    bool platformHasFailed = false;
                    lock (dbLock)
                    {
                        platformHasFailed = errorLog.Contains($"[{pName} Error]");
                    }

                    if (platformHasFailed)
                    {
                        await SendOrUpdateStatusAsync(sourceClient, admin.ID, msgId, $"❌ {pName}: ارسال با خطا مواجه شد.");
                    }
                    else
                    {
                        await SendOrUpdateStatusAsync(sourceClient, admin.ID, msgId, $"✅ {pName}: ارسال با موفقیت انجام شد.");
                    }
                }
            }

            // 5. Log Result
            bLog.IsSuccessful = success;
            bLog.ErrorLog = errorLog;
            await _db.SaveChangesAsync();

            string finalMsg = success ? "🎉 عملیات ارسال با موفقیت پایان یافت." : $"⚠️ پایان عملیات با خطا:\n{errorLog}";
            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup? ikm = null;
            if (!success)
            {
                ikm = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
                {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("🔄 ارسال مجدد به ناموفق‌ها", $"retry_broadcast_{bLog.ID}")
                });
            }

            await SendOrUpdateStatusAsync(sourceClient, admin.ID, request.WaitMessageId, finalMsg, ikm);

            outputPort.Handle(new GenericResponse<BaseResponseResponse>(success, success ? "Broadcast successful" : "Broadcast finished with errors"));
        }

        public async Task HandleAlbumAsync(IEnumerable<BroadcastMessageRequest> requests, IOutPutPort<GenericResponse<BaseResponseResponse>> outputPort)
        {
            var reqList = requests.ToList();
            if (!reqList.Any()) return;
            
            var firstReq = reqList.First();
            var admin = await _db.AdminUserTBLs.FirstOrDefaultAsync(u => u.ID == firstReq.UserId && u.Status == 100);
            if (admin == null) return;

            var captionMsg = reqList.Select(r => r.Update.Message).FirstOrDefault(m => m != null && (!string.IsNullOrEmpty(m.Caption) || !string.IsNullOrEmpty(m.Text)));
            var caption = captionMsg != null ? Application.Utils.TelegramHtmlFormatter.GetHtmlText(captionMsg) : "";
            
            if (!string.IsNullOrWhiteSpace(admin.FooterText))
            {
                caption += "\n\n" + admin.FooterText;
            }
            
            var sourceClient = _platformClients.FirstOrDefault(c => c.PlatformName == firstReq.SourcePlatform);
            var mediaFilePaths = new List<string>();
            var fileNames = new List<string>();
            
            var lastUpdate = System.DateTime.MinValue;
            int totalFiles = reqList.Count;
            int currentFile = 1;
            
            var progress = new System.Progress<double>(async p => {
                if (System.DateTime.Now - lastUpdate > System.TimeSpan.FromSeconds(2) || p >= 100) {
                    lastUpdate = System.DateTime.Now;
                    await SendOrUpdateStatusAsync(sourceClient, admin.ID, firstReq.WaitMessageId, $"⏳ در حال دانلود فایل {currentFile} از {totalFiles}: {p:F0}٪");
                }
            });

            foreach (var req in reqList)
            {
                var msg = req.Update.Message;
                if (msg == null) continue;

                string fileId = null;
                string fName = "photo.jpg";
                
                if (msg.Type == MessageType.Photo && msg.Photo != null)
                {
                    fileId = msg.Photo.Last().FileId;
                }
                else if (msg.Type == MessageType.Video && msg.Video != null)
                {
                    fileId = msg.Video.FileId;
                    fName = msg.Video.FileName ?? "video.mp4";
                }
                else if (msg.Type == MessageType.Document && msg.Document != null)
                {
                    fileId = msg.Document.FileId;
                    fName = msg.Document.FileName ?? "document.file";
                }
                else if (msg.Type == MessageType.Audio && msg.Audio != null)
                {
                    fileId = msg.Audio.FileId;
                    fName = msg.Audio.FileName ?? (!string.IsNullOrEmpty(msg.Audio.Title) ? $"{msg.Audio.Title}.mp3" : "audio.mp3");
                }
                else if (msg.Type == MessageType.Voice && msg.Voice != null)
                {
                    fileId = msg.Voice.FileId;
                    fName = "voice.ogg";
                }

                if (fileId != null)
                {
                    foreach (var d in _mediaDownloaders)
                    {
                        var path = await d.DownloadFileAsync(fileId, req.SourcePlatform, progress);
                        if (path != null)
                        {
                            mediaFilePaths.Add(path);
                            fileNames.Add(fName);
                            break;
                        }
                    }
                    currentFile++;
                }
            }

            if (!mediaFilePaths.Any()) return;

            var allAdminChannels = await _cache.GetOrCreateAsync("AllAdminChannels", async entry => {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                return await _db.AdminChannelTBLs.ToListAsync();
            });

            var assignedChannelIds = allAdminChannels
                .Where(ac => ac.AdminUserId == admin.ID)
                .Select(ac => ac.ChannelId)
                .ToList();

            int appMode = await _cache.GetOrCreateAsync("AppMode", async entry => {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "AppMode");
                return setting != null && int.TryParse(setting.Value, out int m) ? m : 2;
            });

            var allPlatformConfigs = await _cache.GetOrCreateAsync("AllPlatformConfigs", async entry => {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                return await _db.PlatformConfigTBLs.ToListAsync();
            });

            var activeConfigsQuery = allPlatformConfigs
                .Where(p => p.IsActive && assignedChannelIds.Contains(p.ID));

            if (firstReq.RetryConfigIds != null && firstReq.RetryConfigIds.Any())
            {
                activeConfigsQuery = activeConfigsQuery.Where(p => firstReq.RetryConfigIds.Contains(p.ID));
            }

            if (appMode == 1)
            {
                activeConfigsQuery = activeConfigsQuery.Where(p => p.Type == 1);
            }
            else if (appMode == 2)
            {
                activeConfigsQuery = activeConfigsQuery.Where(p => p.Type == 1 || p.Type == 2);
            }

            var activeConfigs = activeConfigsQuery.ToList();
            bool success = true;
            string errorLog = "";
            await SendOrUpdateStatusAsync(sourceClient, admin.ID, firstReq.WaitMessageId, "✅ دانلود آلبوم به اتمام رسید. شروع ارسال به کانال‌ها...");

            var bLog = new Domain.Entity.BroadcastLogTBL
            {
                AdminUserId = admin.ID,
                ContentSummary = "Album (MediaGroup)",
                IsSuccessful = true,
                ErrorLog = "",
                DateInsert = System.DateTime.UtcNow,
                Status = 1,
                Type = 1
            };
            _db.BroadcastLogTBLs.Add(bLog);
            await _db.SaveChangesAsync();

            // Parallel album sends with the same per-platform throttling as above.
            var albumGates = new System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim>();
            var albumDbLock = new object();
            var albumTasks = new List<Task>();

            var channelAlbumReplyToIds = new Dictionary<long, string?>();
            if (firstReq?.ReplyToSourceMessageId.HasValue == true)
            {
                var sourceMsgId = firstReq.ReplyToSourceMessageId.Value;
                var allAdminChannelIds = allAdminChannels.Select(ac => ac.ID).ToList();
                var previousReports = _db.PostReportTBLs
                    .Where(r => r.SourceMessageId == sourceMsgId && r.AdminChannelId != null && allAdminChannelIds.Contains(r.AdminChannelId.Value))
                    .ToList();
                foreach (var pr in previousReports)
                {
                    channelAlbumReplyToIds[pr.AdminChannelId.Value] = pr.DestinationMessageId;
                }
            }

            foreach (var config in activeConfigs)
            {
                var client = _platformClients.FirstOrDefault(c => c.PlatformName == config.PlatformName);
                if (client == null) continue;
                var gate = albumGates.GetOrAdd(config.PlatformName, _ => new SemaphoreSlim(2, 2));

                albumTasks.Add(Task.Run(async () =>
                {
                    Domain.Entity.PostReportTBL postReport = null;
                    var delivery = new Domain.Entity.DeliveryLogTBL
                    {
                        BroadcastLogId = bLog.ID,
                        AdminUserId = admin.ID,
                        PlatformName = config.PlatformName,
                        TargetChannelId = config.TargetChannelId,
                        ContentType = 2, // Album (photo-group)
                        ContentText = caption,
                        MediaPath = mediaFilePaths.FirstOrDefault(),
                        MediaFileName = fileNames != null && fileNames.Count > 1 ? $"{fileNames.Count} files" : (fileNames?.FirstOrDefault()),
                        Status = 1,
                        Type = 1,
                        DateInsert = System.DateTime.UtcNow
                    };
                    bool configSucceeded = true;
                    string configError = "";
                    try
                    {
                        var adminChannel = allAdminChannels.FirstOrDefault(ac => ac.AdminUserId == admin.ID && ac.ChannelId == config.ID);

                        if (adminChannel != null)
                        {
                            postReport = new Domain.Entity.PostReportTBL
                            {
                                AdminChannelId = adminChannel.ID,
                                IsSuccessful = true,
                                InOutType = 2,
                                DateInsert = System.DateTime.Now,
                                Status = 1,
                                Type = 1,
                                Explain = "",
                                SourceMessageId = firstReq.Update.Message?.MessageId
                            };

                            string snd = firstReq.Update.Message?.From?.Id.ToString() ?? admin.ID.ToString();

                            postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "content", MetaValue = caption ?? "", DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                            postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "senderId", MetaValue = snd, DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                            postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "targetChatId", MetaValue = config.TargetChannelId ?? "", DateInsert = System.DateTime.Now, Status = 1, Type = 1 });

                            // Add each fileId as metadata
                            foreach (var req in reqList)
                            {
                                var m = req.Update.Message;
                                string fid = null;
                                if (m?.Type == MessageType.Photo) fid = m.Photo?.LastOrDefault()?.FileId;
                                else if (m?.Type == MessageType.Video) fid = m.Video?.FileId;
                                else if (m?.Type == MessageType.Document) fid = m.Document?.FileId;
                                else if (m?.Type == MessageType.Audio) fid = m.Audio?.FileId;
                                else if (m?.Type == MessageType.Voice) fid = m.Voice?.FileId;
                                if (fid != null)
                                {
                                    postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "album_fileId", MetaValue = fid, DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                                }
                            }
                        }
                        string? replyToId = null;
                        if (adminChannel != null && channelAlbumReplyToIds.ContainsKey(adminChannel.ID))
                        {
                            replyToId = channelAlbumReplyToIds[adminChannel.ID];
                        }
                        
                        await gate.WaitAsync();
                        try
                        {
                            var streams = new List<Stream>();
                            try
                            {
                                foreach (var p in mediaFilePaths) streams.Add(new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read));
                                var aRes = await client.SendMediaGroupAsync(config.TargetChannelId, streams, fileNames, caption, "html", captionMsg, replyToId);
                                configSucceeded = aRes.Success;
                                if (configSucceeded && postReport != null) postReport.DestinationMessageId = aRes.MessageId;
                                if (configSucceeded)
                                {
                                    delivery.Status = 2; // Success
                                }
                                else
                                {
                                    configError = "پاسخ ارسال آلبوم از سمت API پلتفرم ناموفق بود.";
                                    MarkDeliveryFailed(delivery, configError, isPermanent: true);
                                }
                            }
                            finally
                            {
                                foreach (var s in streams) s?.Dispose();
                            }
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }
                    catch (Exception ex)
                    {
                        configSucceeded = false;
                        configError = ex.Message;
                        if (postReport != null)
                        {
                            postReport.IsSuccessful = false;
                            postReport.ErrorLog = ex.Message;
                        }
                        MarkDeliveryFailed(delivery, ex.Message, isPermanent: IsPermanentDeliveryError(ex.Message));
                    }

                    lock (albumDbLock)
                    {
                        if (!configSucceeded)
                        {
                            success = false;
                            errorLog += $"[{config.PlatformName} Error]: {configError}\n";
                        }
                        if (postReport != null)
                        {
                            _db.PostReportTBLs.Add(postReport);
                        }
                        _db.DeliveryLogTBLs.Add(delivery);
                    }
                }));
            }
            await Task.WhenAll(albumTasks);

            bLog.IsSuccessful = success;
            bLog.ErrorLog = errorLog;
            await _db.SaveChangesAsync();

            string finalMsg = success ? "🎉 عملیات ارسال آلبوم با موفقیت به پایان رسید." : $"⚠️ ارسال آلبوم با خطاهایی پایان یافت:\n{errorLog}";
            Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup? ikm = null;
            if (!success)
            {
                ikm = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
                {
                    Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("🔄 ارسال مجدد به ناموفق‌ها", $"retry_broadcast_{bLog.ID}")
                });
            }

            await SendOrUpdateStatusAsync(sourceClient, admin.ID, firstReq.WaitMessageId, finalMsg, ikm);

            outputPort.Handle(new GenericResponse<BaseResponseResponse>(success, "Album broadcasted"));
        }

        private async Task SendOrUpdateStatusAsync(IBotPlatformClient sourceClient, long adminId, int? waitMessageId, string text, Telegram.Bot.Types.ReplyMarkups.IReplyMarkup? replyMarkup = null)
        {
            if (sourceClient == null) return;
            if (waitMessageId.HasValue && waitMessageId.Value != 0)
            {
                bool edited = await sourceClient.EditMessageTextAsync(adminId.ToString(), waitMessageId.Value, text, "html", replyMarkup: (Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup?)replyMarkup);
                if (!edited)
                {
                    await sourceClient.SendTextMessageAsync(adminId.ToString(), text, "html", replyMarkup: replyMarkup);
                }
            }
            else
            {
                await sourceClient.SendTextMessageAsync(adminId.ToString(), text, "html", replyMarkup: replyMarkup);
            }
        }

        // ---------- Per-delivery retry tracking ----------

        const int MaxDeliveryAttempts = 3;

        private static void MarkDeliveryFailed(Domain.Entity.DeliveryLogTBL delivery, string error, bool isPermanent)
        {
            delivery.AttemptCount += 1;
            delivery.LastError = error;
            if (isPermanent || delivery.AttemptCount >= MaxDeliveryAttempts)
            {
                // 3 = PermanentFailure (no more retries)
                delivery.Status = 3;
            }
            else
            {
                // 1 = AwaitingRetry; backoff 1m, 5m
                delivery.Status = 1;
                delivery.NextRetryAt = System.DateTime.UtcNow.AddMinutes(delivery.AttemptCount >= 2 ? 5 : 1);
            }
        }

        private static bool IsPermanentDeliveryError(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            // Auth/permission/400-class errors will not succeed on retry.
            return message.Contains("Unauthorized", System.StringComparison.OrdinalIgnoreCase)
                || message.Contains("Forbidden", System.StringComparison.OrdinalIgnoreCase)
                || message.Contains("Not Found", System.StringComparison.OrdinalIgnoreCase)
                || message.Contains("chat not found", System.StringComparison.OrdinalIgnoreCase)
                || message.Contains("bot was blocked", System.StringComparison.OrdinalIgnoreCase)
                || message.Contains("GROUP_CHAT_ID_INVALID", System.StringComparison.OrdinalIgnoreCase)
                || message.Contains("BOT_BLOCKED", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Manual retry of a failed delivery (called from the admin panel).
        /// Returns true when the resend succeeded.
        /// </summary>
        public async Task<bool> RetryDeliveryAsync(long deliveryId)
        {
            var delivery = await _db.DeliveryLogTBLs.FirstOrDefaultAsync(d => d.ID == deliveryId);
            if (delivery == null) return false;

            var client = _platformClients.FirstOrDefault(c => c.PlatformName == delivery.PlatformName);
            if (client == null) return false;

            try
            {
                bool sendSuccess = false;
                if (delivery.ContentType == 1 && !string.IsNullOrEmpty(delivery.ContentText))
                {
                    var (ok, _) = await client.SendTextMessageAsync(delivery.TargetChannelId, delivery.ContentText, "html");
                    sendSuccess = ok;
                }
                else if (delivery.ContentType == 2 && !string.IsNullOrEmpty(delivery.MediaPath) && System.IO.File.Exists(delivery.MediaPath))
                {
                    using var stream = new FileStream(delivery.MediaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    sendSuccess = (await client.SendPhotoAsync(delivery.TargetChannelId, stream, delivery.MediaFileName ?? "photo.jpg", delivery.ContentText ?? "")).Success;
                }
                else if (delivery.ContentType == 3 && !string.IsNullOrEmpty(delivery.MediaPath) && System.IO.File.Exists(delivery.MediaPath))
                {
                    using var stream = new FileStream(delivery.MediaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    sendSuccess = (await client.SendVideoAsync(delivery.TargetChannelId, stream, delivery.MediaFileName ?? "video.mp4", delivery.ContentText ?? "")).Success;
                }
                else if (delivery.ContentType == 4 && !string.IsNullOrEmpty(delivery.MediaPath) && System.IO.File.Exists(delivery.MediaPath))
                {
                    using var stream = new FileStream(delivery.MediaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    sendSuccess = (await client.SendDocumentAsync(delivery.TargetChannelId, stream, delivery.MediaFileName ?? "file", delivery.ContentText ?? "")).Success;
                }
                else
                {
                    // Media file was cleaned up: nothing to resend.
                    delivery.Status = 3;
                    delivery.LastError = "فایل مدیا در دسترس نیست (پاک شده است).";
                    await _db.SaveChangesAsync();
                    return false;
                }

                if (sendSuccess)
                {
                    delivery.Status = 2; // RetriedSent (Success)
                    delivery.LastError = null;
                    await _db.SaveChangesAsync();
                    return true;
                }
                else
                {
                    delivery.LastError = "پاسخ ارسال مجدد از سمت API ناموفق بود.";
                    delivery.Status = 1; // Failed, available for retry again
                    await _db.SaveChangesAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                delivery.LastError = ex.Message;
                delivery.Status = IsPermanentDeliveryError(ex.Message) ? 3 : 1;
                await _db.SaveChangesAsync();
                return false;
            }
        }

        public async Task HandleEditMessageAsync(BroadcastMessageRequest request)
        {
            var editedMessage = request.Update.EditedMessage ?? request.Update.Message;
            if (editedMessage == null) return;

            var admin = await _db.AdminUserTBLs.FirstOrDefaultAsync(u => u.ID == request.UserId && u.Status == 100);
            if (admin == null) return;

            int sourceMsgId = editedMessage.MessageId;

            // Find all previously successful post reports for this source message ID
            var previousReports = await _db.PostReportTBLs
                .Where(r => r.SourceMessageId == sourceMsgId && r.IsSuccessful && !string.IsNullOrEmpty(r.DestinationMessageId) && r.AdminChannelId != null)
                .ToListAsync();

            if (!previousReports.Any()) return;

            var newContent = Application.Utils.TelegramHtmlFormatter.GetHtmlText(editedMessage);
            if (!string.IsNullOrWhiteSpace(admin.FooterText))
            {
                newContent += "\n\n" + admin.FooterText;
            }

            var allAdminChannels = await _cache.GetOrCreateAsync("AllAdminChannels", async entry => {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                return await _db.AdminChannelTBLs.ToListAsync();
            });

            var allPlatformConfigs = await _cache.GetOrCreateAsync("AllPlatformConfigs", async entry => {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                return await _db.PlatformConfigTBLs.ToListAsync();
            });

            int successCount = 0;
            int totalCount = previousReports.Count;

            var sourceClient = _platformClients.FirstOrDefault(c => c.PlatformName == request.SourcePlatform);

            var successfulChannels = new List<string>();
            var failedChannels = new List<string>();

            foreach (var report in previousReports)
            {
                var adminChannel = allAdminChannels?.FirstOrDefault(ac => ac.ID == report.AdminChannelId.Value);
                if (adminChannel == null) continue;

                var config = allPlatformConfigs?.FirstOrDefault(c => c.ID == adminChannel.ChannelId);
                if (config == null || !config.IsActive) continue;

                var client = _platformClients.FirstOrDefault(c => c.PlatformName == config.PlatformName);
                if (client == null) continue;

                string platformFarsi = config.PlatformName switch
                {
                    "Telegram" => "تلگرام",
                    "Bale" => "بله",
                    "Eitaa" => "ایتا",
                    "Rubika" => "روبیکا",
                    _ => config.PlatformName ?? ""
                };

                string channelDisplayName = !string.IsNullOrWhiteSpace(config.Explain)
                    ? $"{config.Explain} ({config.TargetChannelId})"
                    : (!string.IsNullOrWhiteSpace(adminChannel.Explain) ? $"{adminChannel.Explain} ({config.TargetChannelId})" : config.TargetChannelId ?? "نامشخص");

                if (!string.IsNullOrEmpty(report.DestinationMessageId))
                {
                    try
                    {
                        var (editOk, errorMsg) = await client.EditMessageTextAsync(config.TargetChannelId, report.DestinationMessageId, newContent, "html", null, editedMessage);
                        if (editOk)
                        {
                            successCount++;
                            successfulChannels.Add($"🔹 <b>{platformFarsi}</b>: {channelDisplayName}");
                        }
                        else
                        {
                            string err = !string.IsNullOrWhiteSpace(errorMsg) ? errorMsg : "خطا در برقراری ارتباط یا ویرایش پیام";
                            failedChannels.Add($"🔸 <b>{platformFarsi}</b>: {channelDisplayName}\n   ⚠️ خطا: <code>{err}</code>");
                        }
                    }
                    catch (Exception ex)
                    {
                        MultiPlatformAdminBot.Common.Statics.Log(ex);
                        failedChannels.Add($"🔸 <b>{platformFarsi}</b>: {channelDisplayName}\n   ⚠️ خطا: <code>{ex.Message}</code>");
                    }
                }
                else
                {
                    failedChannels.Add($"🔸 <b>{platformFarsi}</b>: {channelDisplayName}\n   ⚠️ خطا: <code>شناسه پیام مقصد در دیتابیس ثبت نشده بود</code>");
                }
            }

            if (sourceClient != null)
            {
                var sb = new System.Text.StringBuilder();

                if (successfulChannels.Any())
                {
                    sb.AppendLine($"✏️ <b>ویرایش با موفقیت در {successfulChannels.Count} کانال انجام شد:</b>");
                    foreach (var sc in successfulChannels)
                    {
                        sb.AppendLine(sc);
                    }
                }

                if (failedChannels.Any())
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.AppendLine($"⚠️ <b>ویرایش در {failedChannels.Count} کانال با خطا مواجه شد:</b>");
                    foreach (var fc in failedChannels)
                    {
                        sb.AppendLine(fc);
                    }
                }

                if (!successfulChannels.Any() && !failedChannels.Any())
                {
                    sb.AppendLine("⚠️ پستی برای ویرایش در کانال‌ها یافت نشد.");
                }

                await sourceClient.SendTextMessageAsync(admin.ID.ToString(), sb.ToString().TrimEnd(), "html");
            }
        }
    }
}







