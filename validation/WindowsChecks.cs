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
            CaptureControl((FrameworkElement)window.Content, name);
        }
        static void CaptureControl(FrameworkElement root, string name)
        {
            root.UpdateLayout();
            int width = (int)Math.Ceiling(root.ActualWidth), height = (int)Math.Ceiling(root.ActualHeight);
            var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var frame = new DrawingVisual();
            using (var g = frame.RenderOpen())
            {
                g.DrawRectangle(Theme.Brush(Theme.Bg), null, new Rect(0, 0, width, height));
                g.DrawRectangle(new VisualBrush(root) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(0, 0, width, height));
            }
            image.Render(frame);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
            Directory.CreateDirectory("validation-output");
            string path = Path.Combine("validation-output", name + ".png");
            using (var file = File.Create(path)) png.Save(file);
            Console.WriteLine("PREVIEW:" + name + ":" + Convert.ToBase64String(File.ReadAllBytes(path)));
        }

        static void WheelAt(MainWindow window, TimerFace face, System.Windows.Point local)
        {
            var previous = System.Windows.Forms.Cursor.Position;
            try
            {
                var at = face.PointToScreen(local);
                System.Windows.Forms.Cursor.Position = new System.Drawing.Point((int)at.X, (int)at.Y);
                window.Activate(); Pump(30);
                SendMessage(new WindowInteropHelper(window).Handle, 0x020A, (IntPtr)(120 << 16),
                    (IntPtr)(((int)at.Y << 16) | ((int)at.X & 0xFFFF)));
                Pump(80);
            }
            finally { System.Windows.Forms.Cursor.Position = previous; }
        }



        static void CollectResources()
        {
            GC.Collect();
            // WPF finalizers may marshal cleanup to the dispatcher; never block that thread waiting for them.
            var finalizers = System.Threading.Tasks.Task.Run(() => GC.WaitForPendingFinalizers());
            var timeout = Stopwatch.StartNew();
            while (!finalizers.IsCompleted)
            {
                if (timeout.ElapsedMilliseconds > 10000) throw new Exception("WPF finalizer cleanup exceeded 10 seconds");
                Pump(10);
            }
            finalizers.GetAwaiter().GetResult(); GC.Collect(); Pump(100);
        }

        static void RenderGallery()
        {
            var page = new StackPanel { Margin = new Thickness(Design.PageEdge) };
            page.Children.Add(new TextBlock { Text = "Shared controls", FontSize = Design.Title, FontWeight = FontWeights.SemiBold });
            var buttons = Design.Block("Buttons");
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var key in new[] { "PrimaryButton", "", "TextButton", "DangerButton" })
            {
                var button = new Button { Content = key == "" ? "Secondary" : key.Replace("Button", ""), MinWidth = Design.ButtonMin, Margin = new Thickness(0, 0, Design.Gap, 0) };
                if (key != "") button.Style = (Style)Application.Current.FindResource(key);
                row.Children.Add(button);
            }
            buttons.Children.Add(row); page.Children.Add(buttons);
            var form = Design.Block("Fields and switches");
            var fields = new StackPanel { Orientation = Orientation.Horizontal };
            fields.Children.Add(new TextBox { Text = "600", Width = Design.InputWidth, Margin = new Thickness(0, 0, Design.FieldPairGap, 0) });
            fields.Children.Add(new TextBox { Tag = "Placeholder", Width = Design.MainActionWidth, Margin = new Thickness(0, 0, Design.FieldPairGap, 0) });
            fields.Children.Add(new TextBox { Text = "Disabled", IsEnabled = false, Width = Design.InputWidth }); form.Children.Add(fields);
            var toggles = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, Design.BlockPadding, 0, 0) };
            toggles.Children.Add(new ToggleButton { Margin = new Thickness(0, 0, Design.FieldPairGap, 0) });
            toggles.Children.Add(new ToggleButton { IsChecked = true, Margin = new Thickness(0, 0, Design.FieldPairGap, 0) });
            toggles.Children.Add(new ToggleButton { IsEnabled = false }); form.Children.Add(toggles); page.Children.Add(form);
            var menu = Design.Block("Dropdown and menu rows");
            var dropdown = new Button { Content = "Shut down", Width = Design.MainActionWidth, HorizontalAlignment = HorizontalAlignment.Left, Style = (Style)Application.Current.FindResource("DropdownButton") };
            menu.Children.Add(dropdown);
            foreach (var text in new[] { "Current item", "Another item", "Disabled item" })
                menu.Children.Add(new Button { Content = text, IsEnabled = text != "Disabled item", HorizontalContentAlignment = HorizontalAlignment.Left, Style = (Style)Application.Current.FindResource("MenuButton") });
            page.Children.Add(menu);
            var tabs = new ModeSelector("Overview", "Details"); page.Children.Add(tabs);
            var window = new Window { Content = page, Width = 580, SizeToContent = SizeToContent.Height, Background = Theme.Brush(Theme.Bg), Foreground = Theme.Brush(Theme.Text), ShowInTaskbar = false };
            TextOptions.SetTextFormattingMode(page, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(page, TextRenderingMode.ClearType);
            window.Show(); Pump(250); CaptureControl(page, "wpf-gallery"); window.Close();
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
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    Theme.Init(app);
                    Console.WriteLine("CHECKPOINT: opening main window");
                    var window = Open();
                    Console.WriteLine("CHECKPOINT: native wheel and behavior");
                    try
                    {
                        var countdown = Field<Countdown>(window, "countdown");
                        var face = Field<TimerFace>(window, "face");
                        WheelAt(window, face, new System.Windows.Point(90, 180));
                        Check(countdown.Duration == 2100, "Wheel over blank dial interior did not adjust time");
                        WheelAt(window, face, new System.Windows.Point(160, 186));
                        Check(countdown.Duration == 2400, "Wheel over the action selector did not adjust time");
                        countdown.SetDuration(1800); Call(window, "UpdateView", false);
                        Call(window, "SetAction", "sleep");
                        var force = Field<ToggleButton>(window, "forceSwitch");
                        Check(force.IsEnabled, "Force-close setting cannot be switched off while Sleep is selected");
                        force.IsChecked = false; force.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        Check(!Settings.Get("force", true), "Force-close setting did not save Off");
                        Call(window, "SetAction", "shutdown");
                        Check(window.Title == "30:00 · Ready", "Window title is missing current timer/status");
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
                        circleInput.Text = "120"; Call(window, "CommitAnimationInput", circleInput, true);
                        textInput.Text = "900"; Call(window, "CommitAnimationInput", textInput, false);
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
                        circleInput.Text = "900"; Call(window, "CommitAnimationInput", circleInput, true);
                        textInput.Text = "120"; Call(window, "CommitAnimationInput", textInput, false);
                        face.Snap(1800); face.UpdatePosition(3600, true, false); Pump(250);
                        Check(face.Position > 1800 && face.Position < 3600, "Slow circle followed the fast text");
                        Check(Math.Abs(face.CounterPosition - 3600) < 1 && face.DisplayTime == face.Time, "Fast counter did not complete independently");
                        Pump(750);
                        foreach (string bad in new[] { "-1", "1.5", "10001", "bad", "" })
                        {
                            circleInput.Text = bad; Call(window, "CommitAnimationInput", circleInput, true);
                            Check(face.CircleMilliseconds == 900 && Settings.Get("circle_sweep_ms", -1) == 900, "Invalid edit replaced the saved timing");
                        }
                        circleInput.Text = "0"; Call(window, "CommitAnimationInput", circleInput, true);
                        textInput.Text = "0"; Call(window, "CommitAnimationInput", textInput, false);
                        face.Snap(1800); face.UpdatePosition(3600, true, false);
                        Check(face.Position == 3600 && face.CounterPosition == 3600, "Zero duration did not apply instantly");
                        circleInput.Text = "600"; Call(window, "CommitAnimationInput", circleInput, true);
                        textInput.Text = "600"; Call(window, "CommitAnimationInput", textInput, false);

                        Call(window, "SetMode", true);
                        Check(Field<bool>(window, "clockMode"), "At time mode failed");
                        Check(Field<TextBox>(window, "clockField").Visibility == Visibility.Visible, "Clock field stayed hidden");
                        Check(face.ClockMode, "Clock display was not preserved");
                        Call(window, "SetMode", false);
                        Call(window, "StartQuickTimer", 150);
                        countdown.Pause(); Call(window, "UpdateView", false);
                        Check(window.Title.EndsWith(" · Paused"), "Paused title status is missing");
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

                    Console.WriteLine("CHECKPOINT: page previews");
                    var visual = Open();
                    try
                    {
                        var countdown = Field<Countdown>(visual, "countdown");
                        countdown.SetDuration(10800); Call(visual, "UpdateView", false); Pump(150);
                        Check(((FrameworkElement)visual.Content).ActualHeight >= 508, "Client area clips the footer");
                        Capture(visual, "wpf-main");
                        Check(((Grid)visual.Content).ColumnDefinitions[0].Width.Value == Design.Sidebar, "Design-system navigation is missing");
                        Check(Math.Abs(Field<TranslateTransform>(visual, "modeSlide").X - (Design.DialWidth - Design.ModeWidth) / 2) < 1, "Duration selector is not centered");
                        Call(visual, "SetMode", true); Pump(400);
                        var clockBox = Field<TextBox>(visual, "clockField");
                        var clockHost = (ScrollViewer)clockBox.Template.FindName("PART_ContentHost", clockBox);
                        Check(clockBox.Text.Contains(":") && clockHost.ViewportHeight + 1 >= clockHost.ExtentHeight, "Clock text is vertically clipped");
                        Check(Math.Abs(Field<TranslateTransform>(visual, "modeSlide").X) < 1, "At time selector did not slide left");
                        Capture(visual, "wpf-clock");
                        Call(visual, "SetMode", false); Pump(400);
                        var actionPopup = Field<PopupMenu>(visual, "actionMenu");
                        actionPopup.Show(PowerAction.All.Select(p => new MenuRow { Text = p.Label }), Field<Button>(visual, "picker"));
                        Pump(200); CaptureControl((FrameworkElement)Field<Popup>(actionPopup, "popup").Child, "wpf-action-selector");
                        actionPopup.Close(); Pump(200);
                        Call(visual, "ToggleSettings"); Pump(300);
                        var toggle = Field<ToggleButton>(visual, "forceSwitch");
                        var thumb = (Border)toggle.Template.FindName("thumb", toggle);
                        Check(((SolidColorBrush)thumb.Background).Color == Colors.White && thumb.Width == Design.ThumbWidth && thumb.Height == Design.ThumbHeight, "Original switch capsule was changed");
                        var viewer = Field<Grid>(visual, "settings").Children.OfType<ScrollViewer>().Single();
                        viewer.ScrollToVerticalOffset(viewer.ScrollableHeight); Pump(80);
                        Check(viewer.VerticalOffset >= viewer.ScrollableHeight - 1, "Settings scrollbar did not follow content");
                        viewer.ScrollToHome(); Pump(80);
                        Capture(visual, "wpf-settings");
                        Call(visual, "SelectTab", 1); Pump(300);
                        var accentBox = Field<TextBox>(visual, "accentField");
                        var accentHost = (ScrollViewer)accentBox.Template.FindName("PART_ContentHost", accentBox);
                        Check(accentHost.ViewportHeight + 1 >= accentHost.ExtentHeight, "Hex field text is vertically clipped");
                        Check(accentHost.ViewportWidth + 1 >= accentHost.ExtentWidth, "Hex field text is horizontally clipped");
                        var circleBox = Field<TextBox>(visual, "circleDuration");
                        var counterBox = Field<TextBox>(visual, "counterDuration");
                        Check(Math.Abs(circleBox.TranslatePoint(new System.Windows.Point(0, 0), visual).Y - counterBox.TranslatePoint(new System.Windows.Point(0, 0), visual).Y) < 1,
                            "Animation duration fields are not aligned");
                        Check(circleBox.ActualHeight == Design.Field && counterBox.ActualHeight == Design.Field, "Duration fields do not use shared control height");
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
                        awake.Show((System.Collections.Generic.List<MenuRow>)Call(visual, "ChipRows"), anchor);
                        Pump(200); CaptureControl((FrameworkElement)Field<Popup>(awake, "popup").Child, "wpf-awake-menu"); awake.Close(); Pump(200);
                        var trayPopup = Field<PopupMenu>(visual, "trayMenu");
                        Set(visual, "trayPage", 0);
                        trayPopup.Show((System.Collections.Generic.List<MenuRow>)Call(visual, "TrayRows"));
                        Pump(200); CaptureControl((FrameworkElement)Field<Popup>(trayPopup, "popup").Child, "wpf-tray-menu"); trayPopup.Close(); Pump(200);
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
                    Console.WriteLine("CHECKPOINT: resource disposal");
                    for (int i = 0; i < 3; i++) { var warm = Open(); Pump(30); Close(warm); }
                    CollectResources();
                    int baseline = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                    for (int i = 0; i < 20; i++)
                    {
                        var repeat = Open();
                        Call(repeat, "AddTime", 300); Call(repeat, "ToggleSettings");
                        Pump(20); Close(repeat);
                    }
                    CollectResources();
                    int final = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                    Check(final - baseline < 32, "GDI handle growth: " + baseline + " -> " + final);
                    Console.WriteLine("CHECKPOINT: component gallery");
                    RenderGallery();
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
