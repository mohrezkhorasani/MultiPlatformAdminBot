using MultiPlatformAdminBot.Common;
using Xunit;

namespace MultiPlatformAdminBot.Tests
{
    public class PasswordHasherTests
    {
        [Fact]
        public void Hash_Then_Verify_Succeeds()
        {
            var hash = PasswordHasher.Hash("S3cret!");
            Assert.True(PasswordHasher.Verify("S3cret!", hash));
        }

        [Fact]
        public void Verify_WrongPassword_Fails()
        {
            var hash = PasswordHasher.Hash("S3cret!");
            Assert.False(PasswordHasher.Verify("wrong", hash));
        }

        [Fact]
        public void Hash_IsSalted_TwoHashesDiffer()
        {
            var h1 = PasswordHasher.Hash("same");
            var h2 = PasswordHasher.Hash("same");
            Assert.NotEqual(h1, h2);
        }

        [Fact]
        public void IsLegacyPlaintext_DetectsUnhashedValues()
        {
            Assert.True(PasswordHasher.IsLegacyPlaintext("old-plain-password"));
            Assert.False(PasswordHasher.IsLegacyPlaintext(PasswordHasher.Hash("x")));
            Assert.False(PasswordHasher.IsLegacyPlaintext(null));
            Assert.False(PasswordHasher.IsLegacyPlaintext(""));
        }

        [Fact]
        public void Verify_RejectsGarbageStoredValue()
        {
            Assert.False(PasswordHasher.Verify("x", "not-a-hash"));
            Assert.False(PasswordHasher.Verify("x", "PBKDF2$abc$def"));
            Assert.False(PasswordHasher.Verify("x", ""));
        }
    }
}
