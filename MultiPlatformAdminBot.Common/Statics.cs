using System;

namespace MultiPlatformAdminBot.Common
{
    public static class Statics
    {
        public static Action<string> LogAction { get; set; }

        public static void Log(params string[] args)
        {
            if (args == null || args.Length == 0) return;
            var message = string.Join("\n", args);
            LogAction?.Invoke(message);
        }

        public static void Log(Exception ex)
        {
            if (ex == null) return;
            var message = $"🔴 Exception: {ex.Message}\n\nStackTrace: {ex.StackTrace}";
            LogAction?.Invoke(message);
        }
    }
}
