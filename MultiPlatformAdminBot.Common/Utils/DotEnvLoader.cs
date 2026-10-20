using System;
using System.Collections.Generic;
using System.IO;

namespace MultiPlatformAdminBot.Common.Utils
{
    /// <summary>
    /// Lightweight, zero-dependency .env loader for .NET.
    /// Reads key-value pairs from a .env file and sets them as environment variables,
    /// while also mapping common aliases to ASP.NET Core configuration paths.
    /// </summary>
    public static class DotEnvLoader
    {
        /// <summary>
        /// Attempts to find and load a .env file starting from the given path or directory,
        /// or searches up the directory tree from CurrentDirectory and AppContext.BaseDirectory.
        /// </summary>
        /// <param name="customPath">Optional explicit path to the .env file or directory containing it.</param>
        /// <returns>A dictionary of the variables that were loaded.</returns>
        public static Dictionary<string, string> Load(string? customPath = null)
        {
            var loaded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string? envFilePath = FindEnvFile(customPath);
            if (string.IsNullOrEmpty(envFilePath) || !File.Exists(envFilePath))
            {
                // No .env file found; environment variables may already be set in the host / container.
                return loaded;
            }

            try
            {
                var lines = File.ReadAllLines(envFilePath);
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();

                    // Skip empty lines and comment lines
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                        continue;

                    // Support bash-style 'export KEY=VAL'
                    if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
                    {
                        line = line.Substring(7).TrimStart();
                    }

                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex <= 0) continue;

                    string key = line.Substring(0, equalsIndex).Trim();
                    string value = line.Substring(equalsIndex + 1).Trim();

                    if (string.IsNullOrEmpty(key)) continue;

                    // Strip surrounding quotes if present
                    if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                        (value.StartsWith("'") && value.EndsWith("'")))
                    {
                        if (value.Length >= 2)
                        {
                            value = value.Substring(1, value.Length - 2);
                        }
                    }
                    else
                    {
                        // Strip trailing comment if not enclosed in quotes (e.g., KEY=VAL # comment)
                        int commentIndex = value.IndexOf(" #", StringComparison.Ordinal);
                        if (commentIndex >= 0)
                        {
                            value = value.Substring(0, commentIndex).Trim();
                        }
                    }

                    // Set standard environment variable
                    Environment.SetEnvironmentVariable(key, value);
                    loaded[key] = value;

                    // Automatically map friendly aliases to ASP.NET Core hierarchical configuration keys
                    MapAliases(key, value, loaded);
                }

                Console.WriteLine($"[DotEnvLoader] Successfully loaded {loaded.Count} environment variables from: {envFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DotEnvLoader] Warning: Failed to parse .env file ({envFilePath}): {ex.Message}");
            }

            return loaded;
        }

        private static void MapAliases(string key, string value, Dictionary<string, string> loaded)
        {
            void SetAlias(string aspNetKey)
            {
                Environment.SetEnvironmentVariable(aspNetKey, value);
                loaded[aspNetKey] = value;
            }

            switch (key.ToUpperInvariant())
            {
                case "DEFAULT_CONNECTION":
                case "CONNECTION_STRING":
                case "DB_CONNECTION":
                case "DATABASE_URL":
                    SetAlias("ConnectionStrings__DefaultConnection");
                    SetAlias("ConnectionStrings:DefaultConnection");
                    break;

                case "JWT_SECRET":
                case "JWT_SIGNING_KEY":
                case "JWT_KEY":
                    SetAlias("Jwt__SigningKey");
                    SetAlias("Jwt:SigningKey");
                    break;

                case "SWAGGER_USER":
                case "SECURITY_SWAGGER_USER":
                    SetAlias("Security__SwaggerUser");
                    SetAlias("Security:SwaggerUser");
                    break;

                case "SWAGGER_PASSWORD":
                case "SECURITY_SWAGGER_PASSWORD":
                    SetAlias("Security__SwaggerPassword");
                    SetAlias("Security:SwaggerPassword");
                    break;

                case "TELEGRAM_PROXY_URL":
                case "PROXY_BASE_URL":
                    SetAlias("TelegramProxy__ProxyBaseUrl");
                    SetAlias("TelegramProxy:ProxyBaseUrl");
                    break;

                case "TELEGRAM_PROXY_SECRET":
                case "PROXY_SECRET":
                    SetAlias("TelegramProxy__Secret");
                    SetAlias("TelegramProxy:Secret");
                    break;

                case "NOTIFY_ERROR_CHAT_ID":
                case "ERROR_CHAT_ID":
                    SetAlias("Notify__ErrorChatId");
                    SetAlias("Notify:ErrorChatId");
                    break;

                case "CORS_ALLOWED_ORIGINS":
                case "ALLOWED_ORIGINS":
                    SetAlias("Cors__AllowedOrigins");
                    SetAlias("Cors:AllowedOrigins");
                    break;

                case "SEEDER_SEED_DEFAULTS":
                    SetAlias("Seeder__SeedDefaults");
                    SetAlias("Seeder:SeedDefaults");
                    break;

                case "SEEDER_TELEGRAM_TOKEN":
                    SetAlias("Seeder__TelegramToken");
                    SetAlias("Seeder:TelegramToken");
                    break;

                case "SEEDER_BALE_TOKEN":
                    SetAlias("Seeder__BaleToken");
                    SetAlias("Seeder:BaleToken");
                    break;

                case "SEEDER_EITAA_TOKEN":
                    SetAlias("Seeder__EitaaToken");
                    SetAlias("Seeder:EitaaToken");
                    break;

                case "SEEDER_RUBIKA_TOKEN":
                    SetAlias("Seeder__RubikaToken");
                    SetAlias("Seeder:RubikaToken");
                    break;
            }
        }

        private static string? FindEnvFile(string? customPath)
        {
            if (!string.IsNullOrEmpty(customPath))
            {
                if (File.Exists(customPath)) return customPath;
                if (Directory.Exists(customPath))
                {
                    string candidate = Path.Combine(customPath, ".env");
                    if (File.Exists(candidate)) return candidate;
                }
            }

            var candidateDirs = new List<string>();

            try
            {
                candidateDirs.Add(Directory.GetCurrentDirectory());
            }
            catch { }

            try
            {
                candidateDirs.Add(AppContext.BaseDirectory);
            }
            catch { }

            foreach (var baseDir in candidateDirs)
            {
                string? current = baseDir;
                for (int i = 0; i < 6 && !string.IsNullOrEmpty(current); i++)
                {
                    string candidate = Path.Combine(current, ".env");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    var parent = Directory.GetParent(current);
                    current = parent?.FullName;
                }
            }

            return null;
        }
    }
}
