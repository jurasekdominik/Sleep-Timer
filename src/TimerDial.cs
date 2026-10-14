using System;

namespace SleepTimer
{
    enum TimerTone { Idle, Running, Paused, Warning, Urgent }

    static class TimerStatus
    {
        public static TimerTone Tone(TimerState state, double remaining) => state == TimerState.Idle ? TimerTone.Idle
            : state == TimerState.Paused ? TimerTone.Paused : remaining <= 10 ? TimerTone.Urgent
            : remaining <= 60 ? TimerTone.Warning : TimerTone.Running;

        public static int Complete(double remainingFraction) => (int)Math.Floor(100 * (1 - Math.Max(0, Math.Min(1, remainingFraction))));
    }

    /// <summary>Each revolution represents an hour; inner blocks carry all remaining hours.</summary>
    sealed class TimerDial
    {
        public double Seconds => 3600;
        public int Ticks => 60;
        public int MajorEvery => 5;
        public int ExtraHours { get; }
        public double HourFill { get; }

        TimerDial(double remaining)
        {
            ExtraHours = Math.Max(0, (int)Math.Ceiling(WholeSeconds(remaining) / Seconds) - 1);
            HourFill = ExtraHours + Fraction(remaining);
        }

        // Match the numeric countdown's rounding so marks and digits cross hour boundaries together.
        static double WholeSeconds(double remaining) => Math.Ceiling(Math.Max(0, Math.Min(Countdown.MaxSet, remaining)));
        public static TimerDial For(double remaining) => new TimerDial(remaining);

        public double Fraction(double remaining)
        {
            double seconds = Math.Max(0, Math.Min(Countdown.MaxSet, remaining));
            if (seconds == 0) return 0;
            double part = seconds % Seconds;
            return part == 0 ? 1 : part / Seconds;
        }
    }
}
