using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SleepTimer
{
    static class WindowsChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [DllImport("user32.dll")] static extern int GetGuiResources(IntPtr process, int flags);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int msg, IntPtr wParam, IntPtr lParam);
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
        static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
        static void Check(bool okay, string text) { if (!okay) throw new Exception(text); }
        static void Pump(int milliseconds)
        {
            var frame = new DispatcherFrame();
            var stop = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            stop.Tick += (s, e) => { stop.Stop(); frame.Continue = false; };
            stop.Start(); Dispatcher.PushFrame(frame);
        }
        static MainWindow Open()
        {
            var window = new MainWindow { TestMode = true };
            Field<DispatcherTimer>(window, "ticker").Stop();
            window.Show(); window.UpdateLayout(); return window;
        }
        static void Close(MainWindow window) { Set(window, "quitting", true); window.Close(); window.Dispose(); }
        static void Capture(MainWindow window, string name)
        {
            window.UpdateLayout();
            var root = (FrameworkElement)window.Content;
            int width = (int)Math.Ceiling(root.ActualWidth), height = (int)Math.Ceiling(root.ActualHeight);
            var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            image.Render(root);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
            Directory.CreateDirectory("validation-output");
            using (var file = File.Create(Path.Combine("validation-output", name + ".png"))) png.Save(file);
        }
        [STAThread] static int Main()
        {
            // Preserve the caller's settings. No expiry polling or power action runs in this suite.
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\SleepTimer"))
            {
                var saved = key.GetValueNames().ToDictionary(n => n, n => Tuple.Create(key.GetValue(n), key.GetValueKind(n)));
                try
                {
                    Settings.Set("start_with_windows", false); Settings.Set("awake", false);
                    Settings.Set("clock_mode", false); Settings.Set("duration", 1800);
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    Theme.Init(app);
                    var window = Open();
                    try
                    {
                        var countdown = Field<Countdown>(window, "countdown");
                        var face = Field<TimerFace>(window, "face");
                        Call(window, "SetAction", "shutdown");
                        Call(window, "AddTime", 1800);
                        Check(countdown.Duration == 3600, "Wheel did not stop at hour");
                        Call(window, "AddTime", 300); Check(countdown.Duration == 3600, "400 ms hold ignored");
                        Call(window, "AddTime", -300); Check(countdown.Duration == 3300, "Immediate reversal blocked");
                        Pump(450);
                        Call(window, "AddTime", 900); Check(countdown.Duration == 3600, "Modifier-sized step skipped hour");
                        Pump(450);
                        Call(window, "AddTime", 1800); Check(countdown.Duration == 5400, "Hour hold did not expire");
                        Pump(100);
                        Check(face.Position > 3600 && face.Position < 5400, "Dial edit animation has no intermediate progress");
                        Check(face.DisplayTime == TimeText.Clock(Math.Round(face.Position / 60) * 60), "Counter does not follow minute increments");
                        Pump(650);
                        Check(Math.Abs(face.Position - 5400) < 1, "Dial did not reach target");
                        face.UpdatePosition(3600, false, false);
                        face.UpdatePosition(3660, true, false);
                        Pump(80); Check(face.Position >= 3600 && face.Position <= 3660, "Forward rollover reversed");
                        Pump(650);
                        face.UpdatePosition(3360, true, false);
                        Pump(80); Check(face.Position >= 3360 && face.Position <= 3660, "Backward rollover reversed");
                        Pump(650);

                        Call(window, "SetMode", true);
                        Check(Field<bool>(window, "clockMode"), "At time mode failed");
                        Check(Field<TextBox>(window, "clockField").Visibility == Visibility.Visible, "Clock field stayed hidden");
                        Check(face.ClockMode, "Clock display was not preserved");
                        Call(window, "SetMode", false);
                        Call(window, "StartQuickTimer", 150);
                        countdown.Pause(); Call(window, "UpdateView", false);
                        Set(window, "trayPage", 3);
                        var rows = (System.Collections.Generic.List<MenuRow>)Call(window, "TrayRows");
                        rows.Single(r => r.Text == "Restart").Click();
                        Check(Field<PowerAction>(window, "action").Key == "shutdown", "Next action changed paused timer");
                        Call(window, "StartQuickTimer", 60);
                        Check(Field<PowerAction>(window, "action").Key == "restart", "Quick action was not applied");
                        Check(!Field<bool>(window, "clockMode"), "Quick timer did not switch to Duration");
                        countdown.Reset(); Call(window, "UpdateView", false);

                        Call(window, "SetAwake", 15);
                        Check(Field<int>(window, "awakeMinutes") == 15, "Keep awake duration failed");
                        Call(window, "SetAwake", 0);
                        var input = Field<TextBox>(window, "quickMinutes");
                        input.Text = "90"; Call(window, "AddQuickTimer");
                        Check(Field<QuickTimers>(window, "quickTimers").All.Contains(90), "Custom quick timer not added");
                        input.Text = "0"; Call(window, "AddQuickTimer");
                        Check(!Field<QuickTimers>(window, "quickTimers").All.Contains(0), "Invalid quick timer accepted");
                        var palette = new ColorPicker(Theme.Accent) { Owner = window };
                        palette.Show(); Pump(50); palette.Close();
                    }
                    finally { Close(window); }

                    var visual = Open();
                    try
                    {
                        var countdown = Field<Countdown>(visual, "countdown");
                        countdown.SetDuration(10800); Call(visual, "UpdateView", false); Pump(150);
                        Capture(visual, "wpf-main");
                        Call(visual, "ToggleSettings"); Pump(300); Capture(visual, "wpf-settings");
                        Call(visual, "SelectTab", 1); Pump(300); Capture(visual, "wpf-appearance");
                        Call(visual, "SelectTab", 2); Pump(300); Capture(visual, "wpf-timers");
                        Call(visual, "ToggleSettings"); Pump(300);
                        var awake = Field<PopupMenu>(visual, "awakeMenu");
                        var anchor = Field<Button>(visual, "awakeChip");
                        for (int i = 0; i < 2; i++)
                        {
                            awake.Show(new[] { new MenuRow { Text = "Off" } }, anchor);
                            Pump(100);
                            Check(awake.Visible, "WPF popup did not reopen");
                            awake.Close(); Pump(200);
                        }
                        SendMessage(new WindowInteropHelper(visual).Handle, 0x0112, (IntPtr)0xF100, IntPtr.Zero);
                        var info = new GuiThreadInfo { Size = Marshal.SizeOf(typeof(GuiThreadInfo)) };
                        Check(GetGUIThreadInfo(0, ref info) && (info.Flags & 4) == 0, "Alt release activated menu mode");
                        foreach (double scale in new[] { 1d, 1.5, 2 })
                        {
                            var face = Field<TimerFace>(visual, "face");
                            face.LayoutTransform = new ScaleTransform(scale, scale);
                            face.Measure(new System.Windows.Size(320 * scale, 328 * scale));
                            Check(face.DesiredSize.Width >= 320 * scale - 1, "Dial does not scale with DPI");
                            face.LayoutTransform = Transform.Identity;
                        }
                    }
                    finally { Close(visual); }
                    for (int i = 0; i < 3; i++) { var warm = Open(); Pump(30); Close(warm); }
                    GC.Collect(); GC.WaitForPendingFinalizers(); Pump(100);
                    int baseline = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                    for (int i = 0; i < 20; i++)
                    {
                        var repeat = Open();
                        Call(repeat, "AddTime", 300); Call(repeat, "ToggleSettings");
                        Pump(20); Close(repeat);
                    }
                    GC.Collect(); GC.WaitForPendingFinalizers(); Pump(150);
                    int final = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                    Check(final - baseline < 32, "GDI handle growth: " + baseline + " -> " + final);
                    Console.WriteLine("WPF checks passed; GDI handles " + baseline + " -> " + final);
                    return 0;
                }
                catch (Exception ex) { Console.WriteLine("::error::" + ex); return 1; }
                finally
                {
                    Native.KeepAwake(false, false);
                    foreach (var name in key.GetValueNames()) key.DeleteValue(name, false);
                    foreach (var value in saved) key.SetValue(value.Key, value.Value.Item1, value.Value.Item2);
                }
            }
        }
    }
}
