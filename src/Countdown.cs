using System;

namespace SleepTimer
{
    enum TimerState { Idle, Running, Paused }

    /// <summary>Timer state machine. Time comes from an injected monotonic clock (seconds).</summary>
    sealed class Countdown
    {
        public const int MinSet = 60, MaxSet = 24 * 3600, MinActive = 10, DefaultSeconds = 30 * 60;

        readonly Func<double> now;
        double end, remaining;

        public Countdown(int duration, Func<double> now)
        {
            this.now = now;
            Duration = Clamp(duration, MinSet, MaxSet);
            Total = remaining = Duration;
        }

        public TimerState State { get; private set; }
        public int Duration { get; private set; }
        public double Total { get; private set; }
        public double Remaining => State == TimerState.Running ? end - now() : remaining;
        public double Fraction => Math.Max(0, Math.Min(1, Remaining / Total));
        public bool Expired => State == TimerState.Running && Remaining <= 0;

        public void SetDuration(int seconds)
        {
            if (State != TimerState.Idle) return;
            Duration = Clamp(seconds, MinSet, MaxSet);
            Total = remaining = Duration;
        }

        public void Start(double seconds)
        {
            Total = remaining = seconds;
            Resume();
        }

        public void Pause()
        {
            remaining = end - now();
            State = TimerState.Paused;
        }

        public void Resume()
        {
            end = now() + remaining;
            State = TimerState.Running;
        }

        public void Reset()
        {
            State = TimerState.Idle;
            Total = remaining = Duration;
        }

        /// <summary>
        /// Adds time to a running or paused timer. Subtracting never goes below <see cref="MinActive"/>,
        /// so an over-eager scroll cannot trigger the shutdown immediately.
        /// </summary>
        public void Adjust(double delta)
        {
            remaining = Math.Min(MaxSet, Math.Max(MinActive, Remaining + delta));
            Total = Math.Max(Total, remaining);
            if (State == TimerState.Running) end = now() + remaining;
        }

        static int Clamp(int v, int lo, int hi) => Math.Min(hi, Math.Max(lo, v));
    }

    static class TimeText
    {
        /// <summary>m:ss below an hour, h:mm:ss above; partial seconds round up.</summary>
        public static string Clock(double seconds)
        {
            int s = (int)Math.Ceiling(Math.Max(0, seconds));
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60:00}:{s % 60:00}";
        }

        public static string Human(double seconds)
        {
            int m = Math.Max(1, (int)Math.Ceiling(seconds / 60));
            return m >= 60 ? $"{m / 60}h {m % 60:00}m" : $"{m}m";
        }

        public static string HourMinute(int minuteOfDay) => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";

        /// <summary>Next future local target. Gaps advance to the first valid minute;
        /// repeated times use the first occurrence that is still in the future.</summary>
        public static DateTimeOffset NextOccurrence(DateTimeOffset now, int minuteOfDay, TimeZoneInfo zone)
        {
            var day = TimeZoneInfo.ConvertTime(now, zone).Date;
            for (;; day = day.AddDays(1))
            {
                var local = DateTime.SpecifyKind(day.AddMinutes(minuteOfDay), DateTimeKind.Unspecified);
                while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
                var offsets = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local)
                    : new[] { zone.GetUtcOffset(local) };
                DateTimeOffset? next = null;
                foreach (var offset in offsets)
                {
                    var candidate = new DateTimeOffset(local, offset);
                    if (candidate > now && (!next.HasValue || candidate < next.Value)) next = candidate;
                }
                if (next.HasValue) return next.Value;
            }
        }

        /// <summary>Default target for "At time": now + duration, rounded up to the next 5 minutes.</summary>
        public static int DefaultClock(DateTimeOffset now, int durationSeconds, TimeZoneInfo zone)
        {
            var t = TimeZoneInfo.ConvertTime(now.AddSeconds(durationSeconds), zone);
            long step = TimeSpan.TicksPerMinute * 5;
            return (int)(((t.TimeOfDay.Ticks + step - 1) / step * 5) % 1440);
        }
    }
}
