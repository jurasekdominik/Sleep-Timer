using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace SleepTimer
{
    static class Native
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint flags);
        [DllImport("powrprof.dll", SetLastError = true)] static extern bool SetSuspendState(bool hibernate, bool force, bool disableWakeEvents);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int processId);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr handle);
        [DllImport("kernel32.dll")] public static extern bool SetDllDirectory(string path);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);

        /// <summary>1 ms timer resolution while an animation runs (default ~16 ms). Calls must pair up.</summary>
        public static void HiResTimer(bool on) { try { if (on) timeBeginPeriod(1); else timeEndPeriod(1); } catch { } }

        public static void DarkTitlebar(IntPtr hwnd)
        {
            try { int on = 1; DwmSetWindowAttribute(hwnd, 20, ref on, 4); } catch { }
        }

        /// <summary>Keeps Windows from idling into sleep, and optionally the display from turning off.
        /// One state per thread: always call from the UI thread with the combined wish of every feature.</summary>
        public static void KeepAwake(bool system, bool display)
        {
            const uint Continuous = 0x80000000, SystemRequired = 0x00000001, DisplayRequired = 0x00000002;
            try { SetThreadExecutionState(Continuous | (system || display ? SystemRequired : 0) | (display ? DisplayRequired : 0)); } catch { }
        }

        /// <summary>Real sleep, not hibernate. Blocks until the PC wakes up again.</summary>
        public static void Sleep(bool force)
        {
            if (!SetSuspendState(false, force, false)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        public static void Shutdown(bool restart, bool force)
        {
            string args = (restart ? "/r" : "/s") + (force ? " /f" : "") + " /t 0";
            using (var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            })) { }
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
    }

    /// <summary>
    /// User settings in HKCU\Software\SleepTimer. Booleans are stored as "true"/"false",
    /// numbers as DWORDs; reads accept either form.
    /// </summary>
    static class Settings
    {
        const string KeyPath = @"Software\SleepTimer";

        static string Read(string name)
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(KeyPath)) return k?.GetValue(name)?.ToString(); }
            catch { return null; }
        }

        public static bool Get(string name, bool fallback)
        {
            string v = Read(name);
            return v == null ? fallback : v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static int Get(string name, int fallback) => int.TryParse(Read(name), out int v) ? v : fallback;

        public static string Get(string name, string fallback) => Read(name) ?? fallback;

        public static void Set(string name, object value)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(KeyPath))
                    k.SetValue(name, value is bool b ? (b ? "true" : "false") : value);
            }
            catch { }
        }
    }

    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", Name = "SleepTimer";

        /// <summary>Adds or removes the HKCU Run entry. The app then starts minimized to the tray.</summary>
        public static void Set(bool enabled)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (enabled) k.SetValue(Name, $"\"{System.Reflection.Assembly.GetExecutingAssembly().Location}\" {Program.TrayArg}");
                    else k.DeleteValue(Name, false);
                }
            }
            catch { }
        }
    }

    /// <summary>One instance per user session. A second launch asks the running one to show its window.</summary>
    static class SingleInstance
    {
        const string MutexName = @"Local\SleepTimer.SingleInstance", EventName = @"Local\SleepTimer.Show";
        static Mutex mutex;  // held for the whole process lifetime
        static EventWaitHandle show;

        public static bool Claim()
        {
            mutex = new Mutex(false, MutexName, out bool created);
            if (created)
            {
                // Created right away, so a second launch while this one is still starting up is not lost.
                show = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
                return true;
            }
            try
            {
                using (var running = EventWaitHandle.OpenExisting(EventName))
                {
                    // This process was just started by the user, so it may take the foreground;
                    // hand that right to the running instance so its window can come to the front.
                    Native.AllowSetForegroundWindow(-1);
                    running.Set();
                }
            }
            catch { }
            return false;
        }

        public static void Listen(Action onShow)
        {
            // onShow can throw if the window is already closing; on this thread that would crash the app
            new Thread(() => { while (show.WaitOne()) try { onShow(); } catch (InvalidOperationException) { } }) { IsBackground = true }.Start();
        }
    }
}
