using System.Text.RegularExpressions;
using Xunit;

namespace MultiPlatformAdminBot.Tests
{
    /// <summary>
    /// Enforces the clean-architecture layering by inspecting project references:
    ///   Common ← Domain ← Application ← { Infrastructure, Persistence, Platforms.* } ← CoreMe
    /// Inner layers must never reference outer layers, and platform adapters must
    /// not depend on each other or on persistence.
    /// </summary>
    public class ArchitectureTests
    {
        private static readonly string RepoRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        private static string GetProjectRefs(string projectName)
        {
            var csproj = Path.Combine(RepoRoot, projectName, projectName + ".csproj");
            Assert.True(File.Exists(csproj), $"Missing csproj: {csproj}");
            var text = File.ReadAllText(csproj);
            var matches = Regex.Matches(text, @"<ProjectReference\s+Include=""\.\.\\(?<name>[^\\]+)\\");
            return string.Join(", ", matches.Cast<Match>().Select(m => m.Groups["name"].Value));
        }

        [Theory]
        [InlineData("MultiPlatformAdminBot.Common",
            "MultiPlatformAdminBot.Domain", "MultiPlatformAdminBot.Application",
            "MultiPlatformAdminBot.Infrastructure", "MultiPlatformAdminBot.Persistence", "MultiPlatformAdminBot.CoreMe")]
        [InlineData("MultiPlatformAdminBot.Domain",
            "MultiPlatformAdminBot.Application", "MultiPlatformAdminBot.Infrastructure",
            "MultiPlatformAdminBot.Persistence", "MultiPlatformAdminBot.CoreMe", "Platforms.Telegram")]
        [InlineData("MultiPlatformAdminBot.Application",
            "MultiPlatformAdminBot.Infrastructure", "MultiPlatformAdminBot.Persistence",
            "MultiPlatformAdminBot.CoreMe", "Platforms.Telegram", "Platforms.Rubika")]
        [InlineData("MultiPlatformAdminBot.Infrastructure",
            "MultiPlatformAdminBot.Persistence", "MultiPlatformAdminBot.CoreMe")]
        [InlineData("MultiPlatformAdminBot.Persistence",
            "MultiPlatformAdminBot.Infrastructure", "MultiPlatformAdminBot.CoreMe")]
        public void InnerLayers_DoNot_Reference_OuterLayers(string project, params string[] forbidden)
        {
            var refs = GetProjectRefs(project).ToUpperInvariant();
            foreach (var f in forbidden)
            {
                Assert.DoesNotContain(f.ToUpperInvariant(), refs);
            }
        }

        [Fact]
        public void PlatformAdapters_DoNot_Reference_EachOther()
        {
            var platforms = new[] { "Platforms.Telegram", "Platforms.Bale", "Platforms.Eitaa", "Platforms.Rubika" };
            foreach (var p in platforms)
            {
                if (!Directory.Exists(Path.Combine(RepoRoot, p))) continue; // project may be absent in this layout
                var refs = GetProjectRefs(p).ToUpperInvariant();
                foreach (var other in platforms.Where(o => o != p))
                {
                    Assert.DoesNotContain(other.ToUpperInvariant(), refs);
                }
            }
        }

        [Fact]
        public void NoSourceFile_Contains_Hardcoded_BotToken()
        {
            // Bot tokens look like 1234567890:AA... — never in source, always in config.
            var tokenPattern = new Regex(@"\b\d{8,10}:AA[A-Za-z0-9_\-]{33,}\b");
            var offenders = new List<string>();
            foreach (var file in Directory.EnumerateFiles(RepoRoot, "*.cs", SearchOption.AllDirectories)
                         .Concat(Directory.EnumerateFiles(RepoRoot, "*.json", SearchOption.AllDirectories))
                         .Where(f => !f.Contains(Path.Combine("bin", "")) && !f.Contains(Path.Combine("obj", ""))))
            {
                if (tokenPattern.IsMatch(File.ReadAllText(file))) offenders.Add(file);
            }
            Assert.Empty(offenders);
        }
    }
}
