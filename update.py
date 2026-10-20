import re

with open('F:/DOTNET/Admin-Bot/MultiPlatformAdminBot.Application/UseCases/BroadcastMessageUseCase.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# For HandleAsync
code = re.sub(
    r'(var client = _platformClients.FirstOrDefault\(c => c.PlatformName == config.PlatformName\);\s+if \(client != null\)\s+\{)',
    r'''\1
                    var adminChannel = allAdminChannels.FirstOrDefault(ac => ac.AdminUserId == admin.ID && ac.ChannelId == config.ID);
                    Domain.Entity.PostReportTBL postReport = null;
                    if (adminChannel != null)
                    {
                        postReport = new Domain.Entity.PostReportTBL
                        {
                            AdminChannelId = adminChannel.ID,
                            IsSuccessful = true,
                            DateInsert = System.DateTime.Now,
                            Status = 1,
                            Type = 1,
                            Explain = ""
                        };
                        
                        if (msgType != MessageType.Text && !string.IsNullOrEmpty(fileId))
                        {
                            postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "fileId", MetaValue = fileId, DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                        }
                    }''',
    code, count=1
)

code = re.sub(
    r'(catch \(Exception ex\)\s+\{)',
    r'''\1
                        if (postReport != null)
                        {
                            postReport.IsSuccessful = false;
                            postReport.ErrorLog = ex.Message;
                        }''',
    code, count=1
)

code = re.sub(
    r'(errorLog \+= \$"\[\{config.PlatformName\} Error\]: \{err\}\\n";\s+\})',
    r'''\1
                        if (postReport != null && !success)
                        {
                            postReport.IsSuccessful = false;
                            postReport.ErrorLog = "Media download failed.";
                        }''',
    code, count=1
)

code = re.sub(
    r'(if \(client != null\)\s+\{[\s\S]*?catch \(Exception ex\)[\s\S]*?\}\s+\})',
    r'''\g<1>
                if (postReport != null)
                {
                    _db.PostReportTBLs.Add(postReport);
                }''',
    code, count=1
)


# For HandleAlbumAsync
code = re.sub(
    r'(var client = _platformClients.FirstOrDefault\(c => c.PlatformName == config.PlatformName\);\s+if \(client != null\)\s+\{\s+try\s+\{)',
    r'''\1
                    var adminChannel = allAdminChannels.FirstOrDefault(ac => ac.AdminUserId == admin.ID && ac.ChannelId == config.ID);
                    Domain.Entity.PostReportTBL postReport = null;
                    if (adminChannel != null)
                    {
                        postReport = new Domain.Entity.PostReportTBL
                        {
                            AdminChannelId = adminChannel.ID,
                            IsSuccessful = true,
                            DateInsert = System.DateTime.Now,
                            Status = 1,
                            Type = 1,
                            Explain = ""
                        };
                        
                        // Add each fileId as metadata
                        foreach (var req in reqList)
                        {
                            var m = req.Update.Message;
                            string fid = null;
                            if (m?.Type == MessageType.Photo) fid = m.Photo?.LastOrDefault()?.FileId;
                            else if (m?.Type == MessageType.Video) fid = m.Video?.FileId;
                            if (fid != null)
                            {
                                postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "album_fileId", MetaValue = fid, DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                            }
                        }
                    }''',
    code, count=1
)

code = re.sub(
    r'(catch \(Exception ex\)\s+\{\s+success = false;\s+errorLog \+= \$"\[\{config.PlatformName\} Error\]: \{ex.Message\}\\n";)',
    r'''\1
                        if (postReport != null)
                        {
                            postReport.IsSuccessful = false;
                            postReport.ErrorLog = ex.Message;
                        }''',
    code, count=1
)

code = re.sub(
    r'(if \(client != null\)\s+\{\s+try[\s\S]*?catch \(Exception ex\)[\s\S]*?\}\s+\})',
    r'''\g<1>
                if (postReport != null)
                {
                    _db.PostReportTBLs.Add(postReport);
                }''',
    code, count=1
)

with open('F:/DOTNET/Admin-Bot/MultiPlatformAdminBot.Application/UseCases/BroadcastMessageUseCase.cs', 'w', encoding='utf-8') as f:
    f.write(code)

