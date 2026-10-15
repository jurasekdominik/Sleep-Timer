using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace SleepTimer
{
    sealed class MainWindow : Window, IDisposable
    {
        readonly Stopwatch clock = Stopwatch.StartNew(), wheelSave = Stopwatch.StartNew();
        readonly Countdown countdown;
        readonly HourStop hourStop = new HourStop();
        readonly QuickTimers quickTimers = new QuickTimers(Settings.Get("quick_timers", ""));
        readonly Forms.NotifyIcon tray = new Forms.NotifyIcon();
        readonly DispatcherTimer ticker = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        DispatcherTimer aborted;
        readonly TimerFace face = new TimerFace();
        readonly Canvas main = new Canvas(), settings = new Canvas();
        readonly Grid root = new Grid();
        readonly Button gear = new Button { Content = new GearMark(), Padding = new Thickness(0), Width = 30, Height = 30, ToolTip = "Settings" };
        readonly Button fontButton = new Button { Content = new TextBlock { Text = "Aa", FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.SemiBold, FontSize = 14 },
            Padding = new Thickness(0), Width = 30, Height = 30, ToolTip = "Fonts", Visibility = Visibility.Collapsed };
        readonly PopupMenu fontMenu = new PopupMenu(160);
        readonly ActionSelector picker = new ActionSelector();
        readonly ModeSelector modeSelector = new ModeSelector();
        readonly TextBox clockField = new TextBox { MaxLength = 5, Width = 101.333333, Height = 32, TextAlignment = TextAlignment.Center, ToolTip = "24-hour time, HH:mm. Scroll hours or minutes; arrow keys adjust." };
        readonly Button awakeChip = new Button { Width = 160, Height = 32 };
        readonly ToggleButton awakeSwitch = new ToggleButton();
        readonly ToggleButton startupSwitch = new ToggleButton { IsChecked = Settings.Get("start_with_windows", false) };
        readonly ToggleButton forceSwitch = new ToggleButton { IsChecked = Settings.Get("force", true) };
        readonly TextBox accentField = new TextBox { MaxLength = 7, Width = 96, FontSize = 13, Padding = new Thickness(0), TextAlignment = TextAlignment.Center };
        readonly TextBox circleDuration = new TextBox { MaxLength = 5, TextAlignment = TextAlignment.Right };
        readonly TextBox counterDuration = new TextBox { MaxLength = 5, TextAlignment = TextAlignment.Right };
        readonly TextBox hourStopDuration = new TextBox { MaxLength = 5, TextAlignment = TextAlignment.Right };
        readonly TextBlock hourStopHelp = new TextBlock { FontSize = 12, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock animationNote = new TextBlock { FontSize = 12, Foreground = Theme.Brush(Theme.Faint) };
        readonly TextBlock accentNote = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        readonly TextBox quickMinutes = new TextBox { MaxLength = 32, Width = 184, Tag = "Minutes or hours", ToolTip = "Whole minutes (60, 90) or whole hours (2h, 5h). Up to 24 hours." };
        readonly TextBlock quickNote = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        readonly StackPanel customRows = new StackPanel();
        readonly ScrollViewer customScroll = new ThemedScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Border quickCard;
        readonly PopupMenu trayMenu = new PopupMenu(), awakeMenu = new PopupMenu(160);
        readonly List<FrameworkElement> cards = new List<FrameworkElement>();
        readonly List<Button> tabButtons = new List<Button>();
        readonly List<Button> swatches = new List<Button>();
        readonly Border tabUnderline = new Border { Height = 2.5 };
        readonly TranslateTransform tabSlide = new TranslateTransform();
        readonly List<double> tabPositions = new List<double>(), tabWidths = new List<double>();
        readonly TranslateTransform modeSlide = new TranslateTransform();
        Canvas mode;
        ColorPicker colorPicker;
        Window fontGallery;
        PowerAction action, quickAction;
        int clockMinute, awakeMinutes, lastQuickMinutes, trayPage, selectedTab;
        double awakeEnd;
        bool clockMode, clockTouched, settingsOpen, syncing, durationDirty, warned, executing, quitting, disposed, sessionEnding;
        bool wheelViewPending;
        string iconKey;
        IntPtr trayHandle;
        HwndSource source;
        const int ShortcutId = 0x1230;
        static readonly string[] ShortcutNames = { "hotkey_toggle", "hotkey_quick" };
        readonly Shortcut[] shortcuts = new Shortcut[2];
        readonly bool[] shortcutRegistered = new bool[2];
        readonly TextBox[] shortcutFields = new TextBox[2];
        readonly TextBlock shortcutNote = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        int recordingShortcut = -1;
        Point trayClick;
        public bool TestMode { get; set; }

        public MainWindow()
        {
            countdown = new Countdown(Settings.Get("duration", Countdown.DefaultSeconds), () => clock.Elapsed.TotalSeconds);
            face.CircleMilliseconds = AnimationTiming.Normalize(Settings.Get("circle_sweep_ms", AnimationTiming.DefaultMilliseconds));
            face.CounterMilliseconds = AnimationTiming.Normalize(Settings.Get("counter_sweep_ms", AnimationTiming.DefaultMilliseconds));
            int hold = Settings.Get("hour_stop_ms", 400);
            hourStop.HoldMilliseconds = hold >= 0 && hold <= AnimationTiming.MaxMilliseconds ? hold : 400;
            clockMode = Settings.Get("clock_mode", false);
            clockMinute = TimeText.DefaultClock(DateTimeOffset.Now, countdown.Duration, TimeZoneInfo.Local);
            action = PowerAction.Find(Settings.Get("action", "shutdown"));
            quickAction = PowerAction.Find(Settings.Get("quick_action", action.Key));
            lastQuickMinutes = Settings.Get("quick_timer_last", 30);
            awakeMinutes = Settings.Get("awake", false) ? -1 : 0;
            screenOn = Settings.Get("awake_screen", false);
            if (startupSwitch.IsChecked == true) Startup.Set(true);
            Title = "Sleep Timer";
            TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo();
            SizeToContent = SizeToContent.WidthAndHeight;
            root.Width = 368; root.Height = 508; root.Background = Theme.Brush(Theme.Bg);
            ResizeMode = ResizeMode.CanMinimize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Theme.Brush(Theme.Bg); Content = root;
            SetResourceReference(FontFamilyProperty, "UiFontFamily"); SetResourceReference(FontWeightProperty, "UiFontWeight"); FontSize = 14; Foreground = Theme.Brush(Theme.Text);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/assets/SleepTimer.ico"));
            main.Width = settings.Width = 368; main.Height = settings.Height = 508;
            main.HorizontalAlignment = settings.HorizontalAlignment = HorizontalAlignment.Center;
            main.VerticalAlignment = settings.VerticalAlignment = VerticalAlignment.Top;
            root.Children.Add(main); root.Children.Add(settings);
            settings.Visibility = Visibility.Collapsed;
            BuildMain(); BuildSettings();
            gear.HorizontalAlignment = HorizontalAlignment.Right; gear.VerticalAlignment = VerticalAlignment.Top; gear.Margin = new Thickness(0, 12, 24, 0);
            gear.Style = (Style)FindResource("IconButton");
            root.Children.Add(gear); gear.Click += (s, e) => ToggleSettings();
            fontButton.HorizontalAlignment = HorizontalAlignment.Right; fontButton.VerticalAlignment = VerticalAlignment.Top;
            fontButton.Margin = new Thickness(0, 12, 60, 0); fontButton.Style = (Style)FindResource("IconButton");
            root.Children.Add(fontButton); fontButton.Click += (s, e) => ShowFontMenu();
            face.Toggle += StartPause; face.Cancel += Cancel; face.Wheel += AddTime;
            main.PreviewMouseWheel += (s, e) =>
            {
                if (!face.ContainsDial(e.GetPosition(face))) return;
                face.Scroll(e.Delta, Keyboard.Modifiers);
                e.Handled = true;
            };
            main.PreviewMouseDown += (s, e) =>
            {
                if (!face.ContainsDial(e.GetPosition(face))) return;
                // The action selector keeps its left-click interaction; the rest of the dial controls the timer.
                if (e.ChangedButton == MouseButton.Left && picker.IsEnabled && picker.ContainsAction(e.GetPosition(picker))) return;
                if (e.ChangedButton == MouseButton.Left) StartPause();
                else if (e.ChangedButton == MouseButton.Right) Cancel();
                else return;
                e.Handled = true;
            };
            tray.MouseUp += (s, e) =>
            {
                if (e.Button != Forms.MouseButtons.Right) return;
                Native.GetPhysicalCursorPos(out var point);
                trayClick = new Point(point.X, point.Y);
                Dispatcher.BeginInvoke((Action)ShowTrayMenu);
            };
            tray.MouseDoubleClick += (s, e) => { if (e.Button == Forms.MouseButtons.Left) Dispatcher.BeginInvoke((Action)ShowWindow); };
            tray.BalloonTipClicked += (s, e) => Dispatcher.BeginInvoke((Action)ShowWindow);
            ticker.Tick += (s, e) => Tick();
            SourceInitialized += (s, e) =>
            {
                source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                source.AddHook(WindowMessage);
                Native.DarkTitlebar(source.Handle);
                RegisterShortcuts();
            };
            StateChanged += (s, e) => { if (WindowState == WindowState.Minimized) Hide(); };
            Closing += (s, e) =>
            {
                if (!quitting) { e.Cancel = true; Hide(); return; }
                if (!TestMode && !sessionEnding && countdown.State != TimerState.Idle && !ConfirmQuit()) { quitting = false; e.Cancel = true; return; }
                Dispose();
                if (!TestMode) Application.Current.Shutdown();
            };
            PreviewKeyDown += (s, e) =>
            {
                if (recordingShortcut >= 0 || e.Key != Key.Escape) return;
                if (colorPicker != null && colorPicker.IsVisible) { colorPicker.Close(); e.Handled = true; }
                else if (settingsOpen) { ToggleSettings(); e.Handled = true; }
            };
            PreviewMouseDown += (s, e) =>
            {
                if (!(e.OriginalSource is TextBox) && Keyboard.FocusedElement is TextBox box
                    && !IsDescendant(box, e.OriginalSource as DependencyObject)) Keyboard.ClearFocus();
            };
            new WindowInteropHelper(this).EnsureHandle();
            SyncMode(false); ApplyAwake(); UpdateView();
            ticker.Start();
        }

        static bool IsDescendant(DependencyObject parent, DependencyObject child)
        {
            while (child != null) { if (child == parent) return true; child = child is Visual ? VisualTreeHelper.GetParent(child) : null; }
            return false;
        }
        static void Place(Canvas canvas, UIElement item, double x, double y, double width = double.NaN, double height = double.NaN)
        {
            if (item is FrameworkElement f) { if (!double.IsNaN(width)) f.Width = width; if (!double.IsNaN(height)) f.Height = height; }
            Canvas.SetLeft(item, x); Canvas.SetTop(item, y); canvas.Children.Add(item);
        }
        static TextBlock Label(string text, double size = 14, bool secondary = false) =>
            new TextBlock { Text = text, FontSize = size, Foreground = Theme.Brush(secondary ? Theme.Second : Theme.Text), VerticalAlignment = VerticalAlignment.Center };

        void BuildMain()
        {
            Place(main, face, 24, 12);
            Place(main, picker, 44, 182);
            picker.Changed += SetAction;
            mode = new Canvas { Width = 320, Height = 32 };
            modeSelector.RenderTransform = modeSlide;
            Place(mode, modeSelector, 0, 0);
            Place(mode, clockField, 218.6666667, 0, 101.3333333, 32);
            Place(main, mode, 24, 356);
            modeSelector.Changed += SetMode;
            clockField.LostKeyboardFocus += (s, e) => CommitClock();
            clockField.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { CommitClock(); Keyboard.ClearFocus(); e.Handled = true; }
                else if (e.Key == Key.Up || e.Key == Key.Down)
                {
                    EditClock((e.Key == Key.Up ? 1 : -1) * (clockField.CaretIndex < 3 ? 60 : 1)); e.Handled = true;
                }
            };
            clockField.PreviewMouseWheel += (s, e) =>
            {
                if (!clockField.IsEnabled) return;
                bool hours = e.GetPosition(clockField).X < clockField.ActualWidth / 2;
                EditClock(Math.Sign(e.Delta) * (hours ? 60 : 1)); e.Handled = true;
            };
            var awakeLabel = Label("Keep awake");
            awakeLabel.Cursor = Cursors.Hand; awakeLabel.MouseLeftButtonUp += (s, e) => SetAwake(awakeMinutes == 0 ? -1 : 0);
            Place(main, awakeLabel, 24, 410, 96, 24);
            awakeChip.Style = (Style)FindResource("ChipButton");
            Place(main, awakeChip, 128, 404);
            Place(main, awakeSwitch, 300, 408);
            awakeChip.Click += (s, e) => { if (!awakeMenu.Visible && !awakeMenu.JustClosed) awakeMenu.Show(ChipRows(), awakeChip); };
            awakeSwitch.Click += (s, e) => { if (!syncing) SetAwake(awakeSwitch.IsChecked == true ? -1 : 0); };
            awakeSwitch.ToolTip = "Keep the PC awake until you turn this off";
            AddFooter(main);
        }

        void AddFooter(Canvas parent)
        {
            Place(parent, new Border { Background = Theme.Brush(Theme.Border) }, 24, 452, 320, 1);
            Place(parent, new BrandFooter(), 24, 464, 320, 28);
        }

        Border Card(int tab, double y, double height)
        {
            var card = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Child = new Canvas(), Tag = tab };
            card.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            Place(settings, card, 24, y, 320, height); cards.Add(card); return card;
        }
        void BuildSettings()
        {
            Place(settings, new TextBlock { Text = "Settings", FontSize = 15, FontWeight = FontWeights.SemiBold }, 24, 20, 200, 24);
            var tabs = new Canvas { Width = 320, Height = 24 };
            string[] names = { "GENERAL", "APPEARANCE", "TIMERS", "KEYBINDS" };
            double tabX = 0;
            for (int i = 0; i < names.Length; i++)
            {
                int tab = i;
                string spaced = names[i];
                double width = VisualText.Make(this, spaced, 12, Theme.Text, true).WidthIncludingTrailingWhitespace;
                tabPositions.Add(tabX); tabWidths.Add(width);
                var b = new Button { Content = spaced, FontSize = 12, FontWeight = FontWeights.SemiBold, Width = width + 4, Height = 17,
                    Style = (Style)FindResource("TextButton"), HorizontalContentAlignment = HorizontalAlignment.Left };
                b.Click += (s, e) => SelectTab(tab); tabButtons.Add(b); Place(tabs, b, tabX, 0);
                tabX += width + 15;
            }
            tabUnderline.RenderTransform = tabSlide;
            tabUnderline.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
            Place(tabs, tabUnderline, 0, 18, tabWidths[0], 2.5);
            Place(settings, tabs, 24, 56, 320, 24);
            var general = (Canvas)Card(0, 88, 128).Child;
            SwitchRow(general, "Start with Windows", "Opens in the tray when you sign in", startupSwitch, 0);
            SwitchRow(general, "Force close apps", "Skips “save changes?” prompts (/f)", forceSwitch, 64);
            startupSwitch.Click += (s, e) => { bool on = startupSwitch.IsChecked == true; Startup.Set(on); Settings.Set("start_with_windows", on); };
            forceSwitch.Click += (s, e) => Settings.Set("force", forceSwitch.IsChecked == true);
            var help = (Canvas)Card(0, 232, 184).Child;
            string[,] rows = { { "Left-click the ring", "Start · Pause" }, { "Right-click the ring", "Cancel" }, { "Scroll", "±5 min" }, { "Alt + scroll", "±1 min" },
                { "Shift + scroll", "±15 min" }, { "Ctrl + scroll", "±30 min" }, { "Ctrl+Shift + scroll", "±60 min" }, { "Whole-hour stop", "400 ms" } };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                Place(help, Label(rows[i, 0], 12, true), 20, 12 + i * 20, 160, 20);
                var right = i == rows.GetLength(0) - 1 ? hourStopHelp : Label(rows[i, 1], 12); right.TextAlignment = TextAlignment.Right;
                if (right == hourStopHelp) right.Text = hourStop.HoldMilliseconds + " ms";
                Place(help, right, 180, 12 + i * 20, 120, 20);
            }
            var appearance = (Canvas)Card(1, 88, 160).Child;
            Place(appearance, Label("Accent colour"), 20, 16, 180, 24);
            Place(appearance, accentNote, 20, 38, 280, 18);
            string[] colors = { "#0CD29D", "#BADC58", "#FFB142", "#FF793F", "#FD79A8", "#8C7AE6", "#A29BFE", "#34ACE0" };
            for (int i = 0; i < colors.Length; i++)
            {
                string hex = colors[i]; Color color = Theme.Hex(hex);
                var dot = new System.Windows.Shapes.Ellipse { Fill = Theme.Brush(color), Width = 28, Height = 28 };
                var b = new Button { Content = dot, Padding = new Thickness(3), Width = 36, Height = 36, Background = Brushes.Transparent, ToolTip = hex, Tag = hex, Style = (Style)FindResource("SwatchButton") };
                b.Click += (s, e) => ApplyAccent(color); swatches.Add(b);
                Place(appearance, b, 14 + i % 4 * 40, 58 + i / 4 * 40);
            }
            Place(appearance, Label("Custom", 12, true), 204, 70, 96, 16);
            Place(appearance, accentField, 204, 104, 96, 32);
            // Let the TextBox receive its normal focus/caret click, then open after the release.
            accentField.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) =>
                Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)OpenColorHelper)), true);
            accentField.Text = "#" + Theme.Accent.R.ToString("X2") + Theme.Accent.G.ToString("X2") + Theme.Accent.B.ToString("X2");
            accentField.TextChanged += (s, e) =>
            {
                if (syncing) return;
                string hex = accentField.Text.Trim();
                if (!hex.StartsWith("#")) hex = "#" + hex;
                if (System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9A-Fa-f]{6}$")) ApplyAccent(Theme.Hex(hex), true);
            };
            var animationCard = (Canvas)Card(1, 264, 176).Child;
            Place(animationCard, Label("Animation duration"), 20, 12, 280, 24);
            AddAnimationInput(animationCard, "Circle sweep", circleDuration, 40, 0);
            AddAnimationInput(animationCard, "Numerical counter", counterDuration, 76, 1);
            AddAnimationInput(animationCard, "Whole-hour stop", hourStopDuration, 112, 2);
            animationNote.Text = "Enter to save · smaller = faster · 0 = instant";
            Place(animationCard, animationNote, 20, 148, 280, 18);
            quickCard = Card(2, 88, 224);
            var timerCard = (Canvas)quickCard.Child;
            Place(timerCard, Label("Quick timers"), 16, 16, 288, 24);
            Place(timerCard, Label("Start from the tray using the selected action.", 12, true), 16, 48, 288, 20);
            Place(timerCard, Label("Built in: 30 min, 1 h, 2 h, 4 h", 12, true), 16, 72, 288, 20);
            Place(timerCard, quickMinutes, 16, 104, 184, 32);
            var add = new Button { Content = "Add timer", Width = 96, Height = 32, Padding = new Thickness(4), Style = (Style)FindResource("PrimaryButton") };
            Place(timerCard, add, 208, 104);
            add.Click += (s, e) => AddQuickTimer();
            quickMinutes.KeyDown += (s, e) => { if (e.Key == Key.Enter) { AddQuickTimer(); e.Handled = true; } };
            quickNote.Text = "1–1440 minutes or 1h–24h · e.g. 90, 2h, 5h"; quickNote.SetResourceReference(TextBlock.ForegroundProperty, "FaintBrush");
            Place(timerCard, quickNote, 16, 144, 288, 20);
            customScroll.Content = customRows;
            Place(timerCard, customScroll, 16, 176, 288, 32);
            BuildShortcutSettings(); LayoutTabs();
            SelectTab(0); RefreshQuickTimers(); AccentNote(); AddFooter(settings);
        }
        void BuildShortcutSettings()
        {
            var panel = (Canvas)Card(3, 88, 252).Child;
            Place(panel, Label("Global keybinds"), 20, 16, 280, 24);
            Place(panel, Label("Work even when the app is in the tray.", 12, true), 20, 44, 280, 20);
            string[] labels = { "Start / Cancel", "Start last used quick timer" };
            for (int i = 0; i < shortcuts.Length; i++)
            {
                int index = i;
                Shortcut.TryCreate(Settings.Get(ShortcutNames[i] + "_modifiers", 0), Settings.Get(ShortcutNames[i] + "_key", 0), out shortcuts[i]);
                var input = new TextBox { Width = 280, Height = 32, IsReadOnly = true, IsReadOnlyCaretVisible = false,
                    Text = ShortcutText(shortcuts[i]), ToolTip = "Click, then press a key with Ctrl, Alt or Win. Delete to unbind; Escape to keep the current binding." };
                shortcutFields[i] = input;
                Place(panel, Label(labels[i], 12, true), 20, 76 + i * 68, 280, 20);
                Place(panel, input, 20, 100 + i * 68);
                input.GotKeyboardFocus += (s, e) =>
                {
                    recordingShortcut = index; UnregisterShortcuts();
                    input.Text = "Press a key combination…";
                };
                input.LostKeyboardFocus += (s, e) =>
                {
                    recordingShortcut = -1; input.Text = ShortcutText(shortcuts[index]); RegisterShortcuts();
                };
                input.PreviewKeyDown += (s, e) => CaptureShortcut(index, e);
            }
            shortcutNote.Text = "Click a field to bind · Delete to clear";
            shortcutNote.SetResourceReference(TextBlock.ForegroundProperty, "FaintBrush");
            Place(panel, shortcutNote, 20, 208, 280, 40);
        }
        static string ShortcutText(Shortcut shortcut)
        {
            if (!shortcut.IsBound) return "Unbound";
            string text = "";
            if ((shortcut.Modifiers & 2) != 0) text += "Ctrl + ";
            if ((shortcut.Modifiers & 1) != 0) text += "Alt + ";
            if ((shortcut.Modifiers & 4) != 0) text += "Shift + ";
            if ((shortcut.Modifiers & 8) != 0) text += "Win + ";
            return text + new KeyConverter().ConvertToString(KeyInterop.KeyFromVirtualKey(shortcut.Key));
        }
        void ShortcutError(string text)
        {
            shortcutNote.Text = text;
            shortcutNote.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
        }
        void CaptureShortcut(int index, KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Tab) return;
            e.Handled = true;
            if (e.IsRepeat) return;
            if (key == Key.Escape) { Keyboard.ClearFocus(); return; }
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt
                || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
            Shortcut binding = default;
            if (key != Key.Delete && key != Key.Back)
            {
                var keys = Keyboard.Modifiers;
                int modifiers = ((keys & ModifierKeys.Alt) != 0 ? 1 : 0) | ((keys & ModifierKeys.Control) != 0 ? 2 : 0)
                    | ((keys & ModifierKeys.Shift) != 0 ? 4 : 0) | ((keys & ModifierKeys.Windows) != 0 ? 8 : 0);
                if (!Shortcut.TryCreate(modifiers, KeyInterop.VirtualKeyFromKey(key), out binding))
                { ShortcutError("Use a key with Ctrl, Alt or Win."); return; }
            }
            if (SetShortcut(index, binding)) Keyboard.ClearFocus();
        }
        bool SetShortcut(int index, Shortcut binding)
        {
            if (binding.Matches(shortcuts[1 - index]))
            { ShortcutError("The other action already uses that keybind."); return false; }
            var previous = shortcuts[index];
            UnregisterShortcuts(); shortcuts[index] = binding; RegisterShortcuts();
            if (binding.IsBound && !shortcutRegistered[index])
            {
                string error = shortcutNote.Text;
                UnregisterShortcuts(); shortcuts[index] = previous; RegisterShortcuts();
                ShortcutError(error);
                if (recordingShortcut >= 0) UnregisterShortcuts();
                return false;
            }
            Settings.Set(ShortcutNames[index] + "_key", binding.Key);
            Settings.Set(ShortcutNames[index] + "_modifiers", binding.Modifiers);
            shortcutFields[index].Text = ShortcutText(binding);
            if (recordingShortcut >= 0) UnregisterShortcuts();
            return true;
        }
        void RegisterShortcuts()
        {
            if (source == null || disposed) return;
            shortcutNote.Text = "Click a field to bind · Delete to clear";
            shortcutNote.SetResourceReference(TextBlock.ForegroundProperty, "FaintBrush");
            for (int i = 0; i < shortcuts.Length; i++)
            {
                if (!shortcuts[i].IsBound || shortcutRegistered[i]) continue;
                shortcutRegistered[i] = Native.RegisterHotKey(source.Handle, ShortcutId + i,
                    (uint)(shortcuts[i].Modifiers | 0x4000), (uint)shortcuts[i].Key); // MOD_NOREPEAT
                if (!shortcutRegistered[i]) ShortcutError(ShortcutText(shortcuts[i]) + " is unavailable. Choose another keybind.");
            }
        }
        void UnregisterShortcuts()
        {
            if (source == null) return;
            for (int i = 0; i < shortcuts.Length; i++)
            {
                if (shortcutRegistered[i]) Native.UnregisterHotKey(source.Handle, ShortcutId + i);
                shortcutRegistered[i] = false;
            }
        }
        int LastQuickMinutes => lastQuickMinutes >= 1 && lastQuickMinutes <= 1440 ? lastQuickMinutes : 30;
        void RunShortcut(int index)
        {
            if (disposed || executing || recordingShortcut >= 0) return;
            string message;
            if (index == 0 && countdown.State != TimerState.Idle)
            {
                Cancel(); message = action.Label + " timer cancelled.";
            }
            else
            {
                if (index == 0) StartPause(); else if (index == 1) StartQuickTimer(LastQuickMinutes); else return;
                message = (index == 1 ? "Quick timer started: " : "Timer started: ") + action.Label
                    + " in " + TimeText.Clock(countdown.Remaining) + ".";
            }
            tray.BalloonTipTitle = "Sleep Timer"; tray.BalloonTipText = message; tray.BalloonTipIcon = Forms.ToolTipIcon.Info;
            if (!TestMode) tray.ShowBalloonTip(5000);
        }

        int TimingValue(int kind) => kind == 0 ? face.CircleMilliseconds : kind == 1 ? face.CounterMilliseconds : hourStop.HoldMilliseconds;
        void AddAnimationInput(Canvas card, string label, TextBox input, double y, int kind)
        {
            var row = new Grid { Height = 32 };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.Children.Add(Label(label));
            input.Text = TimingValue(kind).ToString(CultureInfo.InvariantCulture);
            input.Height = 32;
            input.ToolTip = "0–10000 milliseconds. Default: " + (kind == 2 ? "400" : "600") + " ms. Press Enter or leave the field to save.";
            Grid.SetColumn(input, 1); row.Children.Add(input);
            var unit = Label("ms", 12, true); unit.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(unit, 2); row.Children.Add(unit);
            Place(card, row, 20, y, 280, 32);
            input.LostKeyboardFocus += (s, e) => CommitAnimationInput(input, kind);
            input.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { CommitAnimationInput(input, kind); e.Handled = true; }
                else if (e.Key == Key.Escape)
                {
                    input.Text = TimingValue(kind).ToString(CultureInfo.InvariantCulture);
                    CommitAnimationInput(input, kind); e.Handled = true;
                }
            };
        }
        void CommitAnimationInput(TextBox input, int kind)
        {
            if (!AnimationTiming.TryParse(input.Text.Trim(), out int milliseconds))
            {
                input.SetResourceReference(TextBox.BorderBrushProperty, "DangerBrush");
                animationNote.Text = "Use a whole number from 0 to 10000 ms.";
                animationNote.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
                return;
            }
            if (kind == 0) face.CircleMilliseconds = milliseconds;
            else if (kind == 1) face.CounterMilliseconds = milliseconds;
            else { hourStop.HoldMilliseconds = milliseconds; hourStop.Reset(); hourStopHelp.Text = milliseconds + " ms"; }
            Settings.Set(kind == 0 ? "circle_sweep_ms" : kind == 1 ? "counter_sweep_ms" : "hour_stop_ms", milliseconds);
            input.Text = milliseconds.ToString(CultureInfo.InvariantCulture);
            input.SetResourceReference(TextBox.BorderBrushProperty, "BorderBrush");
            animationNote.Text = "Enter to save · smaller = faster · 0 = instant";
            animationNote.SetResourceReference(TextBlock.ForegroundProperty, "FaintBrush");
        }

        static void SwitchRow(Canvas parent, string title, string note, ToggleButton toggle, double y)
        {
            TextBlock label = Label(title), detail = Label(note, 12, true);
            Place(parent, label, 20, y + 12, 230, 20);
            Place(parent, detail, 20, y + 34, 230, 18);
            Place(parent, toggle, 256, y + 20);
            label.Cursor = detail.Cursor = Cursors.Hand;
            MouseButtonEventHandler click = (s, e) =>
            {
                if (!toggle.IsEnabled) return;
                toggle.IsChecked = toggle.IsChecked != true;
                toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            };
            label.MouseLeftButtonUp += click; detail.MouseLeftButtonUp += click;
        }

        void LayoutTabs()
        {
            for (int i = 0; i < tabButtons.Count; i++)
                tabWidths[i] = VisualText.Make(this, (string)tabButtons[i].Content, 12, Theme.Text, true).WidthIncludingTrailingWhitespace;
            double gap = Math.Min(15, Math.Max(0, (320 - tabWidths.Sum() - 4) / Math.Max(1, tabButtons.Count - 1)));
            double x = 0;
            for (int i = 0; i < tabButtons.Count; i++)
            {
                tabPositions[i] = x; Canvas.SetLeft(tabButtons[i], x); tabButtons[i].Width = tabWidths[i] + 4;
                x += tabWidths[i] + gap;
            }
        }
        void SelectTab(int tab)
        {
            selectedTab = tab;
            for (int i = 0; i < tabButtons.Count; i++) tabButtons[i].Foreground = Theme.Brush(i == tab ? Theme.Text : Theme.Faint);
            if (tabPositions.Count > tab)
            {
                tabSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(tabSlide.X, tabPositions[tab], TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                tabUnderline.BeginAnimation(WidthProperty, new DoubleAnimation(double.IsNaN(tabUnderline.Width) ? tabWidths[0] : tabUnderline.Width, tabWidths[tab], TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
            foreach (var card in cards)
            {
                bool shown = (int)card.Tag == tab;
                card.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
                if (shown) card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
            }
            Keyboard.ClearFocus();
        }
        void ToggleSettings()
        {
            settingsOpen = !settingsOpen;
            fontButton.Visibility = settingsOpen ? Visibility.Visible : Visibility.Collapsed;
            fontMenu.Close();
            var from = settingsOpen ? main : settings;
            var to = settingsOpen ? settings : main;
            from.IsHitTestVisible = false; to.IsHitTestVisible = true; to.Visibility = Visibility.Visible;
            var hide = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
            hide.Completed += (s, e) => { if ((settingsOpen ? main : settings) == from) from.Visibility = Visibility.Collapsed; };
            from.BeginAnimation(OpacityProperty, hide);
            to.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
            ((GearMark)gear.Content).Active = settingsOpen; ((GearMark)gear.Content).InvalidateVisual();
            gear.Background = settingsOpen ? Theme.Brush(Theme.Surface) : Brushes.Transparent; Keyboard.ClearFocus();
        }

        void SetMode(bool clock)
        {
            if (countdown.State != TimerState.Idle || executing) return;
            hourStop.Reset(); clockMode = clock;
            if (clock && !clockTouched) clockMinute = TimeText.DefaultClock(DateTimeOffset.Now, countdown.Duration, TimeZoneInfo.Local);
            Settings.Set("clock_mode", clock); SyncMode(true); UpdateView();
        }
        void SyncMode(bool animate)
        {
            double x = clockMode ? 0 : 54.6666667;
            if (animate) modeSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(modeSlide.X, x, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else { modeSlide.BeginAnimation(TranslateTransform.XProperty, null); modeSlide.X = x; }
            modeSelector.Set(clockMode, animate);
            if (clockMode) clockField.Visibility = Visibility.Visible;
            if (animate)
            {
                var fade = new DoubleAnimation(clockField.Opacity, clockMode ? 1 : 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                fade.Completed += (s, e) => { if (!clockMode) clockField.Visibility = Visibility.Hidden; };
                clockField.BeginAnimation(OpacityProperty, fade);
            }
            else
            {
                clockField.BeginAnimation(OpacityProperty, null); clockField.Opacity = clockMode ? 1 : 0;
                clockField.Visibility = clockMode ? Visibility.Visible : Visibility.Hidden;
            }
            clockField.IsHitTestVisible = clockMode;
            if (!clockField.IsKeyboardFocusWithin) clockField.Text = TimeText.HourMinute(clockMinute);
        }
        void ShowFontMenu()
        {
            if (fontMenu.Visible || fontMenu.JustClosed) return;
            fontMenu.Show(new[] {
                new MenuRow { Text = "Timer font", Click = () => OpenFontGallery(false) },
                new MenuRow { Text = "UI font", Click = () => OpenFontGallery(true) }
            }, fontButton);
        }
        void OpenFontGallery(bool ui)
        {
            fontGallery?.Close();
            var gallery = new Window { Owner = this, Title = ui ? "UI font" : "Timer font", Width = 420, SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Theme.Brush(Theme.Bg), ShowInTaskbar = false, UseLayoutRounding = true, SnapsToDevicePixels = true };
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(Label(ui ? "Choose a UI font" : "Choose a timer font", 16));
            var available = Label("Installed fonts only · no downloads", 12, true);
            available.Margin = new Thickness(0, 6, 0, 0); panel.Children.Add(available);
            var weights = new PopupMenu(180);
            var weightButton = new Button { Margin = new Thickness(0, 10, 0, 4), HorizontalAlignment = HorizontalAlignment.Left };
            panel.Children.Add(weightButton);
            var choices = new List<Button>();
            Action refresh = null;
            weightButton.Click += (s, e) =>
            {
                if (weights.Visible || weights.JustClosed) return;
                var current = ui ? AppFonts.Ui : AppFonts.Timer;
                weights.Show(current.Weights.Select(weight => new MenuRow {
                    Text = AppFont.WeightName(weight), Selected = weight == current.Typeface.Weight,
                    Click = () => { ApplyFontWeight(weight.ToOpenTypeWeight(), ui); refresh(); }
                }), weightButton);
            };
            foreach (var font in AppFonts.Choices(ui))
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });
                var name = Label(font.Name, 12); name.TextTrimming = TextTrimming.CharacterEllipsis; row.Children.Add(name);
                var sample = new TextBlock { Text = ui ? "Sleep Timer" : "01:30:00", FontSize = ui ? 22 : 26, FontFamily = font.Typeface.FontFamily,
                    FontWeight = font.Typeface.Weight, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(sample, 1); row.Children.Add(sample);
                var choice = new Button { Content = row, Height = 56, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(10, 4, 10, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch, Tag = font, ToolTip = font.Name };
                choice.BorderBrush = Theme.Brush(font.Key == (ui ? AppFonts.Ui : AppFonts.Timer).Key ? Theme.Accent : Theme.Border);
                choice.Click += (s, e) =>
                {
                    ApplyFont(font.Key, ui);
                    refresh();
                };
                choices.Add(choice); panel.Children.Add(choice);
            }
            refresh = () =>
            {
                var current = ui ? AppFonts.Ui : AppFonts.Timer;
                weightButton.Content = "Weight: " + AppFont.WeightName(current.Typeface.Weight) + " ▾";
                foreach (var button in choices)
                {
                    var family = (AppFont)button.Tag;
                    button.BorderBrush = Theme.Brush(family.Key == current.Key ? Theme.Accent : Theme.Border);
                    var sample = ((Grid)button.Content).Children.OfType<TextBlock>().Last();
                    sample.FontWeight = family.WithWeight(current.Typeface.Weight.ToOpenTypeWeight()).Typeface.Weight;
                }
            };
            refresh();
            var note = Label(ui ? "Readable UI families only. Only installed weights are offered. Timer digits keep their own font." : "Changes timer digits only. Only installed weights are offered; unsupported weights are not simulated.", 12, true);
            note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(0, 12, 0, 0); panel.Children.Add(note);
            var done = new Button { Content = "Done", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            done.Click += (s, e) => gallery.Close(); panel.Children.Add(done);
            gallery.Content = new ThemedScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height - 80) };
            gallery.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { gallery.Close(); e.Handled = true; } };
            gallery.SourceInitialized += (s, e) => Native.DarkTitlebar(new WindowInteropHelper(gallery).Handle);
            gallery.Closed += (s, e) => { weights.Dispose(); fontGallery = null; };
            fontGallery = gallery; gallery.Show();
        }
        void ApplyFont(string key, bool ui)
        {
            SetFont(AppFonts.Select(key, ui, Settings.Get(ui ? "ui_font_weight" : "timer_font_weight", -1)), ui);
        }
        void ApplyFontWeight(int weight, bool ui)
        {
            SetFont(AppFonts.Select((ui ? AppFonts.Ui : AppFonts.Timer).Key, ui, weight), ui);
        }
        void SetFont(AppFont font, bool ui)
        {
            if (ui)
            {
                AppFonts.Ui = font;
                Theme.UpdateResources(Application.Current.Resources);
                LayoutTabs(); SelectTab(selectedTab);
                if (awakeMenu.Visible) awakeMenu.SetRows(ChipRows());
                if (trayMenu.Visible) trayMenu.SetRows(TrayRows());
            }
            else AppFonts.Timer = font;
            Settings.Set(ui ? "ui_font" : "timer_font", font.Key);
            Settings.Set(ui ? "ui_font_weight" : "timer_font_weight", font.Typeface.Weight.ToOpenTypeWeight());
            InvalidateTree(root);
        }

        void OpenColorHelper()
        {
            if (colorPicker != null && colorPicker.IsVisible) return;
            colorPicker = new ColorPicker(Theme.Accent);
            colorPicker.Picked += ApplyAccent; colorPicker.Show(accentField);
        }
        void CommitClock()
        {
            if (!clockField.IsEnabled) return;
            if (DateTime.TryParseExact(clockField.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime value))
            { clockMinute = value.Hour * 60 + value.Minute; clockTouched = true; UpdateView(); }
            else clockField.Text = TimeText.HourMinute(clockMinute);
        }
        void EditClock(int delta)
        {
            if (!clockField.IsEnabled) return;
            clockMinute = ((clockMinute + delta) % 1440 + 1440) % 1440; clockTouched = true;
            clockField.Text = TimeText.HourMinute(clockMinute); UpdateView(true);
        }
        void SetAction(string key)
        {
            if (countdown.State != TimerState.Idle || executing) return;
            action = PowerAction.Find(key); quickAction = action;
            Settings.Set("action", key); Settings.Set("quick_action", key); UpdateView();
        }
        void AddTime(int delta)
        {
            if (executing) return;
            double current = countdown.State == TimerState.Idle ? clockMode ? clockMinute * 60 : countdown.Duration : countdown.Remaining;
            double adjustment = hourStop.Adjust(current, delta, clock.Elapsed.TotalMilliseconds);
            if (adjustment == 0) return;
            if (countdown.State == TimerState.Idle)
            {
                if (clockMode)
                {
                    clockMinute = ((clockMinute + (int)(adjustment / 60)) % 1440 + 1440) % 1440; clockTouched = true;
                    clockField.Text = TimeText.HourMinute(clockMinute);
                }
                else { countdown.SetDuration(countdown.Duration + (int)adjustment); durationDirty = true; wheelSave.Restart(); }
            }
            else { countdown.Adjust(adjustment); if (countdown.Remaining > 60) warned = false; }
            // Every wheel event updates the model immediately; paint only the latest target per frame.
            if (!wheelViewPending)
            {
                wheelViewPending = true;
                CompositionTarget.Rendering += RenderWheelView;
            }
        }
        void RenderWheelView(object sender, EventArgs e) => RefreshView(true, true);
        void StartPause()
        {
            if (executing) return;
            hourStop.Reset(); SaveDuration();
            switch (countdown.State)
            {
                case TimerState.Running: countdown.Pause(); break;
                case TimerState.Paused: countdown.Resume(); break;
                default:
                    var now = DateTimeOffset.Now;
                    double seconds = clockMode ? (TimeText.NextOccurrence(now, clockMinute, TimeZoneInfo.Local) - now).TotalSeconds : countdown.Duration;
                    countdown.Start(seconds); warned = false; break;
            }
            ApplyAwake(); UpdateView();
        }
        void Cancel()
        {
            if (executing) return;
            hourStop.Reset(); countdown.Reset(); ApplyAwake(); UpdateView(true);
        }
        void StartQuickTimer(int minutes)
        {
            if (executing) return;
            hourStop.Reset(); durationDirty = false; countdown.Reset(); action = quickAction;
            Settings.Set("action", action.Key);
            countdown.SetDuration(minutes * 60); lastQuickMinutes = minutes;
            Settings.Set("quick_timer_last", minutes); clockMode = false;
            Settings.Set("clock_mode", false); Settings.Set("duration", countdown.Duration);
            countdown.Start(countdown.Duration); warned = false; SyncMode(false); ApplyAwake(); UpdateView(true);
        }
        void AddQuickTimer()
        {
            if (!QuickTimers.TryMinutes(quickMinutes.Text.Trim(), out int minutes))
            { quickNote.Text = "Use 1–1440 minutes or 1h–24h."; quickNote.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush"); }
            else if (!quickTimers.Add(minutes))
            { quickNote.Text = "That timer is already in the quick menu."; quickNote.SetResourceReference(TextBlock.ForegroundProperty, "SecondBrush"); }
            else
            {
                Settings.Set("quick_timers", quickTimers.Save()); quickMinutes.Clear();
                quickNote.Text = "Added " + QuickTimers.Label(minutes) + "."; quickNote.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                RefreshQuickTimers();
            }
        }
        void RefreshQuickTimers()
        {
            customRows.Children.Clear();
            int[] timers = quickTimers.Custom.ToArray();
            if (timers.Length == 0)
            {
                var empty = Label("No custom timers", 12, true); empty.Height = 32;
                customRows.Children.Add(empty);
            }
            foreach (int minutes in timers)
            {
                var row = new Grid { Height = 36 };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(Label(QuickTimers.Label(minutes)));
                var trash = new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse("M3,6 H21 M9,6 V4 Q9,3 10,3 H14 Q15,3 15,4 V6 M5,6 L6,20 Q6,21 7,21 H17 Q18,21 18,20 L19,6 M10,10 V17 M14,10 V17"),
                    Width = 14, Height = 14, Stretch = Stretch.Uniform, StrokeThickness = 1.5,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
                };
                trash.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "DangerBrush");
                var remove = new Button { Content = trash, Width = 28, Height = 28, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Center,
                    Style = (Style)FindResource("RemoveButton"),
                    ToolTip = "Remove " + QuickTimers.Label(minutes) };
                remove.Click += (s, e) =>
                {
                    quickTimers.Remove(minutes); Settings.Set("quick_timers", quickTimers.Save()); RefreshQuickTimers();
                };
                Grid.SetColumn(remove, 1); row.Children.Add(remove); customRows.Children.Add(row);
            }
            customScroll.Height = Math.Min(160, Math.Max(32, timers.Length * 36));
            quickCard.Height = 192 + customScroll.Height;
            if (trayMenu.Visible) trayMenu.SetRows(TrayRows());
        }
        void ApplyAccent(Color color) => ApplyAccent(color, false);
        void ApplyAccent(Color color, bool fromField)
        {
            Theme.Accent = color; Theme.UpdateResources(Application.Current.Resources);
            string hex = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
            Settings.Set("accent", hex);
            if (!fromField) { syncing = true; accentField.Text = hex; syncing = false; }
            AccentNote(); iconKey = null; SelectTab(selectedTab); SyncMode(false); UpdateView();
            RefreshQuickTimers();
            InvalidateTree(root);
        }
        static void InvalidateTree(DependencyObject element)
        {
            if (element is UIElement visual) visual.InvalidateVisual();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                InvalidateTree(VisualTreeHelper.GetChild(element, i));
        }
        void AccentNote()
        {
            double cr = Theme.Contrast(Theme.Accent, Theme.Bg);
            accentNote.Text = cr >= 3 ? "The ring, buttons, switches and the tray icon" : "Low contrast (" + cr.ToString("0.0", CultureInfo.InvariantCulture) + " : 1) — hard to read";
            accentNote.Foreground = Theme.Brush(cr >= 3 ? Theme.Faint : Theme.Danger);
            foreach (var b in swatches) b.BorderBrush = Theme.Brush(string.Equals((string)b.Tag, accentField.Text, StringComparison.OrdinalIgnoreCase) ? Theme.Accent : Colors.Transparent);
        }

        bool screenOn;
        static readonly int[] AwakeChoices = { 0, -1, 15, 30, 60, 120, 240 };
        static string AwakeLabel(int minutes) => minutes == 0 ? "Off" : minutes < 0 ? "Indefinitely" : QuickTimers.Label(minutes);
        List<MenuRow> AwakeRows()
        {
            var rows = AwakeChoices.Select(m => new MenuRow { Text = AwakeLabel(m), Selected = m == awakeMinutes, Click = () => SetAwake(m) }).ToList();
            rows.Add(new MenuRow { Separator = true });
            rows.Add(new MenuRow { Text = "Keep screen on", Right = screenOn ? "On" : "Off", Selected = screenOn, KeepOpen = true, Click = () =>
            { screenOn = !screenOn; Settings.Set("awake_screen", screenOn); ApplyAwake(); UpdateView(); } });
            return rows;
        }
        List<MenuRow> ChipRows()
        {
            var rows = new List<MenuRow> { new MenuRow { Text = Convert.ToString(awakeChip.Content), Header = true }, new MenuRow { Separator = true } };
            rows.AddRange(AwakeRows().Where(r => !r.Selected || r.Text == "Keep screen on"));
            return rows;
        }
        List<MenuRow> TrayRows()
        {
            if (trayPage == 3)
            {
                var rows = new List<MenuRow> { new MenuRow { Text = "‹ Timer action", KeepOpen = true, Click = () => SetTrayPage(2) }, new MenuRow { Separator = true } };
                rows.AddRange(PowerAction.All.Select(a => new MenuRow { Text = a.Label, Selected = a.Key == quickAction.Key, Enabled = !executing, KeepOpen = true, Click = () =>
                {
                    quickAction = a; Settings.Set("quick_action", a.Key);
                    if (countdown.State == TimerState.Idle) SetAction(a.Key);
                    SetTrayPage(2);
                } }));
                return rows;
            }
            if (trayPage == 2)
            {
                var rows = new List<MenuRow>
                {
                    new MenuRow { Text = "‹ Quick timers", KeepOpen = true, Click = () => SetTrayPage(0) },
                    new MenuRow { Text = "Action", Right = quickAction.Label + " ›", KeepOpen = true, Enabled = !executing, Click = () => SetTrayPage(3) },
                    new MenuRow { Separator = true },
                };
                rows.AddRange(quickTimers.All.Select(m => new MenuRow { Text = QuickTimers.Label(m), Enabled = !executing, Click = () => StartQuickTimer(m) }));
                return rows;
            }
            if (trayPage == 1)
            {
                var rows = new List<MenuRow> { new MenuRow { Text = "‹ Keep awake", KeepOpen = true, Click = () => SetTrayPage(0) }, new MenuRow { Separator = true } };
                rows.AddRange(AwakeRows()); return rows;
            }
            bool idle = countdown.State == TimerState.Idle;
            var menu = new List<MenuRow>
            {
                new MenuRow { Text = "Keep awake", Selected = awakeMinutes != 0, Right = (awakeMinutes == 0 ? "Off" : awakeMinutes < 0 ? "On" : TimeText.Clock(awakeEnd - clock.Elapsed.TotalSeconds)) + " ›", KeepOpen = true, Click = () => SetTrayPage(1) },
                new MenuRow { Text = "Quick timers", Right = quickAction.Label + " ›", KeepOpen = true, Enabled = !executing, Click = () => SetTrayPage(2) },
                new MenuRow { Separator = true },
                new MenuRow { Text = idle ? "Start timer" : "Cancel timer",
                    Right = idle ? clockMode ? "At " + TimeText.HourMinute(clockMinute) : TimeText.Clock(countdown.Duration) : null,
                    Enabled = !executing, Click = () => { if (countdown.State == TimerState.Idle) StartPause(); else Cancel(); } },
            };
            if (!idle) menu.Add(new MenuRow { Text = countdown.State == TimerState.Running ? "Pause" : "Resume", Enabled = !executing, Click = StartPause });
            else
            {
                int minutes = LastQuickMinutes;
                menu.Add(new MenuRow { Separator = true });
                menu.Add(new MenuRow { Text = "Quick start", Right = TimeText.Human(minutes * 60), Enabled = !executing, Click = () => StartQuickTimer(minutes) });
            }
            menu.Add(new MenuRow { Separator = true }); menu.Add(new MenuRow { Text = "Quit", Danger = true, Click = Quit });
            return menu;
        }
        void SetTrayPage(int page) { trayPage = page; trayMenu.SetRows(TrayRows()); }
        void ShowTrayMenu()
        {
            if (trayMenu.Visible || trayMenu.JustClosed || disposed) return;
            Native.SetForegroundWindow(new WindowInteropHelper(this).Handle);
            trayPage = 0; trayMenu.Show(TrayRows(), this, screenPosition: trayClick);
        }
        void SetAwake(int minutes)
        {
            awakeMinutes = minutes; awakeEnd = clock.Elapsed.TotalSeconds + minutes * 60;
            Settings.Set("awake", minutes < 0); ApplyAwake(); UpdateView();
        }
        void ApplyAwake() => Native.KeepAwake(awakeMinutes != 0 || countdown.State == TimerState.Running, awakeMinutes != 0 && screenOn);
        void SaveDuration()
        {
            if (!durationDirty) return; Settings.Set("duration", countdown.Duration); durationDirty = false;
        }
        void Tick()
        {
            if (disposed) return;
            if (durationDirty && wheelSave.ElapsedMilliseconds >= 250) SaveDuration();
            if (awakeMinutes > 0 && clock.Elapsed.TotalSeconds >= awakeEnd) SetAwake(0);
            if (countdown.Expired) { Fire(); return; }
            if (countdown.State == TimerState.Running && countdown.Remaining <= 60 && !warned)
            {
                warned = true;
                if (!TestMode) tray.ShowBalloonTip(10000, "Sleep Timer", action.Label + " in " + TimeText.Clock(countdown.Remaining) + ". Click to pause or cancel.", Forms.ToolTipIcon.Warning);
            }
            UpdateView();
        }
        void UpdateView(bool animate = false) => RefreshView(animate, false);
        void RefreshView(bool animate, bool dialOnly)
        {
            if (wheelViewPending)
            {
                wheelViewPending = false;
                CompositionTarget.Rendering -= RenderWheelView;
                animate = true;
            }
            if (disposed || executing) return;
            var state = countdown.State; bool idle = state == TimerState.Idle;
            double seconds = idle ? countdown.Duration : countdown.Remaining;
            string idleLabel = "";
            face.State = state; face.ClockMode = idle && clockMode;
            if (idle && clockMode)
            {
                var now = DateTimeOffset.Now;
                var target = TimeText.NextOccurrence(now, clockMinute, TimeZoneInfo.Local);
                seconds = Math.Max(0, (target - now).TotalSeconds);
                face.Title = action.ClockTitle; face.Time = TimeText.HourMinute(clockMinute);
                face.Sub = (target.Date > DateTime.Today ? "tomorrow, " : "") + "in " + TimeText.Human(seconds);
                idleLabel = "at " + face.Time;
            }
            else if (idle)
            {
                face.Title = ""; face.Time = idleLabel = TimeText.Clock(seconds);
                face.Sub = action.Would + " " + DateTimeOffset.Now.AddSeconds(seconds).ToLocalTime().ToString("HH:mm");
            }
            else
            {
                face.Title = state == TimerState.Running ? action.RunTitle : "PAUSED"; face.Time = TimeText.Clock(seconds);
                face.Sub = state == TimerState.Running ? action.At + " " + DateTimeOffset.Now.AddSeconds(seconds).ToLocalTime().ToString("HH:mm") : "Resume to continue";
            }
            // Windows controls thumbnail width. Keep the header short enough to
            // show the full time; expose action and state in the shell tooltip.
            Title = face.Time;
            TaskbarItemInfo.Description = face.Time + " · " + action.Label + " · "
                + (idle ? clockMode ? "Scheduled" : "Ready" : state == TimerState.Running ? "Running" : "Paused");
            face.Tone = TimerStatus.Tone(state, seconds); face.UpdatePosition(seconds, animate, state == TimerState.Running);
            // Tray IPC, menus and unrelated controls refresh on the regular status tick.
            if (dialOnly) return;
            picker.SetCurrent(action.Key); picker.IsEnabled = mode.IsEnabled = idle;
            forceSwitch.IsEnabled = action.Key != "sleep";
            string awake = awakeMinutes < 0 ? "on" : awakeMinutes > 0 ? TimeText.Clock(awakeEnd - clock.Elapsed.TotalSeconds) : null;
            awakeChip.Content = awake == null ? "Off" : awakeMinutes < 0 ? "Indefinitely" : awake + " left";
            awakeChip.Background = Theme.Brush(awake == null ? Theme.Surface : Theme.Blend(Theme.Surface, Theme.Accent, .18));
            awakeChip.Foreground = Theme.Brush(awake == null ? Theme.Second : Theme.Blend(Theme.Accent, Colors.White, .22));
            syncing = true; awakeSwitch.IsChecked = awake != null; syncing = false;
            if (awakeMenu.Visible) awakeMenu.SetRows(ChipRows());
            if (trayMenu.Visible) trayMenu.SetRows(TrayRows());
            string tip = state == TimerState.Running ? "Sleep Timer · " + TimeText.Clock(seconds) + " left · " + TimerStatus.Complete(countdown.Fraction) + "% complete"
                : state == TimerState.Paused ? "Paused · " + TimeText.Clock(seconds) + " left · " + TimerStatus.Complete(countdown.Fraction) + "% complete"
                : "Sleep Timer - not started (" + idleLabel + ")";
            if (awake != null) tip += "\nKeep awake: " + awake;
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
            UpdateIcon(idle ? 1 : countdown.Fraction, state, face.Tone);
        }

        void UpdateIcon(double fraction, TimerState state, TimerTone tone)
        {
            string key = state + "|" + tone + "|" + Math.Round(fraction * 48);
            if (key == iconKey) return; iconKey = key;
            using (var bitmap = TrayGraphics.RingIcon(Forms.SystemInformation.SmallIconSize.Width, fraction, state, tone))
            {
                IntPtr next = bitmap.GetHicon();
                var previous = tray.Icon; IntPtr old = trayHandle;
                tray.Icon = System.Drawing.Icon.FromHandle(next);
                trayHandle = next; tray.Visible = true;
                previous?.Dispose(); if (old != IntPtr.Zero) Native.DestroyIcon(old);
            }
        }
        async void Fire()
        {
            if (TestMode) { countdown.Reset(); UpdateView(); return; }
            ticker.Stop(); countdown.Reset(); ApplyAwake(); executing = true;
            face.Interactive = false; face.State = TimerState.Idle; face.Tone = TimerTone.Idle; face.Snap(0);
            face.Title = action.FinalTitle; face.Time = "00:00"; face.Sub = action.FinalSub; face.InvalidateVisual();
            Title = "00:00 · " + action.FinalSub.TrimEnd('.');
            picker.IsEnabled = mode.IsEnabled = false;
            tray.Text = "Sleep Timer - " + action.FinalSub.ToLowerInvariant();
            bool force = forceSwitch.IsChecked == true;
            if (action.Key == "sleep")
            {
                new Thread(() =>
                {
                    Exception failure = null; try { Native.Sleep(); } catch (Exception ex) { failure = ex; }
                    if (!disposed) Dispatcher.BeginInvoke((Action)(() =>
                    {
                        if (disposed) return;
                        OnResumed();
                        if (failure != null) tray.ShowBalloonTip(10000, "Sleep Timer", "Could not sleep: " + failure.Message, Forms.ToolTipIcon.Error);
                    }));
                }) { IsBackground = true }.Start();
            }
            else
            {
                try
                {
                    await System.Threading.Tasks.Task.Run(() => Native.Shutdown(action.Key == "restart", force));
                    if (disposed) return;
                }
                catch (Exception ex) { if (disposed) return; OnResumed(); tray.ShowBalloonTip(10000, "Sleep Timer", "Could not " + action.Verb + ": " + ex.Message, Forms.ToolTipIcon.Error); return; }
                aborted?.Stop(); aborted = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
                aborted.Tick += (s, e) => { aborted.Stop(); aborted = null; OnResumed(); }; aborted.Start();
            }
        }
        void OnResumed()
        {
            if (disposed) return; executing = false; face.Interactive = true; ticker.Start(); UpdateView(true);
        }
        public void ShowWindow()
        {
            if (disposed) return; Show(); WindowState = WindowState.Normal; Activate();
        }
        bool ConfirmQuit()
        {
            ShowWindow();
            var dialog = new Window { Owner = this, Title = "Cancel timer?", Width = 360, Height = 185, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Theme.Brush(Theme.Bg), ShowInTaskbar = false };
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(Label("Quit and cancel the active timer?", 16));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 28, 0, 0) };
            var stay = new Button { Content = "Keep timer", Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
            var quit = new Button { Content = "Quit", IsDefault = true };
            stay.Click += (s, e) => dialog.DialogResult = false; quit.Click += (s, e) => dialog.DialogResult = true;
            buttons.Children.Add(stay); buttons.Children.Add(quit); panel.Children.Add(buttons); dialog.Content = panel;
            dialog.SourceInitialized += (s, e) => Native.DarkTitlebar(new WindowInteropHelper(dialog).Handle);
            return dialog.ShowDialog() == true;
        }
        public void EndSession() { sessionEnding = quitting = true; }
        void Quit() { quitting = true; Close(); }
        IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0312)
            {
                int index = (int)wParam - ShortcutId;
                if (index >= 0 && index < shortcuts.Length && shortcutRegistered[index]
                    && shortcuts[index].MatchesMessage(lParam.ToInt64()))
                {
                    RunShortcut(index); handled = true;
                }
            }
            if (msg == 0x0112)
            {
                int command = (int)wParam & 0xFFF0;
                if (command == 0xF020) { Hide(); handled = true; }
                if (command == 0xF100 && lParam == IntPtr.Zero) handled = true;
            }
            return IntPtr.Zero;
        }
        public void Dispose()
        {
            if (disposed) return;
            SaveDuration(); disposed = true;
            ticker.Stop(); aborted?.Stop();
            CompositionTarget.Rendering -= RenderWheelView; wheelViewPending = false;
            UnregisterShortcuts();
            source?.RemoveHook(WindowMessage);
            face.Snap(0);
            trayMenu.Dispose(); awakeMenu.Dispose(); fontMenu.Dispose(); colorPicker?.Close(); fontGallery?.Close();
            tray.Visible = false; tray.Icon?.Dispose(); tray.Dispose();
            if (trayHandle != IntPtr.Zero) { Native.DestroyIcon(trayHandle); trayHandle = IntPtr.Zero; }
            Native.KeepAwake(false, false);
        }
    }

    static class TrayGraphics
    {
        public static System.Drawing.Bitmap RingIcon(int size, double fraction, TimerState state, TimerTone tone)
        {
            var bitmap = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float k = size / 64f;
                var rect = new System.Drawing.RectangleF(9 * k, 9 * k, 46 * k, 46 * k);
                var color = Theme.TimerColor(tone);
                var active = System.Drawing.Color.FromArgb(color.R, color.G, color.B);
                var track = Theme.Blend(Theme.Bg, Theme.Text, .3);
                using (var pen = new System.Drawing.Pen(state == TimerState.Idle ? active : System.Drawing.Color.FromArgb(track.R, track.G, track.B), 9 * k)) g.DrawEllipse(pen, rect);
                if (state != TimerState.Idle && fraction > .001)
                    using (var pen = new System.Drawing.Pen(active, 9 * k)) g.DrawArc(pen, rect, -90, (float)(fraction * 360));
                if (state == TimerState.Paused)
                    using (var pen = new System.Drawing.Pen(active, 6 * k) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                    { g.DrawLine(pen, 26 * k, 25 * k, 26 * k, 39 * k); g.DrawLine(pen, 38 * k, 25 * k, 38 * k, 39 * k); }
            }
            return bitmap;
        }
    }
}
