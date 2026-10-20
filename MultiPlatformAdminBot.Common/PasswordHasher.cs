using System.Security.Cryptography;

namespace MultiPlatformAdminBot.Common
{
    /// <summary>
    /// PBKDF2-SHA256 password hashing (no external packages needed).
    /// Stored format: PBKDF2$<iterations>$<salt base64>$<hash base64>
    /// </summary>
    public static class PasswordHasher
    {
        private const int Iterations = 100_000;
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const string Prefix = "PBKDF2";

        public static string Hash(string password)
        {
            ArgumentException.ThrowIfNullOrEmpty(password);
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) return false;
            if (!stored.StartsWith(Prefix + "$")) return false;

            var parts = stored.Split('$');
            if (parts.Length != 4) return false;
            if (!int.TryParse(parts[1], out var iterations) || iterations < 1) return false;

            byte[] salt, key;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                key = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var test = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, key.Length);
            return CryptographicOperations.FixedTimeEquals(key, test);
        }

        /// <summary>True when the stored value is a pre-migration plaintext password.</summary>
        public static bool IsLegacyPlaintext(string? stored) =>
            !string.IsNullOrEmpty(stored) && !stored.StartsWith(Prefix + "$");
    }
}
