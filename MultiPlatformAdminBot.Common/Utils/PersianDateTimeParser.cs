using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MultiPlatformAdminBot.Common.Utils
{
    public static class PersianDateTimeParser
    {
        private static readonly TimeSpan IranOffset = TimeSpan.FromHours(3.5); // +03:30

        public static string NormalizeDigits(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return input
                .Replace('۰', '0').Replace('۱', '1').Replace('۲', '2').Replace('۳', '3').Replace('۴', '4')
                .Replace('۵', '5').Replace('۶', '6').Replace('۷', '7').Replace('۸', '8').Replace('۹', '9');
        }

        public static bool TryParseSchedule(string input, DateTime nowUtc, out DateTime targetUtc, out string error)
        {
            targetUtc = default;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "زمان وارد نشده است.";
                return false;
            }

            var clean = NormalizeDigits(input.Trim());

            // 1. Relative shortcuts: +1h, +2h, etc.
            if (clean.StartsWith("+") && clean.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                var numStr = clean.Substring(1, clean.Length - 2);
                if (int.TryParse(numStr, out int hours) && hours > 0)
                {
                    targetUtc = nowUtc.AddHours(hours);
                    return true;
                }
            }

            // Tehran current time
            var nowTehran = nowUtc + IranOffset;

            // 2. Simple Time: HH:mm or HH
            var timeMatch = Regex.Match(clean, @"^(?<h>\d{1,2})(:(?<m>\d{1,2}))?$");
            if (timeMatch.Success)
            {
                int h = int.Parse(timeMatch.Groups["h"].Value);
                int m = timeMatch.Groups["m"].Success ? int.Parse(timeMatch.Groups["m"].Value) : 0;

                if (h < 0 || h > 23 || m < 0 || m > 59)
                {
                    error = "ساعت وارد شده نامعتبر است (باید بین ۰۰:۰۰ تا ۲۳:۵۹ باشد).";
                    return false;
                }

                var targetTehran = new DateTime(nowTehran.Year, nowTehran.Month, nowTehran.Day, h, m, 0, DateTimeKind.Unspecified);
                
                // If the time today is already passed, roll over to tomorrow
                if (targetTehran <= nowTehran)
                {
                    targetTehran = targetTehran.AddDays(1);
                }

                targetUtc = DateTime.SpecifyKind(targetTehran - IranOffset, DateTimeKind.Utc);
                return true;
            }

            // 3. Persian Date + Time: yyyy/MM/dd HH:mm
            var fullMatch = Regex.Match(clean, @"^(?<y>\d{4})/(?<mo>\d{1,2})/(?<d>\d{1,2})\s+(?<h>\d{1,2}):(?<m>\d{1,2})$");
            if (fullMatch.Success)
            {
                int py = int.Parse(fullMatch.Groups["y"].Value);
                int pmo = int.Parse(fullMatch.Groups["mo"].Value);
                int pd = int.Parse(fullMatch.Groups["d"].Value);
                int ph = int.Parse(fullMatch.Groups["h"].Value);
                int pm = int.Parse(fullMatch.Groups["m"].Value);

                var pc = new PersianCalendar();
                try
                {
                    var targetGregorian = pc.ToDateTime(py, pmo, pd, ph, pm, 0, 0);
                    var calculatedUtc = DateTime.SpecifyKind(targetGregorian - IranOffset, DateTimeKind.Utc);
                    if (calculatedUtc <= nowUtc)
                    {
                        error = "زمان تعیین‌شده باید در آینده باشد.";
                        return false;
                    }

                    targetUtc = calculatedUtc;
                    return true;
                }
                catch (Exception)
                {
                    error = "تاریخ یا ساعت شمسی نامعتبر است.";
                    return false;
                }
            }

            error = "فرمت نامعتبر است. نمونه‌های مجاز: ۱۱:۰۰ یا +1h یا ۱۴۰۵/۰۷/۱۵ ۱۱:۳۰";
            return false;
        }

        public static string ToPersianDateTimeString(DateTime utc)
        {
            var tehran = utc + IranOffset;
            var pc = new PersianCalendar();
            return $"{pc.GetYear(tehran)}/{pc.GetMonth(tehran):D2}/{pc.GetDayOfMonth(tehran):D2} ساعت {tehran.Hour:D2}:{tehran.Minute:D2}";
        }
    }
}
