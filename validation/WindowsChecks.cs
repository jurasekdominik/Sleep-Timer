using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SleepTimer
{
    static class WindowsChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [DllImport("user32.dll")] static extern int GetGuiResources(IntPtr process, int flags);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
        [StructLayout(LayoutKind.Sequential)]
        struct GuiThreadInfo
        {
            public int Size, Flags;
            public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
            public Rectangle CaretRect;
        }
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Pump(int milliseconds)
        {
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
        }
        static MainForm Open()
        {
            var form = new MainForm(); form.Show();
            // Stop expiry polling before any test starts a countdown. No power action is ever fired.
            Field<System.Windows.Forms.Timer>(form, "ticker").Stop();
            return form;
        }
        [STAThread] static int Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Theme.Init();
            try
            {
                using (var form = Open())
                {
                    var timer = Field<Countdown>(form, "countdown");
                    var ring = Field<Ring>(form, "ring");
                    Set(form, "clockMode", false); Call(form, "SyncMode", false); Call(form, "SetAction", "shutdown");
                    timer.SetDuration(1800); Call(form, "UpdateView", false); Pump(200);
                    // Sample the real UI tween on both sides of an hourly rollover.
                    ring.SetTime(3600, false);
                    ring.SetTime(3660, true);
                    Pump(60);
                    var arc = Field<Tween>(ring, "sweep");
                    Check(arc.Value >= 3600 && arc.Value <= 3660, "Adding across hour reversed animation");
                    Pump(180);
                    Check(Math.Abs(arc.Value - 3660) < .001, "Forward animation missed its target");
                    ring.SetTime(3660, false);
                    ring.SetTime(3360, true);
                    Pump(60);
                    Check(arc.Value >= 3360 && arc.Value <= 3660, "Subtracting across hour reversed animation");
                    Pump(180);
                    Check(Math.Abs(arc.Value - 3360) < .001, "Backward animation missed its target");
                    Call(form, "StartQuickTimer", 150); timer.Pause(); Call(form, "UpdateView", false); Pump(200);
                    Set(form, "trayQuickPage", true); Set(form, "trayQuickActionPage", true);
                    var actions = (System.Collections.Generic.List<PickRow>)form.GetType().GetMethod("TrayRows", Private).Invoke(form, null);
                    actions.Single(r => r.Text == "Restart").Click();
                    Check(Field<PowerAction>(form, "action").Key == "shutdown", "Choosing next action changed paused countdown");
                    Call(form, "StartQuickTimer", 60);
                    Check(Field<PowerAction>(form, "action").Key == "restart", "Quick action was not applied");
                    Check(Field<ActionPicker>(form, "picker").Current == "restart", "Window picker did not synchronize");
                    timer.Reset(); Call(form, "UpdateView", false);
                    var wheel = typeof(Ring).GetMethod("OnMouseWheel", Private);
                    timer.SetDuration(1800);
                    // Bare Alt release must not enter the native menu loop or require pointer re-entry.
                    SendMessage(form.Handle, 0x0112, (IntPtr)0xF100, IntPtr.Zero);
                    var gui = new GuiThreadInfo { Size = Marshal.SizeOf(typeof(GuiThreadInfo)) };
                    Check(GetGUIThreadInfo(0, ref gui), "Could not inspect GUI thread after Alt release");
                    Check((gui.Flags & 4) == 0, "Alt release activated native menu mode");
                    var at = ring.PointToScreen(new Point(ring.Width / 2, Theme.D(50)));
                    SendMessage(ring.Handle, 0x020A, (IntPtr)(120 << 16), (IntPtr)((at.Y << 16) | (at.X & 0xFFFF)));
                    ring.FlushWheel();
                    Check(timer.Duration == 2100, "Wheel stopped responding after Alt release");
                    timer.SetDuration(1800);
                    for (int i = 0; i < 5000; i++) wheel.Invoke(ring, new object[] {new MouseEventArgs(MouseButtons.None, 0, 0, 0, 12)});
                    ring.FlushWheel();
                    Check(timer.Duration == 3600, "Fast-wheel burst skipped the first hour stop");
                    wheel.Invoke(ring, new object[] {new MouseEventArgs(MouseButtons.None, 0, 0, 0, 120)});
                    ring.FlushWheel(); Check(timer.Duration == 3600, "Wheel input bypassed the hour hold");
                    wheel.Invoke(ring, new object[] {new MouseEventArgs(MouseButtons.None, 0, 0, 0, -120)});
                    ring.FlushWheel(); Check(timer.Duration == 3300, "Wheel reversal was delayed by hour hold");
                }
                // Warm caches before checking that repeated construction/disposal does not grow GDI objects.
                for (int i = 0; i < 3; i++) using (var f = Open()) Pump(20);
                GC.Collect(); GC.WaitForPendingFinalizers();
                int baseline = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                for (int i = 0; i < 25; i++) using (var f = Open())
                {
                    Call(f, "ToggleSettings"); Pump(20); // Also close in the middle of a bitmap crossfade.
                    var list = Field<PopupList>(f, "awakeList");
                    list.Show(new System.Collections.Generic.List<PickRow> {new PickRow {Text = "Off"}}, new Point(100, 100), false, true);
                    Pump(20);
                }
                GC.Collect(); GC.WaitForPendingFinalizers(); Pump(100);
                int final = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                Check(final - baseline < 32, $"GDI resource growth: {baseline} -> {final}");
                Console.WriteLine($"Windows checks passed; GDI objects {baseline} -> {final}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("::error::" + ex.ToString().Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A"));
                return 1;
            }
            finally { Native.KeepAwake(false, false); Theme.DisposeFonts(); }
        }
    }
}
