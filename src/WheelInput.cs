using System;

namespace SleepTimer
{
    /// <summary>Snaps wheel edits to whole hours and briefly ignores continued input.</summary>
    sealed class HourStop
    {
        public int HoldMilliseconds { get; set; } = 400;
        int direction;
        double boundary = double.NaN, until;
        public void Reset() { direction = 0; boundary = double.NaN; until = 0; }

        public double Adjust(double current, double delta, double milliseconds)
        {
            if (delta == 0) return 0;
            int nextDirection = Math.Sign(delta);
            if (nextDirection != direction) Reset();
            direction = nextDirection;
            if (milliseconds < until) return 0;
            double next = direction > 0 ? (Math.Floor(current / 3600) + 1) * 3600
                : (Math.Ceiling(current / 3600) - 1) * 3600;
            // A running countdown can tick just below the boundary during the hold.
            if (next == boundary) next += direction * 3600;
            if (next > 0 && next <= Countdown.MaxSet
                && (direction > 0 ? current + delta >= next : current + delta <= next))
            {
                boundary = next;
                until = milliseconds + Math.Max(0, HoldMilliseconds);
                return next - current;
            }
            return delta;
        }
    }

    /// <summary>Preserves partial wheel deltas, resets at direction/modifier changes, and bounds bursts.</summary>
    sealed class WheelInput
    {
        long remainder;
        int lastUnit;
        public int Add(int delta, int secondsPerNotch)
        {
            if (secondsPerNotch != lastUnit || (remainder != 0 && Math.Sign(remainder) != Math.Sign(delta))) remainder = 0;
            lastUnit = secondsPerNotch;
            remainder += delta;
            long steps = remainder / 120;
            remainder %= 120;
            return (int)Math.Max(-Countdown.MaxSet, Math.Min(Countdown.MaxSet, steps * secondsPerNotch));
        }
    }
}
