namespace MultiPlatformAdminBot.Infrastructure;

/// <summary>
/// Options for the outbound Telegram proxy. The secret and URL must come from
/// configuration (user-secrets / environment variables), never from source code.
/// </summary>
public class TelegramProxyOptions
{
    public const string SectionName = "TelegramProxy";

    /// <summary>Base URL of the PHP proxy, e.g. https://your-proxy-host/proxy/index.php</summary>
    public string? ProxyBaseUrl { get; set; }

    /// <summary>Shared secret sent in the X-Proxy-Secret header. Must match the server-side value.</summary>
    public string? Secret { get; set; }
}
