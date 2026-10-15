using System.Globalization;

namespace SleepTimer
{
    static class AnimationTiming
    {
        public const int DefaultMilliseconds = 600, MaxMilliseconds = 10000;
        public static int Normalize(int milliseconds) => milliseconds >= 0 && milliseconds <= MaxMilliseconds
            ? milliseconds : DefaultMilliseconds;
        public static bool TryParse(string value, out int milliseconds) =>
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out milliseconds)
            && milliseconds >= 0 && milliseconds <= MaxMilliseconds;
    }
}
