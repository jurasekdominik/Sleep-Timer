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

        public static bool TryMinutes(string text, out int minutes) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out minutes) && minutes >= 1 && minutes <= 1440;

        public bool Add(int minutes) => minutes >= 1 && minutes <= 1440 && !Defaults.Contains(minutes) && custom.Add(minutes);
        public bool Remove(int minutes) => custom.Remove(minutes);
        public string Save() => string.Join(",", custom.Select(m => m.ToString(CultureInfo.InvariantCulture)));
        public static string Label(int minutes) => minutes < 60 ? $"{minutes} minutes"
            : minutes % 60 == 0 ? $"{minutes / 60} hour{(minutes == 60 ? "" : "s")}" : $"{minutes / 60} h {minutes % 60} min";
    }
}
