using System;
using System.IO;
using MultiPlatformAdminBot.Common.Utils;
using Xunit;

namespace MultiPlatformAdminBot.Tests
{
    public class DotEnvLoaderTests : IDisposable
    {
        private readonly string _testEnvPath;

        public DotEnvLoaderTests()
        {
            _testEnvPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.env");
        }

        public void Dispose()
        {
            if (File.Exists(_testEnvPath))
            {
                try { File.Delete(_testEnvPath); } catch { }
            }
        }

        [Fact]
        public void Load_Should_Parse_Basic_Key_Values()
        {
            File.WriteAllLines(_testEnvPath, new[]
            {
                "# Comment line",
                "FOO=bar",
                "BAZ=\"quoted value\"",
                "SINGLE='single quoted'",
                "EXPORT_KEY=export val",
                "WITH_COMMENT=my_val # trailing comment"
            });

            var result = DotEnvLoader.Load(_testEnvPath);

            Assert.Equal("bar", result["FOO"]);
            Assert.Equal("quoted value", result["BAZ"]);
            Assert.Equal("single quoted", result["SINGLE"]);
            Assert.Equal("my_val", result["WITH_COMMENT"]);

            Assert.Equal("bar", Environment.GetEnvironmentVariable("FOO"));
            Assert.Equal("quoted value", Environment.GetEnvironmentVariable("BAZ"));
            Assert.Equal("single quoted", Environment.GetEnvironmentVariable("SINGLE"));
            Assert.Equal("my_val", Environment.GetEnvironmentVariable("WITH_COMMENT"));
        }

        [Fact]
        public void Load_Should_Map_DefaultConnection_Alias()
        {
            string expectedConn = "Data Source=localhost;Initial Catalog=AdminBotDB;User ID=adminAi;Password=secret;TrustServerCertificate=True";

            File.WriteAllLines(_testEnvPath, new[]
            {
                $"DEFAULT_CONNECTION=\"{expectedConn}\""
            });

            var result = DotEnvLoader.Load(_testEnvPath);

            Assert.Equal(expectedConn, result["DEFAULT_CONNECTION"]);
            Assert.Equal(expectedConn, Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));
            Assert.Equal(expectedConn, Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection"));
        }

        [Fact]
        public void Load_Should_Map_JwtSecret_And_Swagger_Aliases()
        {
            File.WriteAllLines(_testEnvPath, new[]
            {
                "JWT_SECRET=\"SuperSecretKey12345!\"",
                "SWAGGER_USER=\"admin\"",
                "SWAGGER_PASSWORD=\"strongpass\""
            });

            var result = DotEnvLoader.Load(_testEnvPath);

            Assert.Equal("SuperSecretKey12345!", Environment.GetEnvironmentVariable("Jwt__SigningKey"));
            Assert.Equal("admin", Environment.GetEnvironmentVariable("Security__SwaggerUser"));
            Assert.Equal("strongpass", Environment.GetEnvironmentVariable("Security__SwaggerPassword"));
        }
    }
}
