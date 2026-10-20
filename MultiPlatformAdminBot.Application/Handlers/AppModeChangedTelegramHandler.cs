using MediatR;
using MultiPlatformAdminBot.Application.Notifications;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace MultiPlatformAdminBot.Application.Handlers
{
    public class AppModeChangedTelegramHandler : INotificationHandler<AppModeChangedNotification>
    {
        private readonly IAppDbContext _db;
        private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

        public AppModeChangedTelegramHandler(IAppDbContext db, System.Net.Http.IHttpClientFactory httpClientFactory, Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        private string GetProxyUrl(string token, string action)
        {
            string baseUrl = _config["TelegramProxy:ProxyBaseUrl"] ?? "https://telbot.hekmateislami.com/proxy/index.php";
            string editUrl = baseUrl.Contains("index.php")
                ? baseUrl.Replace("index.php", "proxy_editbot.php")
                : (baseUrl.EndsWith("/") ? $"{baseUrl}proxy_editbot.php" : $"{baseUrl}/proxy_editbot.php");
            return $"{editUrl}?token={token}&action={action}";
        }

        private string GetProxySecret()
        {
            return _config["TelegramProxy:Secret"] ?? _config["TELEGRAM_PROXY_SECRET"] ?? "SecureSecret12345";
        }

        private async Task<JsonElement?> CallTelegramProxyAsync(HttpClient client, string token, string action, object payload, CancellationToken cancellationToken)
        {
            string proxyUrl = GetProxyUrl(token, action);
            var req = new HttpRequestMessage(HttpMethod.Post, proxyUrl);
            req.Headers.Add("X-Proxy-Secret", GetProxySecret());

            if (payload != null)
            {
                string json = JsonSerializer.Serialize(payload);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await client.SendAsync(req, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var responseStr = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(responseStr)) return null;

            try
            {
                using var doc = JsonDocument.Parse(responseStr);
                if (doc.RootElement.TryGetProperty("ok", out var okElem) && okElem.GetBoolean())
                {
                    if (doc.RootElement.TryGetProperty("result", out var resultElem))
                    {
                        return resultElem.Clone();
                    }
                    return doc.RootElement.Clone();
                }
            }
            catch { }

            return null;
        }

        public async Task Handle(AppModeChangedNotification notification, CancellationToken cancellationToken)
        {
            var telegramTokenEntity = await _db.BotTokenTBLs.FirstOrDefaultAsync(t => t.PlatformName == "Telegram", cancellationToken);
            if (telegramTokenEntity == null || string.IsNullOrEmpty(telegramTokenEntity.TokenValue)) return;

            string token = telegramTokenEntity.TokenValue;

            var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "AppMode", cancellationToken);
            int appMode = setting != null && int.TryParse(setting.Value, out int m) ? m : 2;

            // Use a standard HttpClient, NOT the internal Telegram client
            var httpClient = _httpClientFactory.CreateClient(); 

            try
            {
                // 1. GetMe
                var meResult = await CallTelegramProxyAsync(httpClient, token, "getMe", null, cancellationToken);
                string currentName = "Bot";
                if (meResult.HasValue && meResult.Value.TryGetProperty("first_name", out var fnElem))
                {
                    currentName = fnElem.GetString() ?? "Bot";
                }
                string cleanName = currentName.Replace("(دیباگ)", "").Replace("(پروداکشن)", "").Trim();

                // 2. GetMyShortDescription
                string currentShortDesc = "";
                var shortDescResult = await CallTelegramProxyAsync(httpClient, token, "getMyShortDescription", null, cancellationToken);
                if (shortDescResult.HasValue && shortDescResult.Value.TryGetProperty("short_description", out var sdElem))
                {
                    currentShortDesc = sdElem.GetString() ?? "";
                }

                // 3. GetMyDescription
                string currentDesc = "";
                var descResult = await CallTelegramProxyAsync(httpClient, token, "getMyDescription", null, cancellationToken);
                if (descResult.HasValue && descResult.Value.TryGetProperty("description", out var dElem))
                {
                    currentDesc = dElem.GetString() ?? "";
                }

                string cleanShortDesc = currentShortDesc.Replace("(دیباگ)", "").Replace("(پروداکشن)", "").Trim();
                string cleanDesc = currentDesc.Replace("(دیباگ)", "").Replace("(پروداکشن)", "").Trim();

                string newName = appMode == 1 ? $"(دیباگ) {cleanName}" : $"{cleanName} (پروداکشن)";
                string newShortDesc = appMode == 1 ? $"(دیباگ) {cleanShortDesc}" : $"{cleanShortDesc} (پروداکشن)";
                string newDesc = appMode == 1 ? $"(دیباگ) {cleanDesc}" : $"{cleanDesc} (پروداکشن)";

                // 4. Set updates
                await CallTelegramProxyAsync(httpClient, token, "setMyName", new { name = newName }, cancellationToken);

                if (!string.IsNullOrEmpty(cleanShortDesc))
                    await CallTelegramProxyAsync(httpClient, token, "setMyShortDescription", new { short_description = newShortDesc }, cancellationToken);

                if (!string.IsNullOrEmpty(cleanDesc))
                    await CallTelegramProxyAsync(httpClient, token, "setMyDescription", new { description = newDesc }, cancellationToken);


                // 5. Profile Photo Update via proxy_editbot.php
                string photoFileName = appMode == 1 ? "debug.jpg" : "main.jpg";
                string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;
                string photoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", photoFileName);
                if (!File.Exists(photoPath))
                {
                    photoPath = Path.Combine(baseDir, "wwwroot", photoFileName);
                }

                if (File.Exists(photoPath))
                {
                    using var content = new MultipartFormDataContent();
                    using var fileStream = File.OpenRead(photoPath);
                    using var streamContent = new StreamContent(fileStream);
                    content.Add(streamContent, "photo", photoFileName);
                    
                    string proxyUrl = GetProxyUrl(token, "setChatPhoto");
                    using var req = new HttpRequestMessage(HttpMethod.Post, proxyUrl);
                    req.Headers.Add("X-Proxy-Secret", GetProxySecret());
                    req.Content = content;

                    using var response = await httpClient.SendAsync(req, cancellationToken);

                    // خواندن محتوای ریسپانس
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                    Console.WriteLine("═══════════════════════════════════════");
                    Console.WriteLine($"Status Code : {(int)response.StatusCode} {response.StatusCode}");
                    Console.WriteLine($"Reason      : {response.ReasonPhrase}");
                    Console.WriteLine("───── Response Body ─────");
                    Console.WriteLine(responseBody);
                    Console.WriteLine("═══════════════════════════════════════");

                    // اگر خواستی مطمئن بشی درخواست موفق بوده:
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("⚠️ Request FAILED!");
                    }
                }
            }
            catch (System.Exception)
            {
                // Ignore API errors or rate limits
            }
        }
    }
}





