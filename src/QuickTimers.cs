using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
            bool hours = value.EndsWith("h", StringComparison.Ordinal);
            if (hours) value = value.Substring(0, value.Length - 1).TrimEnd();
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int amount)
                || amount < 1 || amount > (hours ? 24 : 1440)) return false;
            minutes = hours ? amount * 60 : amount;
            return true;
        }

        public bool Add(int minutes) => minutes >= 1 && minutes <= 1440 && !Defaults.Contains(minutes) && custom.Add(minutes);
        public bool Remove(int minutes) => custom.Remove(minutes);
        public string Save() => string.Join(",", custom.Select(m => m.ToString(CultureInfo.InvariantCulture)));
        public static string Label(int minutes) => minutes < 60 ? $"{minutes} minutes"
            : minutes % 60 == 0 ? $"{minutes / 60} hour{(minutes == 60 ? "" : "s")}" : $"{minutes / 60} h {minutes % 60} min";
    }
}
