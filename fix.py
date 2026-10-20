import re

with open('F:/DOTNET/Admin-Bot/MultiPlatformAdminBot.Application/UseCases/BroadcastMessageUseCase.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# Remove the existing declarations
code = code.replace("Domain.Entity.PostReportTBL postReport = null;", "")

# Add it back outside the if(client != null) blocks
code = re.sub(
    r'(var client = _platformClients.FirstOrDefault\(c => c.PlatformName == config.PlatformName\);)',
    r'''Domain.Entity.PostReportTBL postReport = null;
                \1''',
    code
)

with open('F:/DOTNET/Admin-Bot/MultiPlatformAdminBot.Application/UseCases/BroadcastMessageUseCase.cs', 'w', encoding='utf-8') as f:
    f.write(code)

