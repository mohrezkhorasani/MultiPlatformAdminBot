using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace MultiPlatformAdminBot.Infrastructure.Services
{
    /// <summary>
    /// Rewrites outbound api.telegram.org requests to go through the PHP proxy
    /// (required because the hosting network blocks direct access to Telegram).
    /// The proxy URL and shared secret are injected from configuration
    /// (TelegramProxy:ProxyBaseUrl / TelegramProxy:Secret), never hard-coded.
    /// </summary>
    public class TelegramProxyHandler : DelegatingHandler
    {
        private readonly TelegramProxyOptions _options;

        public TelegramProxyHandler(IOptions<TelegramProxyOptions> options)
        {
            _options = options.Value;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.ProxyBaseUrl) || string.IsNullOrWhiteSpace(_options.Secret))
            {
                // Proxy not configured - pass the request through unchanged.
                return await base.SendAsync(request, cancellationToken);    
            }

            // Inject the shared secret header so the PHP script allows the request
            request.Headers.Add("X-Proxy-Secret", _options.Secret);

            // Bypass PHP's multipart/form-data parser by changing the media type
            if (request.Content != null && request.Content.Headers.ContentType != null && request.Content.Headers.ContentType.MediaType == "multipart/form-data")
            {
                request.Content.Headers.ContentType.MediaType = "application/x-proxy-multipart";
            }

            if (request.RequestUri != null && request.RequestUri.Host.Contains("api.telegram.org"))
            {
                // Use the ?req= parameter to specify the path for the PHP proxy[]

                string path = System.Uri.EscapeDataString(request.RequestUri.AbsolutePath);

                string newQuery = $"?req={path}";
                if (!string.IsNullOrEmpty(request.RequestUri.Query))
                {
                    // request.RequestUri.Query already starts with '?'
                    newQuery += "&" + request.RequestUri.Query.Substring(1);
                }

                string baseUri = _options.ProxyBaseUrl.TrimEnd('/');
                string newUri = $"{baseUri}{newQuery}";
                request.RequestUri = new System.Uri(newUri);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
