using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SleepTimer
{
    sealed class TimerFont
    {
        public readonly string Key, Name;
        public readonly Typeface Typeface;
        public TimerFont(string key, string name, string family, FontWeight weight)
        {
            Key = key; Name = name;
            Typeface = new Typeface(new FontFamily(family), FontStyles.Normal, weight, FontStretches.Normal);
        }
    }

    static class TimerFonts
    {
        public static readonly TimerFont[] All = {
            new TimerFont("segoe", "Segoe UI · Regular", "Segoe UI", FontWeights.Normal),
            new TimerFont("light", "Segoe UI · Light", "Segoe UI", FontWeights.Light),
            new TimerFont("semilight", "Segoe UI · Semilight", "Segoe UI", FontWeight.FromOpenTypeWeight(350)),
            new TimerFont("semibold", "Segoe UI · Semibold", "Segoe UI", FontWeights.SemiBold),
            new TimerFont("bahnschrift", "Bahnschrift · Technical", "Bahnschrift, Segoe UI", FontWeights.Normal),
            new TimerFont("consolas", "Consolas · Monospaced", "Consolas, Segoe UI", FontWeights.Normal)
        };
        public static TimerFont Current = All[0];
        public static TimerFont Find(string key) => All.FirstOrDefault(font => font.Key == key) ?? All[0];
    }
}
