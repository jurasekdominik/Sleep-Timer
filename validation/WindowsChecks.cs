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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SleepTimer
{
    static class WindowsChecks
    {
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out WindowRect rectangle);
        [StructLayout(LayoutKind.Sequential)] struct WindowRect { public int Left, Top, Right, Bottom; }
        static WindowRect PopupBounds(PopupMenu menu)
        {
            var popup = Field<Popup>(menu, "popup");
            var source = (HwndSource)PresentationSource.FromVisual(popup.Child);
            Check(source != null && GetWindowRect(source.Handle, out _), "Popup has no native window");
            GetWindowRect(source.Handle, out var rectangle); return rectangle;
        }
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
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
        static object Call(object owner, string name, params object[] args)
        {
            var method = owner.GetType().GetMethod(name, Private);
            if (method == null) throw new MissingMethodException(owner.GetType().FullName, name);
            return method.Invoke(owner, args);
        }
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
            CaptureControl((FrameworkElement)window.Content, name);
        }
        static void CaptureControl(FrameworkElement root, string name, double scale = 1)
        {
            root.UpdateLayout();
            int width = (int)Math.Ceiling(root.ActualWidth * scale), height = (int)Math.Ceiling(root.ActualHeight * scale);
            var image = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            image.Render(root);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
            Directory.CreateDirectory("validation-output");
            string path = Path.Combine("validation-output", name + ".png");
            using (var file = File.Create(path)) png.Save(file);
            Console.WriteLine("PREVIEW:" + name + ":" + path);
        }
        static void CheckShortcuts(MainWindow window)
        {
            var countdown = Field<Countdown>(window, "countdown");
            var tray = Field<System.Windows.Forms.NotifyIcon>(window, "tray");
            Check(Field<Shortcut[]>(window, "shortcuts").All(binding => !binding.IsBound), "Fresh shortcuts must be unbound");
            Call(window, "RunShortcut", 0);
            Check(countdown.State == TimerState.Running && tray.BalloonTipText.Contains("Timer started"), "Start shortcut or notification failed");
            countdown.Pause(); Call(window, "RunShortcut", 0);
            Check(countdown.State == TimerState.Idle && tray.BalloonTipText.Contains("cancelled"), "Paused timer was not cancelled or notified");
            Call(window, "StartQuickTimer", 150); Call(window, "Cancel"); window.Hide();
            Call(window, "RunShortcut", 1);
            Check(countdown.State == TimerState.Running && countdown.Duration == 9000 && tray.BalloonTipText.Contains("Quick timer started"), "Last quick timer shortcut failed while hidden");
            Check(window.Title == Field<TimerFace>(window, "face").Time && window.TaskbarItemInfo.Description.Contains("Running"), "Thumbnail title or full shell description missing");
            Call(window, "RunShortcut", 0);
            Set(window, "recordingShortcut", 0); Call(window, "RunShortcut", 0);
            Check(countdown.State == TimerState.Idle, "Shortcut fired while recording");
            Set(window, "recordingShortcut", -1);
            Set(window, "executing", true); Call(window, "RunShortcut", 0);
            Check(countdown.State == TimerState.Idle, "Shortcut fired during a power action");
            Set(window, "executing", false); window.Show();
            var handle = new WindowInteropHelper(window).Handle;
            Shortcut bound = default;
            for (int key = 0x87; key >= 0x7C; key--)
            {
                Shortcut.TryCreate(7, key, out var candidate);
                if ((bool)Call(window, "SetShortcut", 0, candidate)) { bound = candidate; break; }
            }
            Check(bound.IsBound, "Could not register a test shortcut");
            try
            {
                Check(!(bool)Call(window, "SetShortcut", 1, bound), "Duplicate shortcuts were accepted");
                Check(Settings.Get("hotkey_quick_key", 0) == 0, "Rejected duplicate was saved");
                SendMessage(handle, 0x0312, (IntPtr)0x1230, (IntPtr)((0x41 << 16) | bound.Modifiers));
                Check(countdown.State == TimerState.Idle, "Stale hotkey message started a timer");
                var message = (IntPtr)((bound.Key << 16) | bound.Modifiers);
                window.WindowState = WindowState.Minimized;
                Check(!window.IsVisible && Field<bool[]>(window, "shortcutRegistered")[0], "Minimizing to tray unregistered the global shortcut");
                SendMessage(handle, 0x0312, (IntPtr)0x1230, message);
                Check(countdown.State == TimerState.Running, "Global hotkey message did not start the timer while hidden");
                SendMessage(handle, 0x0312, (IntPtr)0x1230, message);
                Check(countdown.State == TimerState.Idle, "Global hotkey message did not cancel the timer while hidden");
                Call(window, "ShowWindow");
                Call(window, "UnregisterShortcuts");
                var reopened = Open();
                try
                {
                    Check(Field<Shortcut[]>(reopened, "shortcuts")[0].Matches(bound)
                        && Field<bool[]>(reopened, "shortcutRegistered")[0], "Shortcut did not survive reopening");
                }
                finally { Close(reopened); }
                Call(window, "RegisterShortcuts");
                using (var ready = new System.Threading.ManualResetEvent(false))
                using (var release = new System.Threading.ManualResetEvent(false))
                {
                    Shortcut blocked = default;
                    // A separate thread owns this reservation, like another app.
                    var owner = new System.Threading.Thread(() =>
                    {
                        try
                        {
                            for (int key = 0x87; key >= 0x7C; key--)
                            {
                                Shortcut.TryCreate(7, key, out var candidate);
                                if (Native.RegisterHotKey(IntPtr.Zero, 0x1500, 7 | 0x4000, (uint)key)) { blocked = candidate; break; }
                            }
                            ready.Set(); release.WaitOne();
                        }
                        finally { if (blocked.IsBound) Native.UnregisterHotKey(IntPtr.Zero, 0x1500); }
                    }) { IsBackground = true };
                    owner.Start();
                    try
                    {
                        Check(ready.WaitOne(5000) && blocked.IsBound, "Could not reserve a conflicting shortcut");
                        Check(!(bool)Call(window, "SetShortcut", 0, blocked), "Unavailable system shortcut was accepted");
                        Check(Field<Shortcut[]>(window, "shortcuts")[0].Matches(bound)
                            && Field<bool[]>(window, "shortcutRegistered")[0]
                            && Settings.Get("hotkey_toggle_key", 0) == bound.Key, "Failed binding replaced the existing shortcut");
                    }
                    finally { release.Set(); Check(owner.Join(5000), "Shortcut reservation thread did not stop"); }
                }
            }
            finally { Call(window, "SetShortcut", 0, default(Shortcut)); Call(window, "SetShortcut", 1, default(Shortcut)); }
            Check(Native.RegisterHotKey(handle, 0x1500, (uint)(bound.Modifiers | 0x4000), (uint)bound.Key), "Clearing did not release the global shortcut");
            Native.UnregisterHotKey(handle, 0x1500);
            SendMessage(handle, 0x0312, (IntPtr)0x1230, (IntPtr)((bound.Key << 16) | bound.Modifiers));
            Check(countdown.State == TimerState.Idle, "Unbound shortcut still fires");
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
                    Settings.Set("circle_sweep_ms", 600); Settings.Set("counter_sweep_ms", 600);
                    Settings.Set("timer_font", "segoe"); Settings.Set("ui_font", "segoe");
                    Settings.Set("timer_font_weight", 400); Settings.Set("ui_font_weight", 400);
                    Settings.Set("hotkey_toggle_key", 0); Settings.Set("hotkey_toggle_modifiers", 0);
                    Settings.Set("hotkey_quick_key", 0); Settings.Set("hotkey_quick_modifiers", 0);
                    Settings.Set("validation_bool", 1); Check(Settings.Get("validation_bool", false), "DWORD true setting was ignored");
                    Settings.Set("validation_bool", 0); Check(!Settings.Get("validation_bool", true), "DWORD false setting was ignored");
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
                        Check(face.DisplayTime == TimeText.Clock(Math.Round(face.CounterPosition / 60) * 60), "Counter does not follow minute increments");
                        Pump(650);
                        Check(Math.Abs(face.Position - 5400) < 1, "Dial did not reach target");
                        face.UpdatePosition(3600, false, false);
                        face.UpdatePosition(3660, true, false);
                        Pump(80); Check(face.Position >= 3600 && face.Position <= 3660, "Forward rollover reversed");
                        Pump(650);
                        face.UpdatePosition(3360, true, false);
                        Pump(80); Check(face.Position >= 3360 && face.Position <= 3660, "Backward rollover reversed");
                        Pump(650);

                        var circleInput = Field<TextBox>(window, "circleDuration");
                        var textInput = Field<TextBox>(window, "counterDuration");
                        Check(circleInput.Text == "600" && textInput.Text == "600", "Animation defaults changed");
                        circleInput.Text = "120"; Call(window, "CommitAnimationInput", circleInput, 0);
                        textInput.Text = "900"; Call(window, "CommitAnimationInput", textInput, 1);
                        Check(Settings.Get("circle_sweep_ms", -1) == 120 && Settings.Get("counter_sweep_ms", -1) == 900, "Independent timings were not saved");
                        var reopened = Open();
                        try
                        {
                            var reloaded = Field<TimerFace>(reopened, "face");
                            Check(reloaded.CircleMilliseconds == 120 && reloaded.CounterMilliseconds == 900, "Timing overrides did not survive reopen");
                        }
                        finally { Close(reopened); }
                        window.Activate();
                        face.Snap(1800); face.Time = TimeText.Clock(3600);
                        face.UpdatePosition(3600, true, false); Pump(250);
                        Check(Math.Abs(face.Position - 3600) < 1, "Fast circle did not complete");
                        Check(face.CounterPosition > 1800 && face.CounterPosition < 3600 && face.DisplayTime != face.Time, "Slow text followed the fast circle");
                        face.UpdatePosition(3600, false, false); Pump(750);
                        Check(Math.Abs(face.CounterPosition - 3600) < 1, "Status refresh interrupted independent text animation");
                        circleInput.Text = "900"; Call(window, "CommitAnimationInput", circleInput, 0);
                        textInput.Text = "120"; Call(window, "CommitAnimationInput", textInput, 1);
                        face.Snap(1800); face.UpdatePosition(3600, true, false); Pump(250);
                        Check(face.Position > 1800 && face.Position < 3600, "Slow circle followed the fast text");
                        Check(Math.Abs(face.CounterPosition - 3600) < 1 && face.DisplayTime == face.Time, "Fast counter did not complete independently");
                        Pump(750);
                        foreach (string bad in new[] { "-1", "1.5", "10001", "bad", "" })
                        {
                            circleInput.Text = bad; Call(window, "CommitAnimationInput", circleInput, 0);
                            Check(face.CircleMilliseconds == 900 && Settings.Get("circle_sweep_ms", -1) == 900, "Invalid edit replaced the saved timing");
                        }
                        circleInput.Text = "0"; Call(window, "CommitAnimationInput", circleInput, 0);
                        textInput.Text = "0"; Call(window, "CommitAnimationInput", textInput, 1);
                        face.Snap(1800); face.UpdatePosition(3600, true, false);
                        Check(face.Position == 3600 && face.CounterPosition == 3600, "Zero duration did not apply instantly");
                        circleInput.Text = "600"; Call(window, "CommitAnimationInput", circleInput, 0);
                        textInput.Text = "600"; Call(window, "CommitAnimationInput", textInput, 1);

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
                        var palette = new ColorPicker(Theme.Accent);
                        palette.Show(face); Pump(50); palette.Close();
                        Console.WriteLine("CHECK: global shortcuts");
                        CheckShortcuts(window);
                    }
                    finally { Close(window); }

                    var visual = Open();
                    try
                    {
                        var countdown = Field<Countdown>(visual, "countdown");
                        countdown.SetDuration(10800); Call(visual, "UpdateView", false); Pump(150);
                        var idleIcon = visual.Icon;
                        countdown.Start(10800); countdown.Adjust(-5400); countdown.Pause();
                        Call(visual, "UpdateView", false); Pump(50);
                        Check(Field<string>(visual, "iconKey").EndsWith("|24"), "Taskbar/tray progress does not use half of the total three-hour countdown");
                        Check(visual.Icon != idleIcon && visual.Icon.IsFrozen, "Taskbar icon was not updated with a copied progress bitmap");
                        Check(visual.Title.Contains("Paused"), "Visible window title does not show timer state");
                        visual.Hide(); Call(visual, "UpdateView", false);
                        Check(visual.Title == Field<TimerFace>(visual, "face").Time, "Hidden thumbnail title is not compact");
                        visual.Show(); countdown.Reset(); Call(visual, "UpdateView", false); Pump(50);
                        Check(visual.Title.Contains("Ready"), "Ready state missing from visible title");
                        Check(Program.Version == "1.1", "Release version is not 1.1");
                        Check(((FrameworkElement)visual.Content).ActualHeight >= 508, "Client area clips the footer");
                        Console.WriteLine("CHECK: font families and weights");
                        Check(AppFonts.All.Length >= 6 && AppFonts.All.Select(font => font.Key).Distinct().Count() == AppFonts.All.Length,
                            "Font choices are missing or duplicate");
                        var installed = Fonts.SystemFontFamilies.Select(font => font.Source).ToArray();
                        Check(AppFonts.All.All(font => font.Key == "segoe" || installed.Contains(font.Family, StringComparer.OrdinalIgnoreCase)),
                            "Unavailable fonts are offered as installed choices");
                        Check(AppFonts.All.Any(font => font.Key == "aptos") == installed.Contains("Aptos", StringComparer.OrdinalIgnoreCase),
                            "Aptos availability does not match installed fonts");
                        Check(AppFonts.Timer.Key == "segoe" && AppFonts.Ui.Key == "segoe", "Font defaults must both be Segoe UI");
                        Check(Field<Button>(visual, "fontButton").Visibility == Visibility.Collapsed, "Font icon is visible outside settings");
                        Check(AppFonts.Find("unknown") == AppFonts.All[0], "Invalid font setting has no safe fallback");
                        Call(visual, "OpenFontGallery", false); Pump(100);
                        CaptureControl((FrameworkElement)Field<Window>(visual, "fontGallery").Content, "wpf-font-gallery");
                        Field<Window>(visual, "fontGallery").Close();
                        var fontFace = Field<TimerFace>(visual, "face");
                        foreach (var font in AppFonts.All)
                        {
                            Call(visual, "ApplyFont", font.Key, false); Pump(50);
                            Capture(visual, "wpf-font-" + font.Key);
                            var artwork = Field<System.Collections.IDictionary>(fontFace, "textArtwork")[45.3];
                            Check((Typeface)artwork.GetType().GetField("Typeface").GetValue(artwork) == AppFonts.Timer.Typeface,
                                "Changing the timer font left stale cached glyphs");
                            Check(AppFonts.Find(Settings.Get("timer_font", "")).Key == font.Key && AppFonts.Ui.Key == "segoe", "Timer font was not saved independently of UI");
                        }
                        Call(visual, "ApplyFont", "segoe", false); Pump(50);
                        foreach (var font in AppFonts.Choices(true))
                        {
                            Call(visual, "ApplyFont", font.Key, true); Pump(50);
                            Check(visual.FontFamily.Source == AppFonts.Ui.Typeface.FontFamily.Source && Field<TextBox>(visual, "clockField").FontFamily.Source == AppFonts.Ui.Typeface.FontFamily.Source,
                                "UI font did not update window or input controls");
                            Check(AppFonts.Timer.Key == "segoe", "UI font changed the timer font");
                            Check(Settings.Get("ui_font", "") == font.Key && Settings.Get("timer_font", "") == "segoe", "Font selections were not saved independently");
                            var buttons = Field<System.Collections.Generic.List<Button>>(visual, "tabButtons");
                            Check(Canvas.GetLeft(buttons.Last()) + buttons.Last().Width <= 320.5, "UI font pushes settings tabs outside the window");
                            if (font.Key == "aptos" || font.Key == "verdana" || font.Key == "consolas") Capture(visual, "wpf-ui-font-" + font.Key);
                        }
                        Check(AppFonts.Choices(true).All(font => font.ForUi && font.Key != "bahnschrift"), "Display fonts leak into readable UI choices");
                        Check(AppFonts.Select("bahnschrift", true, -1).Key == "segoe", "Old Bahnschrift UI choice has no readable fallback");
                        Check(AppFonts.Select("light", false, -1).Typeface.Weight == FontWeights.Light, "Legacy Segoe light preset was not migrated");
                        foreach (var font in AppFonts.All)
                        {
                            Call(visual, "ApplyFont", font.Key, false);
                            foreach (var weight in font.Weights)
                            {
                                Call(visual, "ApplyFontWeight", weight.ToOpenTypeWeight(), false); Pump(20);
                                Check(AppFonts.Timer.Key == font.Key && AppFonts.Timer.Typeface.Weight == weight,
                                    "Timer weight selection does not match an installed face");
                                Check(Settings.Get("timer_font_weight", 0) == weight.ToOpenTypeWeight(), "Timer weight was not saved");
                            }
                        }
                        Call(visual, "ApplyFont", "segoe", false); Call(visual, "ApplyFontWeight", 400, false);
                        Call(visual, "ApplyFont", "segoe", true); Call(visual, "ApplyFontWeight", 700, true);
                        Check(AppFonts.Timer.Typeface.Weight == FontWeights.Normal && AppFonts.Ui.Typeface.Weight == FontWeights.Bold,
                            "UI and timer weights are not independent");
                        Check(AppFonts.Select(Settings.Get("ui_font", ""), true, Settings.Get("ui_font_weight", -1)).Typeface.Weight == FontWeights.Bold,
                            "UI weight does not reload from settings");
                        Call(visual, "ApplyFontWeight", 400, true); Pump(50);
                        Call(visual, "OpenFontGallery", true); Pump(100);
                        Check(((FontWindow)Field<Window>(visual, "fontGallery")).IsUi, "UI gallery opened with the wrong target");
                        CaptureControl((FrameworkElement)Field<Window>(visual, "fontGallery").Content, "wpf-ui-font-gallery");
                        Field<Window>(visual, "fontGallery").Close();
                        Capture(visual, "wpf-main");
                        CaptureControl((FrameworkElement)visual.Content, "wpf-main-150", 1.5);
                        CaptureControl((FrameworkElement)visual.Content, "wpf-main-200", 2);
                        Check(Field<Canvas>(visual, "main").Children.OfType<BrandFooter>().Count() == 1, "Original wordmark missing");
                        Check(Math.Abs(Field<TranslateTransform>(visual, "modeSlide").X - 54.6666667) < 1, "Duration selector is not centered");
                        Call(visual, "SetMode", true); Pump(400);
                        var clockBox = Field<TextBox>(visual, "clockField");
                        var clockHost = (ScrollViewer)clockBox.Template.FindName("PART_ContentHost", clockBox);
                        Check(clockBox.Text.Contains(":") && clockHost.ViewportHeight + 1 >= clockHost.ExtentHeight, "Clock text is vertically clipped");
                        Check(Math.Abs(Field<TranslateTransform>(visual, "modeSlide").X) < 1, "At time selector did not slide left");
                        Capture(visual, "wpf-clock");
                        Call(visual, "SetMode", false); Pump(400);
                        var selector = Field<ActionSelector>(visual, "picker");
                        Call(selector, "SetOpen", true); Pump(300); Capture(visual, "wpf-action-selector");
                        Call(selector, "SetOpen", false); Pump(300);
                        Call(visual, "ToggleSettings"); Pump(300);
                        Check(Field<Button>(visual, "fontButton").Visibility == Visibility.Visible, "Font icon missing in settings");
                        Field<Button>(visual, "fontButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Pump(100);
                        var fonts = (FontWindow)Field<Window>(visual, "fontGallery");
                        Check(fonts.IsVisible && !fonts.IsUi, "Font button did not open the font window directly");
                        Field<Button>(fonts, "uiTab").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        Check(fonts.IsUi, "UI submenu did not switch inside the font window");
                        Field<Button>(fonts, "timerTab").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        Check(!fonts.IsUi, "Timer submenu did not switch inside the font window");
                        var fontChoice = Field<System.Collections.Generic.List<Button>>(fonts, "choices").Last();
                        var pickedFont = (AppFont)fontChoice.Tag;
                        fontChoice.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                            { RoutedEvent = Control.MouseDoubleClickEvent });
                        Check(!fonts.IsVisible && AppFonts.Timer.Key == pickedFont.Key, "Double-click did not select the font and close the window");
                        Call(visual, "ApplyFont", "segoe", false); Call(visual, "ApplyFontWeight", 400, false);
                        var toggle = Field<ToggleButton>(visual, "forceSwitch");
                        var thumb = (Border)toggle.Template.FindName("thumb", toggle);
                        Check(((SolidColorBrush)thumb.Background).Color == Colors.White && thumb.Width == 28 && thumb.Height == 20, "Original switch capsule was changed");
                        Capture(visual, "wpf-settings");
                        Call(visual, "SelectTab", 1); Pump(300);
                        var accentBox = Field<TextBox>(visual, "accentField");
                        var accentHost = (ScrollViewer)accentBox.Template.FindName("PART_ContentHost", accentBox);
                        Check(accentHost.ViewportHeight + 1 >= accentHost.ExtentHeight, "Hex field text is vertically clipped");
                        Check(accentHost.ViewportWidth + 1 >= accentHost.ExtentWidth, "Hex field text is horizontally clipped");
                        Capture(visual, "wpf-appearance");
                        Call(visual, "SelectTab", 2); Pump(300); Capture(visual, "wpf-timers");
                        Call(visual, "ToggleSettings"); Pump(300);
                        var awake = Field<PopupMenu>(visual, "awakeMenu");
                        var anchor = Field<Button>(visual, "awakeChip");
                        for (int i = 0; i < 2; i++)
                        {
                            awake.Show(new[] { new MenuRow { Text = "Off" } }, anchor);
                            Pump(100);
                            Check(awake.Visible, "WPF popup did not reopen");
                            var panel = Field<StackPanel>(awake, "items");
                            var row = panel.Children.OfType<Button>().Single();
                            Check(row.Height == 28 && row.Style == Application.Current.FindResource("MenuButton"), "Popup row uses boxed button styling");
                            awake.Close(); Pump(200);
                        }
                        awake.Show(new[] { new MenuRow { Text = "Keep awake", Right = "00:15" }, new MenuRow { Text = "Quit" } }, anchor);
                        Pump(50);
                        var focusPanel = Field<StackPanel>(awake, "items");
                        focusPanel.Children.OfType<Button>().Last().Focus();
                        awake.SetRows(new[] { new MenuRow { Text = "Keep awake", Right = "00:14" }, new MenuRow { Text = "Quit" } });
                        Check(focusPanel.Children.OfType<Button>().Last().IsKeyboardFocusWithin, "Live menu update lost keyboard focus");
                        awake.Close(); Pump(200);
                        awake.Show((System.Collections.Generic.List<MenuRow>)Call(visual, "ChipRows"), anchor);
                        Pump(200); CaptureControl((FrameworkElement)Field<Popup>(awake, "popup").Child, "wpf-awake-menu"); awake.Close(); Pump(200);
                        var tooltip = new ToolTip { Content = "Click to start or pause", PlacementTarget = Field<TimerFace>(visual, "face") };
                        tooltip.IsOpen = true; Pump(100);
                        Check(((SolidColorBrush)tooltip.Background).Color == Theme.Surface && ((SolidColorBrush)tooltip.Foreground).Color == Theme.Text, "Tooltip does not use the app theme");
                        CaptureControl(tooltip, "wpf-tooltip"); tooltip.IsOpen = false;
                        Call(visual, "ToggleSettings"); Call(visual, "SelectTab", 3); Pump(300);
                        Capture(visual, "wpf-keybinds");
                        Check(Field<TextBox[]>(visual, "shortcutFields").All(input => input.Text == "Unbound"), "Keybinds are not unbound by default");
                        Call(visual, "ToggleSettings"); Pump(300);
                        var trayPopup = Field<PopupMenu>(visual, "trayMenu");
                        Set(visual, "trayPage", 0);
                        trayPopup.Show((System.Collections.Generic.List<MenuRow>)Call(visual, "TrayRows"), anchor);
                        Pump(200); CaptureControl((FrameworkElement)Field<Popup>(trayPopup, "popup").Child, "wpf-tray-menu"); trayPopup.Close(); Pump(200);
                        Console.WriteLine("CHECK: physical tray placement");
                        var screenPoint = visual.PointToScreen(new System.Windows.Point(300, 450));
                        Set(visual, "trayClick", screenPoint); Call(visual, "ShowTrayMenu"); Pump(100);
                        var firstBounds = PopupBounds(trayPopup);
                        Call(visual, "SetTrayPage", 1);
                        var trayItems = Field<StackPanel>(trayPopup, "items");
                        var choice = trayItems.Children.OfType<Button>().Single(button => button.Content is Grid content
                            && content.Children.OfType<TextBlock>().First().Text == "15 minutes");
                        choice.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Pump(200);
                        Check(!trayPopup.Visible, "Selecting keep awake did not close the tray popup");
                        visual.Hide();
                        Set(visual, "trayClick", screenPoint); Call(visual, "ShowTrayMenu"); Pump(100);
                        Check(trayPopup.Visible, "Tray popup cannot open while the window is hidden");
                        var nextBounds = PopupBounds(trayPopup);
                        Check(Math.Abs(firstBounds.Left - nextBounds.Left) <= 2 && Math.Abs(firstBounds.Top - nextBounds.Top) <= 2,
                            "Tray popup moved after selecting keep awake and reopening");
                        trayPopup.Close(); Call(visual, "SetAwake", 0); visual.Show(); Pump(200);
                        foreach (var monitor in System.Windows.Forms.Screen.AllScreens)
                        {
                            var area = monitor.WorkingArea;
                            int px = area.Left + area.Width * 3 / 4, py = area.Top + area.Height * 3 / 4;
                            Set(visual, "trayClick", new System.Windows.Point(px, py)); Call(visual, "ShowTrayMenu"); Pump(150);
                            var bounds = PopupBounds(trayPopup);
                            var expected = ScreenPlacement.Above(px, py, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top,
                                area.Left, area.Top, area.Right, area.Bottom);
                            Check(Math.Abs(bounds.Left - expected.X) <= 2 && Math.Abs(bounds.Top - expected.Y) <= 2,
                                "Tray popup ignored the clicked monitor's physical pixels");
                            trayPopup.Close(); Pump(200);
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
                    Console.WriteLine("CHECK: repeated window disposal");
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
