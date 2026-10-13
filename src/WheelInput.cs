using System;

namespace SleepTimer
{
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
