using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace SleepTimer
{
    sealed class PowerAction
    {
        public string Key, Label, Verb, Would, At, RunTitle, ClockTitle, FinalTitle, FinalSub;

        public static readonly PowerAction[] All =
        {
            new PowerAction { Key = "shutdown", Label = "Shut down", Verb = "shut down", Would = "Would shut down at", At = "Shutdown at",
                              RunTitle = "SHUTTING DOWN IN", ClockTitle = "SHUT DOWN AT", FinalTitle = "GOODNIGHT", FinalSub = "Shutting down..." },
            new PowerAction { Key = "sleep", Label = "Sleep", Verb = "go to sleep", Would = "Would sleep at", At = "Sleep at",
                              RunTitle = "SLEEPING IN", ClockTitle = "SLEEP AT", FinalTitle = "GOODNIGHT", FinalSub = "Going to sleep..." },
            new PowerAction { Key = "restart", Label = "Restart", Verb = "restart", Would = "Would restart at", At = "Restart at",
                              RunTitle = "RESTARTING IN", ClockTitle = "RESTART AT", FinalTitle = "SEE YOU SOON", FinalSub = "Restarting..." },
        };

        public static PowerAction Find(string key) => All.FirstOrDefault(a => a.Key == key) ?? All[0];
    }

}
