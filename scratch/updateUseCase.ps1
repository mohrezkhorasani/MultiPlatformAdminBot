$content = Get-Content -Path "F:\DOTNET\Admin-Bot\MultiPlatformAdminBot.Application\UseCases\BroadcastMessageUseCase.cs" -Raw

$insert1 = @"
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
"@
$content = $content -replace 'foreach\(var config in activeConfigs\)', $insert1

$insert2 = @"
                    string? replyToId = null;
                    if (adminChannel != null && channelReplyToIds.ContainsKey(adminChannel.ID))
                    {
                        replyToId = channelReplyToIds[adminChannel.ID];
                    }

                    // Per-channel result:
"@
$content = $content -replace '// Per-channel result:', $insert2

$content = $content -replace 'client.SendTextMessageAsync\(config.TargetChannelId, content, "html", message\);', 'client.SendTextMessageAsync(config.TargetChannelId, content, "html", message, null, replyToId);'
$content = $content -replace 'client.SendPhotoAsync\(config.TargetChannelId, mediaStream, fileName, content, "html", message\)', 'client.SendPhotoAsync(config.TargetChannelId, mediaStream, fileName, content, "html", message, replyToId)'
$content = $content -replace 'client.SendVideoAsync\(config.TargetChannelId, mediaStream, fileName, content, "html", message\)', 'client.SendVideoAsync(config.TargetChannelId, mediaStream, fileName, content, "html", message, replyToId)'
$content = $content -replace 'client.SendDocumentAsync\(config.TargetChannelId, mediaStream, fileName, content, "html", message\)', 'client.SendDocumentAsync(config.TargetChannelId, mediaStream, fileName, content, "html", message, replyToId)'


$content = $content -replace 'var \(resOk, _\) = await client.SendTextMessageAsync\(config.TargetChannelId, content, "html", message, null, replyToId\);\s*configSucceeded = resOk;', 'var resText = await client.SendTextMessageAsync(config.TargetChannelId, content, "html", message, null, replyToId); configSucceeded = resText.Success; if(configSucceeded && postReport != null) postReport.DestinationMessageId = resText.MessageId;'
$content = $content -replace 'configSucceeded = \(await client.SendPhotoAsync\(config.TargetChannelId, mediaStream, fileName, content, "html", message, replyToId\)\).Success;', 'var pRes = await client.SendPhotoAsync(config.TargetChannelId, mediaStream, fileName, content, "html", message, replyToId); configSucceeded = pRes.Success; if(configSucceeded && postReport != null) postReport.DestinationMessageId = pRes.MessageId;'
$content = $content -replace 'configSucceeded = \(await client.SendVideoAsync\(config.TargetChannelId, mediaStream, fileName, content, "html", message, replyToId\)\).Success;', 'var vRes = await client.SendVideoAsync(config.TargetChannelId, mediaStream, fileName, content, "html", message, replyToId); configSucceeded = vRes.Success; if(configSucceeded && postReport != null) postReport.DestinationMessageId = vRes.MessageId;'
$content = $content -replace 'configSucceeded = \(await client.SendDocumentAsync\(config.TargetChannelId, mediaStream, fileName, content, "html", message, replyToId\)\).Success;', 'var dRes = await client.SendDocumentAsync(config.TargetChannelId, mediaStream, fileName, content, "html", message, replyToId); configSucceeded = dRes.Success; if(configSucceeded && postReport != null) postReport.DestinationMessageId = dRes.MessageId;'

$content | Set-Content -Path "F:\DOTNET\Admin-Bot\MultiPlatformAdminBot.Application\UseCases\BroadcastMessageUseCase.cs"
