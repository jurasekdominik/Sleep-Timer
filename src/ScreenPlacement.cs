using System;

namespace SleepTimer
{
    static class ScreenPlacement
    {
        // All coordinates and sizes are physical screen pixels, including negative monitors.
        public static (int X, int Y) Above(int x, int y, int width, int height, int left, int top, int right, int bottom) =>
            (Math.Max(left, Math.Min(x - width, right - width)),
             Math.Max(top, Math.Min(y - height, bottom - height)));
    }
}
