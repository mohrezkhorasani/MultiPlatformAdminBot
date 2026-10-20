import re

with open('F:/DOTNET/Admin-Bot/MultiPlatformAdminBot.Application/UseCases/BroadcastMessageUseCase.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# For HandleAsync
code = re.sub(
    r'(Explain = ""\s*};)',
    r'''Explain = "",
                            SourceMessageId = request.Update.Message?.MessageId
                        };
                        
                        string txt = request.Update.Message?.Text ?? request.Update.Message?.Caption ?? "";
                        string snd = request.Update.Message?.From?.Id.ToString() ?? admin.ID.ToString();
                        
                        postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "content", MetaValue = txt, DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                        postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "senderId", MetaValue = snd, DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                        postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "targetChatId", MetaValue = config.TargetChannelId ?? "", DateInsert = System.DateTime.Now, Status = 1, Type = 1 });''',
    code, count=1
)

# For HandleAlbumAsync
code = re.sub(
    r'(Explain = ""\s*};)([\s\S]*?// Add each fileId as metadata)',
    r'''Explain = "",
                            SourceMessageId = firstReq.Update.Message?.MessageId
                        };
                        
                        string snd = firstReq.Update.Message?.From?.Id.ToString() ?? admin.ID.ToString();
                        
                        postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "content", MetaValue = caption ?? "", DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                        postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "senderId", MetaValue = snd, DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                        postReport.MetaDatas.Add(new Domain.Entity.PostReportMetaDataTBL { MetaKey = "targetChatId", MetaValue = config.TargetChannelId ?? "", DateInsert = System.DateTime.Now, Status = 1, Type = 1 });
                        \2''',
    code, count=1
)

with open('F:/DOTNET/Admin-Bot/MultiPlatformAdminBot.Application/UseCases/BroadcastMessageUseCase.cs', 'w', encoding='utf-8') as f:
    f.write(code)
