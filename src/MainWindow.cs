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
        readonly Button gear = new Button { Content = "⚙", Padding = new Thickness(0), FontSize = 20, Width = 30, Height = 30, ToolTip = "Settings" };
        readonly Button picker = new Button { Width = 160, Height = 32, Padding = new Thickness(8, 3, 8, 3) };
        readonly Button durationButton = new Button { Content = "Duration", Height = 32 };
        readonly Button clockButton = new Button { Content = "At time", Height = 32 };
        readonly TextBox clockField = new TextBox { MaxLength = 5, Width = 88, Height = 32, TextAlignment = TextAlignment.Center, ToolTip = "24-hour time, HH:mm. Scroll hours or minutes; arrow keys adjust." };
        readonly Button awakeChip = new Button { Width = 160, Height = 32 };
        readonly ToggleButton awakeSwitch = new ToggleButton();
        readonly ToggleButton startupSwitch = new ToggleButton { IsChecked = Settings.Get("start_with_windows", false) };
        readonly ToggleButton forceSwitch = new ToggleButton { IsChecked = Settings.Get("force", true) };
        readonly TextBox accentField = new TextBox { MaxLength = 7, Width = 90 };
        readonly TextBlock accentNote = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        readonly TextBox quickMinutes = new TextBox { MaxLength = 4, Width = 184, ToolTip = "Whole minutes from 1 to 1440" };
        readonly TextBlock quickNote = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        readonly Button customChip = new Button { Width = 288 };
        readonly PopupMenu trayMenu = new PopupMenu(), awakeMenu = new PopupMenu(224), actionMenu = new PopupMenu(180), customMenu = new PopupMenu(288);
        readonly List<FrameworkElement> cards = new List<FrameworkElement>();
        readonly List<Button> tabButtons = new List<Button>();
        readonly List<Button> swatches = new List<Button>();
        readonly Border modeHighlight = new Border { CornerRadius = new CornerRadius(8), Width = 100, Height = 32, HorizontalAlignment = HorizontalAlignment.Left };
        readonly TranslateTransform modeSlide = new TranslateTransform();
        Grid mode;
        ColorPicker colorPicker;
        PowerAction action, quickAction;
        int clockMinute, awakeMinutes, lastQuickMinutes, trayPage, selectedTab;
        double awakeEnd;
        bool clockMode, clockTouched, settingsOpen, syncing, durationDirty, warned, executing, quitting, disposed;
        string iconKey;
        IntPtr trayHandle;
        HwndSource source;
        public bool TestMode { get; set; }

        public MainWindow()
        {
            countdown = new Countdown(Settings.Get("duration", Countdown.DefaultSeconds), () => clock.Elapsed.TotalSeconds);
            clockMode = Settings.Get("clock_mode", false);
            clockMinute = TimeText.DefaultClock(DateTime.Now, countdown.Duration);
            action = PowerAction.Find(Settings.Get("action", "shutdown"));
            quickAction = PowerAction.Find(Settings.Get("quick_action", action.Key));
            lastQuickMinutes = Settings.Get("quick_timer_last", 30);
            awakeMinutes = Settings.Get("awake", false) ? -1 : 0;
            screenOn = Settings.Get("awake_screen", false);
            if (startupSwitch.IsChecked == true) Startup.Set(true);
            Title = "Sleep Timer"; Width = 384; Height = 548;
            ResizeMode = ResizeMode.CanMinimize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Theme.Brush(Theme.Bg); Content = root;
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/assets/SleepTimer.ico"));
            main.Width = settings.Width = 368; main.Height = settings.Height = 508;
            main.HorizontalAlignment = settings.HorizontalAlignment = HorizontalAlignment.Center;
            main.VerticalAlignment = settings.VerticalAlignment = VerticalAlignment.Top;
            root.Children.Add(main); root.Children.Add(settings);
            settings.Visibility = Visibility.Collapsed;
            BuildMain(); BuildSettings();
            gear.HorizontalAlignment = HorizontalAlignment.Right; gear.VerticalAlignment = VerticalAlignment.Top; gear.Margin = new Thickness(0, 12, 24, 0);
            root.Children.Add(gear); gear.Click += (s, e) => ToggleSettings();
            face.Toggle += StartPause; face.Cancel += Cancel; face.Wheel += AddTime;
            tray.MouseUp += (s, e) => { if (e.Button == Forms.MouseButtons.Right) Dispatcher.BeginInvoke((Action)ShowTrayMenu); };
            tray.MouseDoubleClick += (s, e) => { if (e.Button == Forms.MouseButtons.Left) Dispatcher.BeginInvoke((Action)ShowWindow); };
            tray.BalloonTipClicked += (s, e) => Dispatcher.BeginInvoke((Action)ShowWindow);
            ticker.Tick += (s, e) => Tick();
            SourceInitialized += (s, e) =>
            {
                source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                source.AddHook(WindowMessage);
                Native.DarkTitlebar(source.Handle);
            };
            StateChanged += (s, e) => { if (WindowState == WindowState.Minimized) Hide(); };
            Closing += (s, e) =>
            {
                if (!quitting) { e.Cancel = true; Hide(); return; }
                if (!TestMode && countdown.State != TimerState.Idle && !ConfirmQuit()) { quitting = false; e.Cancel = true; return; }
                Dispose();
                if (!TestMode) Application.Current.Shutdown();
            };
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape && settingsOpen) { ToggleSettings(); e.Handled = true; } };
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
            Place(main, picker, 104, 183);
            picker.Click += (s, e) =>
            {
                if (actionMenu.Visible || actionMenu.JustClosed) return;
                actionMenu.Show(PowerAction.All.Select(a => new MenuRow { Text = a.Label, Selected = a.Key == action.Key,
                    Click = () => SetAction(a.Key) }), picker);
            };
            mode = new Grid { Width = 320, Height = 32 };
            mode.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            mode.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            mode.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
            modeHighlight.RenderTransform = modeSlide;
            modeHighlight.SetResourceReference(Border.BackgroundProperty, "SoftBrush");
            Grid.SetColumnSpan(modeHighlight, 3); mode.Children.Add(modeHighlight);
            durationButton.Background = clockButton.Background = Brushes.Transparent;
            durationButton.BorderBrush = clockButton.BorderBrush = Brushes.Transparent;
            mode.Children.Add(durationButton); Grid.SetColumn(clockButton, 1); mode.Children.Add(clockButton);
            Grid.SetColumn(clockField, 2); mode.Children.Add(clockField);
            Place(main, mode, 24, 356);
            durationButton.Click += (s, e) => SetMode(false);
            clockButton.Click += (s, e) => SetMode(true);
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
            Place(main, awakeChip, 128, 404);
            Place(main, awakeSwitch, 300, 408);
            awakeChip.Click += (s, e) => { if (!awakeMenu.Visible && !awakeMenu.JustClosed) awakeMenu.Show(AwakeRows(), awakeChip); };
            awakeSwitch.Click += (s, e) => { if (!syncing) SetAwake(awakeSwitch.IsChecked == true ? -1 : 0); };
            awakeSwitch.ToolTip = "Keep the PC awake until you turn this off";
            AddFooter(main);
        }

        void AddFooter(Canvas parent)
        {
            Place(parent, new Border { Background = Theme.Brush(Theme.Border) }, 24, 452, 320, 1);
            var logo = new StackPanel { Orientation = Orientation.Horizontal, Cursor = Cursors.Hand, ToolTip = "Open the project on GitHub" };
            logo.Children.Add(Label("◔", 25));
            logo.Children.Add(new TextBlock { Text = " sleep timer", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            logo.MouseLeftButtonUp += (s, e) => Native.OpenUrl("https://github.com/jurasekdominik/sleep-timer");
            Place(parent, logo, 24, 464, 140, 28);
            var byline = Label("v" + Program.Version + " · © Dominik Jurásek", 11, true);
            Place(parent, byline, 164, 471, 156, 18);
            var github = new Image { Width = 16, Height = 16, Cursor = Cursors.Hand,
                Source = new BitmapImage(new Uri("pack://application:,,,/assets/github-mark-white.png")) };
            github.MouseLeftButtonUp += (s, e) => Native.OpenUrl("https://github.com/jurasekdominik/sleep-timer");
            Place(parent, github, 328, 471);
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
            var tabs = new StackPanel { Orientation = Orientation.Horizontal };
            string[] names = { "GENERAL", "APPEARANCE", "TIMERS" };
            for (int i = 0; i < names.Length; i++)
            {
                int tab = i;
                var b = new Button { Content = names[i], FontSize = 12, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 4, 0) };
                b.Click += (s, e) => SelectTab(tab); tabButtons.Add(b); tabs.Children.Add(b);
            }
            Place(settings, tabs, 24, 56, 320, 32);
            var general = (Canvas)Card(0, 100, 128).Child;
            SwitchRow(general, "Start with Windows", "Opens in the tray when you sign in", startupSwitch, 0);
            SwitchRow(general, "Force close apps", "Skips “save changes?” prompts (/f)", forceSwitch, 64);
            startupSwitch.Click += (s, e) => { bool on = startupSwitch.IsChecked == true; Startup.Set(on); Settings.Set("start_with_windows", on); };
            forceSwitch.Click += (s, e) => Settings.Set("force", forceSwitch.IsChecked == true);
            var help = (Canvas)Card(0, 244, 184).Child;
            string[,] rows = { { "Left-click the ring", "Start · Pause" }, { "Right-click the ring", "Cancel" }, { "Scroll", "±5 min" }, { "Alt + scroll", "±1 min" },
                { "Shift + scroll", "±15 min" }, { "Ctrl + scroll", "±30 min" }, { "Ctrl+Shift + scroll", "±60 min" }, { "Whole-hour stop", "400 ms" } };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                Place(help, Label(rows[i, 0], 12, true), 20, 12 + i * 20, 160, 20);
                var right = Label(rows[i, 1], 12); right.TextAlignment = TextAlignment.Right;
                Place(help, right, 180, 12 + i * 20, 120, 20);
            }
            var appearance = (Canvas)Card(1, 100, 176).Child;
            Place(appearance, Label("Accent colour"), 20, 16, 280, 24);
            Place(appearance, accentNote, 20, 44, 280, 32);
            string[] colors = { "#0CD29D", "#BADC58", "#FFB142", "#FF793F", "#FD79A8", "#8C7AE6", "#A29BFE", "#34ACE0" };
            for (int i = 0; i < colors.Length; i++)
            {
                string hex = colors[i]; Color color = Theme.Hex(hex);
                var dot = new System.Windows.Shapes.Ellipse { Fill = Theme.Brush(color), Width = 28, Height = 28 };
                var b = new Button { Content = dot, Padding = new Thickness(3), Width = 36, Height = 36, Background = Brushes.Transparent, ToolTip = hex, Tag = hex };
                b.Click += (s, e) => ApplyAccent(color); swatches.Add(b);
                Place(appearance, b, 16 + i % 4 * 40, 82 + i / 4 * 40);
            }
            Place(appearance, Label("Custom", 12, true), 208, 82, 92, 20);
            Place(appearance, accentField, 208, 110, 92, 32);
            var helper = new Button { Content = "Palette / mixer", FontSize = 11, Padding = new Thickness(3), Width = 104 };
            Place(appearance, helper, 202, 146, 104, 24);
            helper.Click += (s, e) =>
            {
                colorPicker?.Close();
                colorPicker = new ColorPicker(Theme.Accent) { Owner = this };
                colorPicker.Picked += ApplyAccent; colorPicker.Show();
            };
            accentField.Text = "#" + Theme.Accent.R.ToString("X2") + Theme.Accent.G.ToString("X2") + Theme.Accent.B.ToString("X2");
            accentField.TextChanged += (s, e) =>
            {
                if (syncing) return;
                string hex = accentField.Text.Trim();
                if (!hex.StartsWith("#")) hex = "#" + hex;
                if (System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9A-Fa-f]{6}$")) ApplyAccent(Theme.Hex(hex), true);
            };
            var timerCard = (Canvas)Card(2, 100, 236).Child;
            Place(timerCard, Label("Quick timers"), 16, 16, 288, 24);
            Place(timerCard, Label("Start from the tray using the selected action.", 12, true), 16, 48, 288, 20);
            Place(timerCard, Label("Built in: 30 min, 1 h, 2 h, 4 h", 12, true), 16, 72, 288, 20);
            Place(timerCard, quickMinutes, 16, 104, 184, 32);
            var add = new Button { Content = "Add timer", Width = 96, Height = 32, Padding = new Thickness(4), Style = (Style)FindResource("PrimaryButton") };
            Place(timerCard, add, 208, 104);
            add.Click += (s, e) => AddQuickTimer();
            quickMinutes.KeyDown += (s, e) => { if (e.Key == Key.Enter) { AddQuickTimer(); e.Handled = true; } };
            quickNote.Text = "Enter 1–1440 minutes"; quickNote.Foreground = Theme.Brush(Theme.Faint);
            Place(timerCard, quickNote, 16, 144, 288, 32);
            Place(timerCard, customChip, 16, 184, 288, 32);
            customChip.Click += (s, e) => customMenu.Show(quickTimers.Custom.Select(m => new MenuRow
            {
                Text = QuickTimers.Label(m), Right = "Remove", Click = () =>
                {
                    quickTimers.Remove(m); Settings.Set("quick_timers", quickTimers.Save()); RefreshQuickTimers();
                }
            }), customChip);
            SelectTab(0); RefreshQuickTimers(); AccentNote(); AddFooter(settings);
        }
        static void SwitchRow(Canvas parent, string title, string note, ToggleButton toggle, double y)
        {
            var label = Label(title), detail = Label(note, 12, true);
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

        void SelectTab(int tab)
        {
            selectedTab = tab;
            for (int i = 0; i < tabButtons.Count; i++) tabButtons[i].Foreground = Theme.Brush(i == tab ? Theme.Accent : Theme.Second);
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
            var from = settingsOpen ? main : settings;
            var to = settingsOpen ? settings : main;
            from.IsHitTestVisible = false; to.IsHitTestVisible = true; to.Visibility = Visibility.Visible;
            var hide = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
            hide.Completed += (s, e) => { if ((settingsOpen ? main : settings) == from) from.Visibility = Visibility.Collapsed; };
            from.BeginAnimation(OpacityProperty, hide);
            to.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
            gear.Content = settingsOpen ? "×" : "⚙"; Keyboard.ClearFocus();
        }

        void SetMode(bool clock)
        {
            if (countdown.State != TimerState.Idle || executing) return;
            hourStop.Reset(); clockMode = clock;
            if (clock && !clockTouched) clockMinute = TimeText.DefaultClock(DateTime.Now, countdown.Duration);
            Settings.Set("clock_mode", clock); SyncMode(true); UpdateView();
        }
        void SyncMode(bool animate)
        {
            double x = clockMode ? 100 : 0;
            if (animate) modeSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(modeSlide.X, x, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else { modeSlide.BeginAnimation(TranslateTransform.XProperty, null); modeSlide.X = x; }
            durationButton.Foreground = Theme.Brush(clockMode ? Theme.Second : Theme.Accent);
            clockButton.Foreground = Theme.Brush(clockMode ? Theme.Accent : Theme.Second);
            clockField.Visibility = clockMode ? Visibility.Visible : Visibility.Hidden;
            if (!clockField.IsKeyboardFocusWithin) clockField.Text = TimeText.HourMinute(clockMinute);
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
            UpdateView(true);
        }
        void StartPause()
        {
            if (executing) return;
            hourStop.Reset(); SaveDuration();
            switch (countdown.State)
            {
                case TimerState.Running: countdown.Pause(); break;
                case TimerState.Paused: countdown.Resume(); break;
                default:
                    double seconds = clockMode ? Math.Max(1, (TimeText.NextOccurrence(DateTime.Now, clockMinute) - DateTime.Now).TotalSeconds) : countdown.Duration;
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
            { quickNote.Text = "Use a whole number from 1 to 1440 minutes."; quickNote.Foreground = Theme.Brush(Theme.Danger); }
            else if (!quickTimers.Add(minutes))
            { quickNote.Text = "That timer is already in the quick menu."; quickNote.Foreground = Theme.Brush(Theme.Second); }
            else
            {
                Settings.Set("quick_timers", quickTimers.Save()); quickMinutes.Clear();
                quickNote.Text = "Added " + QuickTimers.Label(minutes) + "."; quickNote.Foreground = Theme.Brush(Theme.Accent);
                RefreshQuickTimers();
            }
        }
        void RefreshQuickTimers()
        {
            int count = quickTimers.Custom.Count();
            customChip.Content = count == 0 ? "No custom timers" : "Custom timers (" + count + ")";
            customChip.IsEnabled = count > 0;
            if (trayMenu.Visible) trayMenu.SetRows(TrayRows());
        }
        void ApplyAccent(Color color) => ApplyAccent(color, false);
        void ApplyAccent(Color color, bool fromField)
        {
            Theme.Accent = color; Theme.UpdateResources(Application.Current.Resources);
            string hex = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
            Settings.Set("accent", hex);
            if (!fromField) { syncing = true; accentField.Text = hex; syncing = false; }
            AccentNote(); iconKey = null; SelectTab(selectedTab); UpdateView();
        }
        void AccentNote()
        {
            double cr = Theme.Contrast(Theme.Accent, Theme.Bg);
            accentNote.Text = cr >= 3 ? "The ring, buttons, switches and the tray icon" : "Low contrast (" + cr.ToString("0.0", CultureInfo.InvariantCulture) + " : 1) — hard to read";
            accentNote.Foreground = Theme.Brush(cr >= 3 ? Theme.Faint : Theme.Danger);
            foreach (var b in swatches) b.BorderBrush = Theme.Brush(string.Equals((string)b.Tag, accentField.Text, StringComparison.OrdinalIgnoreCase) ? Theme.Accent : Theme.Border);
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
                new MenuRow { Text = "Keep awake", Right = (awakeMinutes == 0 ? "Off" : awakeMinutes < 0 ? "On" : TimeText.Clock(awakeEnd - clock.Elapsed.TotalSeconds)) + " ›", KeepOpen = true, Click = () => SetTrayPage(1) },
                new MenuRow { Text = "Quick timers", Right = quickAction.Label + " ›", KeepOpen = true, Enabled = !executing, Click = () => SetTrayPage(2) },
                new MenuRow { Separator = true },
                new MenuRow { Text = idle ? "Start timer" : "Cancel timer",
                    Right = idle ? clockMode ? "At " + TimeText.HourMinute(clockMinute) : TimeText.Clock(countdown.Duration) : null,
                    Enabled = !executing, Click = () => { if (countdown.State == TimerState.Idle) StartPause(); else Cancel(); } },
            };
            if (!idle) menu.Add(new MenuRow { Text = countdown.State == TimerState.Running ? "Pause" : "Resume", Enabled = !executing, Click = StartPause });
            else
            {
                int minutes = quickTimers.All.Contains(lastQuickMinutes) ? lastQuickMinutes : 30;
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
            trayPage = 0; trayMenu.Show(TrayRows());
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
        void UpdateView(bool animate = false)
        {
            if (disposed || executing) return;
            var state = countdown.State; bool idle = state == TimerState.Idle;
            double seconds = idle ? countdown.Duration : countdown.Remaining;
            string idleLabel = "";
            face.State = state; face.ClockMode = idle && clockMode;
            if (idle && clockMode)
            {
                var target = TimeText.NextOccurrence(DateTime.Now, clockMinute);
                seconds = Math.Max(0, (target - DateTime.Now).TotalSeconds);
                face.Title = action.ClockTitle; face.Time = TimeText.HourMinute(clockMinute);
                face.Sub = (target.Date > DateTime.Today ? "tomorrow, " : "") + "in " + TimeText.Human(seconds);
                idleLabel = "at " + face.Time;
            }
            else if (idle)
            {
                face.Title = ""; face.Time = idleLabel = TimeText.Clock(seconds);
                face.Sub = action.Would + " " + DateTime.Now.AddSeconds(seconds).ToString("HH:mm");
            }
            else
            {
                face.Title = state == TimerState.Running ? action.RunTitle : "PAUSED"; face.Time = TimeText.Clock(seconds);
                face.Sub = state == TimerState.Running ? action.At + " " + DateTime.Now.AddSeconds(seconds).ToString("HH:mm") : "Resume to continue";
            }
            face.Tone = TimerStatus.Tone(state, seconds); face.UpdatePosition(seconds, animate, state == TimerState.Running);
            picker.Content = action.Label + " ⌄"; picker.IsEnabled = mode.IsEnabled = idle;
            forceSwitch.IsEnabled = action.Key != "sleep";
            string awake = awakeMinutes < 0 ? "on" : awakeMinutes > 0 ? TimeText.Clock(awakeEnd - clock.Elapsed.TotalSeconds) : null;
            awakeChip.Content = awake == null ? "Off ⌄" : awakeMinutes < 0 ? "Indefinitely ⌄" : awake + " left ⌄";
            syncing = true; awakeSwitch.IsChecked = awake != null; syncing = false;
            if (awakeMenu.Visible) awakeMenu.SetRows(AwakeRows());
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
        void Fire()
        {
            if (TestMode) { countdown.Reset(); UpdateView(); return; }
            ticker.Stop(); countdown.Reset(); ApplyAwake(); executing = true;
            face.Interactive = false; face.State = TimerState.Idle; face.Tone = TimerTone.Idle; face.Snap(0);
            face.Title = action.FinalTitle; face.Time = "00:00"; face.Sub = action.FinalSub; face.InvalidateVisual();
            picker.IsEnabled = mode.IsEnabled = false;
            tray.Text = "Sleep Timer - " + action.FinalSub.ToLowerInvariant();
            bool force = forceSwitch.IsChecked == true;
            if (action.Key == "sleep")
            {
                new Thread(() =>
                {
                    Exception failure = null; try { Native.Sleep(force); } catch (Exception ex) { failure = ex; }
                    if (!disposed) Dispatcher.BeginInvoke((Action)(() =>
                    {
                        OnResumed();
                        if (failure != null) tray.ShowBalloonTip(10000, "Sleep Timer", "Could not sleep: " + failure.Message, Forms.ToolTipIcon.Error);
                    }));
                }) { IsBackground = true }.Start();
            }
            else
            {
                try { Native.Shutdown(action.Key == "restart", force); }
                catch (Exception ex) { OnResumed(); tray.ShowBalloonTip(10000, "Sleep Timer", "Could not " + action.Verb + ": " + ex.Message, Forms.ToolTipIcon.Error); return; }
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
        void Quit() { quitting = true; Close(); }
        IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
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
            source?.RemoveHook(WindowMessage);
            face.Snap(0);
            trayMenu.Dispose(); awakeMenu.Dispose(); actionMenu.Dispose(); customMenu.Dispose(); colorPicker?.Close();
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
