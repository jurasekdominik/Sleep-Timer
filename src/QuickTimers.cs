using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SleepTimer
{
    sealed class QuickTimers
    {
        static readonly int[] Defaults = { 30, 60, 120, 240 };
        readonly SortedSet<int> custom = new SortedSet<int>();
        public IEnumerable<int> Custom => custom;
        public IEnumerable<int> All => Defaults.Concat(custom).Distinct().OrderBy(m => m);

        public QuickTimers(string saved)
        {
            foreach (string value in (saved ?? "").Split(','))
                if (TryMinutes(value, out int minutes)) Add(minutes);
        }

        public static bool TryMinutes(string text, out int minutes)
        {
            minutes = 0;
            string value = (text ?? "").Trim().ToLowerInvariant();
            decimal total;
            var clock = Regex.Match(value, @"^(\d+)\s*:\s*(\d{1,2})$");
            if (clock.Success)
            {
                if (!decimal.TryParse(clock.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out decimal hours)
                    || hours > 24 || !int.TryParse(clock.Groups[2].Value, out int part) || part >= 60) return false;
                total = hours * 60 + part;
            }
            else
            {
                var match = Regex.Match(value, @"^(?:(?<h>\d+(?:[.,]\d+)?)\s*(?:hours?|hrs?|h)\s*)?(?:(?<m>\d+(?:[.,]\d+)?)\s*(?:minutes?|mins?|m)?)?$");
                if (!match.Success || (!match.Groups["h"].Success && !match.Groups["m"].Success)) return false;
                decimal hours = 0, remainder = 0;
                if (match.Groups["h"].Success
                    && (!decimal.TryParse(match.Groups["h"].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out hours) || hours > 24)) return false;
                if (match.Groups["m"].Success
                    && (!decimal.TryParse(match.Groups["m"].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out remainder) || remainder > 1440)) return false;
                total = hours * 60 + remainder;
            }
            if (total < 1 || total > 1440) return false;
            minutes = (int)Math.Round(total, 0, MidpointRounding.AwayFromZero);
            return true;
        }

        public bool Add(int minutes) => minutes >= 1 && minutes <= 1440 && !Defaults.Contains(minutes) && custom.Add(minutes);
        public bool Remove(int minutes) => custom.Remove(minutes);
        public string Save() => string.Join(",", custom.Select(m => m.ToString(CultureInfo.InvariantCulture)));
        public static string Label(int minutes) => minutes < 60 ? $"{minutes} minutes"
            : minutes % 60 == 0 ? $"{minutes / 60} hour{(minutes == 60 ? "" : "s")}" : $"{minutes / 60} h {minutes % 60} min";
    }
}
