using System;

namespace SleepTimer
{
    static class SwitchGeometry
    {
        public static double ThumbWidth(double width, double height, double scale, double press)
        {
            double inset = 2 * scale, thumbHeight = height - 2 * inset;
            double resting = Math.Abs(width / scale - 44) < 0.5 && Math.Abs(height / scale - 24) < 0.5
                ? 28 * scale : Math.Round(width * 0.625);
            double available = Math.Max(0, width - 2 * inset);
            return Math.Min(available, resting + Math.Max(0, Math.Min(1, press)) * thumbHeight * 0.28);
        }
    }
}
