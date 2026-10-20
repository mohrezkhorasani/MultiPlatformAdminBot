namespace MultiPlatformAdminBot.CoreMe
{
    /// <summary>
    /// JWT signing configuration. The key must be a strong, random value
    /// supplied via user-secrets / environment variables (Jwt:SigningKey),
    /// never hard-coded in source.
    /// </summary>
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string SigningKey { get; set; } = string.Empty;
    }
}
