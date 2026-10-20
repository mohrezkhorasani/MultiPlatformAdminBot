using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace MultiPlatformAdminBot.CoreMe.Middleware
{
    /// <summary>
    /// Protects /swagger with HTTP Basic auth. Credentials come from configuration
    /// (Security:SwaggerUser / Security:SwaggerPassword), never from source code.
    /// </summary>
    public class SwaggerBasicAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _username;
        private readonly string _password;

        public SwaggerBasicAuthMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _username = configuration["Security:SwaggerUser"] ?? "admin";
            _password = configuration["Security:SwaggerPassword"] ?? "";
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                string authHeader = context.Request.Headers["Authorization"];
                if (authHeader != null && authHeader.StartsWith("Basic "))
                {
                    try
                    {
                        var authHeaderVal = AuthenticationHeaderValue.Parse(authHeader);
                        var credentialBytes = Convert.FromBase64String(authHeaderVal.Parameter ?? string.Empty);
                        var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);

                        bool ok = string.IsNullOrEmpty(_password)
                            ? false
                            : credentials.Length == 2
                              && credentials[0] == _username
                              && credentials[1] == _password;

                        if (ok)
                        {
                            await _next.Invoke(context);
                            return;
                        }
                    }
                    catch
                    {
                        // Ignore parsing errors and fall through to unauthorized
                    }
                }

                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Swagger Area\"";
                context.Response.StatusCode = 401;
                return;
            }

            await _next.Invoke(context);
        }
    }
}
