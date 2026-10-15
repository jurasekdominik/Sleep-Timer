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
        readonly Canvas main = new Canvas { Background = Brushes.Transparent };
        readonly Grid settings = new Grid(), root = new Grid();
        readonly StackPanel navigation = new StackPanel(), settingsBlocks = new StackPanel();
        readonly List<Button> navigationButtons = new List<Button>(), swatches = new List<Button>();
        readonly List<FrameworkElement> cards = new List<FrameworkElement>();
        readonly Button picker = new Button { Width = Design.MainActionWidth };
        readonly ModeSelector modeSelector = new ModeSelector();
        readonly TextBox clockField = new TextBox { MaxLength = 5, Width = Design.TimeWidth, TextAlignment = TextAlignment.Center, ToolTip = "24-hour time, HH:mm. Scroll hours or minutes; arrow keys adjust." };
        readonly Button awakeChip = new Button { Width = Design.InputWidth * 2 };
        readonly ToggleButton awakeSwitch = new ToggleButton();
        readonly ToggleButton startupSwitch = new ToggleButton { IsChecked = Settings.Get("start_with_windows", false) };
        readonly ToggleButton forceSwitch = new ToggleButton { IsChecked = Settings.Get("force", true) };
        readonly TextBlock forceNote = new TextBlock { FontSize = Design.Caption, Foreground = Theme.Brush(Theme.Faint) };
        readonly Button accentButton = new Button { Width = Design.MainActionWidth, HorizontalAlignment = HorizontalAlignment.Left };
        readonly TextBox accentField = new TextBox { MaxLength = 7, Width = Design.InputWidth };
        readonly TextBox circleDuration = new TextBox { MaxLength = 5, TextAlignment = TextAlignment.Right, Width = Design.InputWidth };
        readonly TextBox counterDuration = new TextBox { MaxLength = 5, TextAlignment = TextAlignment.Right, Width = Design.InputWidth };
        readonly TextBlock animationNote = new TextBlock { FontSize = Design.Caption, Foreground = Theme.Brush(Theme.Faint), TextWrapping = TextWrapping.Wrap };
        readonly TextBlock accentNote = new TextBlock { FontSize = Design.Caption, TextWrapping = TextWrapping.Wrap };
        readonly TextBox quickMinutes = new TextBox { MaxLength = 4, Width = Design.InputWidth, Tag = "Minutes", ToolTip = "Whole minutes from 1 to 1440" };
        readonly TextBlock quickNote = new TextBlock { FontSize = Design.Caption, TextWrapping = TextWrapping.Wrap };
        readonly Button customChip = new Button();
        readonly TextBlock pageTitle = new TextBlock { FontSize = Design.Title, FontWeight = FontWeights.SemiBold };
        readonly TextBlock status = new TextBlock { FontSize = Design.Caption, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock awakeStatus = new TextBlock { FontSize = Design.Caption, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        readonly PopupMenu trayMenu = new PopupMenu(), awakeMenu = new PopupMenu(), actionMenu = new PopupMenu(), customMenu = new PopupMenu();
        readonly TranslateTransform modeSlide = new TranslateTransform();
        Canvas mode;
        ColorPicker colorPicker;
        PowerAction action, quickAction;
        int clockMinute, awakeMinutes, lastQuickMinutes, trayPage, selectedTab;
        double awakeEnd;
        bool clockMode, clockTouched, settingsOpen, syncing, durationDirty, warned, executing, quitting, disposed, sessionEnding;
        string iconKey;
        IntPtr trayHandle;
        HwndSource source;
        public bool TestMode { get; set; }

        public MainWindow(bool testMode = false)
        {
            TestMode = testMode;
            if (TestMode) Console.WriteLine("CHECKPOINT: construct window");
            countdown = new Countdown(Settings.Get("duration", Countdown.DefaultSeconds), () => clock.Elapsed.TotalSeconds);
            face.CircleMilliseconds = AnimationTiming.Normalize(Settings.Get("circle_sweep_ms", AnimationTiming.DefaultMilliseconds));
            face.CounterMilliseconds = AnimationTiming.Normalize(Settings.Get("counter_sweep_ms", AnimationTiming.DefaultMilliseconds));
            clockMode = Settings.Get("clock_mode", false);
            clockMinute = TimeText.DefaultClock(DateTime.Now, countdown.Duration);
            action = PowerAction.Find(Settings.Get("action", "shutdown"));
            quickAction = PowerAction.Find(Settings.Get("quick_action", action.Key));
            lastQuickMinutes = Settings.Get("quick_timer_last", 30);
            awakeMinutes = Settings.Get("awake", false) ? -1 : 0;
            screenOn = Settings.Get("awake_screen", false);
            if (startupSwitch.IsChecked == true) Startup.Set(true);
            Title = "Sleep Timer"; SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.CanMinimize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI"); FontSize = Design.Body; Foreground = Theme.Brush(Theme.Text);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            Background = Theme.Brush(Theme.Bg); Content = root;
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/assets/SleepTimer.ico"));
            root.Width = Design.Sidebar + Design.PageWidth; root.Height = Design.PageHeight + Design.StatusHeight;
            root.Background = Theme.Brush(Theme.Bg);
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Design.Sidebar) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Design.PageWidth) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Design.PageHeight) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Design.StatusHeight) });
            main.Width = settings.Width = Design.PageWidth; main.Height = settings.Height = Design.PageHeight;
            Grid.SetColumn(main, 1); Grid.SetColumn(settings, 1);
            root.Children.Add(main); root.Children.Add(settings); settings.Visibility = Visibility.Collapsed;
            if (TestMode) Console.WriteLine("CHECKPOINT: build shared controls");
            BuildNavigation(); BuildMain(); BuildSettings(); BuildStatus();
            if (TestMode) Console.WriteLine("CHECKPOINT: controls built");
            face.Toggle += StartPause; face.Cancel += Cancel; face.Wheel += AddTime;
            main.PreviewMouseWheel += (s, e) =>
            {
                // Preview includes the action dropdown as well as blank areas inside the watchface.
                if (!face.ContainsDial(e.GetPosition(face))) return;
                face.Scroll(e.Delta, Keyboard.Modifiers); e.Handled = true;
            };
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
                if (!TestMode && !sessionEnding && countdown.State != TimerState.Idle && !ConfirmQuit()) { quitting = false; e.Cancel = true; return; }
                Dispose();
                if (!TestMode) Application.Current.Shutdown();
            };
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape && settingsOpen) { ShowTimer(); e.Handled = true; } };
            PreviewMouseDown += (s, e) =>
            {
                if (!(e.OriginalSource is TextBox) && Keyboard.FocusedElement is TextBox box
                    && !IsDescendant(box, e.OriginalSource as DependencyObject)) Keyboard.ClearFocus();
            };

            if (TestMode) Console.WriteLine("CHECKPOINT: create native window handle");
            new WindowInteropHelper(this).EnsureHandle();
            if (TestMode) Console.WriteLine("CHECKPOINT: initialize window state");
            SyncMode(false); ApplyAwake(); UpdateView(); RefreshNavigation(); ticker.Start();
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
        static TextBlock Label(string text, double size = Design.Body, bool secondary = false) => Design.TextLabel(text, size, secondary);

        void BuildNavigation()
        {
            var sidebar = new Border { Background = Theme.Brush(Theme.Surface), BorderBrush = Theme.Brush(Theme.Border), BorderThickness = new Thickness(0, 0, Design.Line, 0) };
            var dock = new DockPanel();
            var collapse = new Button { Content = Icons.View(Icons.ChevronLeft), Style = (Style)FindResource("IconButton"), ToolTip = "Collapse navigation", Margin = new Thickness(Design.Gap) };
            collapse.Click += (s, e) =>
            {
                bool collapsed = root.ColumnDefinitions[0].Width.Value < Design.Sidebar;
                root.ColumnDefinitions[0].Width = new GridLength(collapsed ? Design.Sidebar : Design.CollapsedSidebar);
                root.Width = (collapsed ? Design.Sidebar : Design.CollapsedSidebar) + Design.PageWidth;
                foreach (var nav in navigationButtons)
                    if (nav.Content is Grid grid && grid.Children.Count > 1)
                    {
                        grid.Children[1].Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
                        bool child = !Equals(nav.ToolTip, "Timer") && !Equals(nav.ToolTip, "Settings");
                        nav.Margin = new Thickness(collapsed && child ? Design.ColumnGap : Design.Gap, 0, Design.Gap, child ? 0 : Design.Space);
                    }
                collapse.ToolTip = collapsed ? "Collapse navigation" : "Expand navigation";
                collapse.Content = Icons.View(collapsed ? Icons.ChevronLeft : Icons.ChevronRight);
            };
            DockPanel.SetDock(collapse, Dock.Bottom); dock.Children.Add(collapse);
            navigation.Margin = new Thickness(0, Design.PageEdge, 0, 0);
            AddNavigation("Timer", Icons.Timer, -1, false);
            AddNavigation("Settings", Icons.Settings, 0, false);
            AddNavigation("General", Icons.Settings, 0, true);
            AddNavigation("Appearance", Icons.Palette, 1, true);
            AddNavigation("Timers", Icons.List, 2, true);
            dock.Children.Add(navigation); sidebar.Child = dock; root.Children.Add(sidebar);
        }
        void AddNavigation(string text, string icon, int page, bool child)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Design.NavIcon + Design.IconGap) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(Icons.View(icon, Design.NavIcon));
            var label = Label(text); Grid.SetColumn(label, 1); grid.Children.Add(label);
            var b = new Button { Content = grid, ToolTip = text, Tag = page, Height = child ? Design.Field - Design.Space / 2 : Design.Field,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, Style = (Style)FindResource("NavButton"),
                Margin = new Thickness(child ? Design.ColumnGap : Design.Gap, 0, Design.Gap, child ? 0 : Design.Space) };
            b.Click += (s, e) => { if (page < 0) ShowTimer(); else { settingsOpen = true; SelectTab(page); } };
            navigation.Children.Add(b); navigationButtons.Add(b);
        }
        void RefreshNavigation()
        {
            foreach (var b in navigationButtons)
            {
                bool active = (int)b.Tag == (settingsOpen ? selectedTab : -1) && !Equals(b.ToolTip, "Settings");
                b.Background = active ? Theme.Brush(Theme.Blend(Theme.Surface, Theme.Accent, .15)) : Brushes.Transparent;
                b.BorderBrush = active ? Theme.Brush(Theme.Accent) : Brushes.Transparent;
                b.BorderThickness = active ? new Thickness(Design.ActiveMarker, 0, 0, 0) : new Thickness(0);
            }
        }
        void BuildStatus()
        {
            var bar = new Border { Background = Theme.Brush(Theme.Surface), BorderBrush = Theme.Brush(Theme.Border),
                BorderThickness = new Thickness(0, Design.Line, 0, 0), Padding = new Thickness(Design.PageEdge, 0, Design.PageEdge, 0) };
            var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(status); Grid.SetColumn(awakeStatus, 1); grid.Children.Add(awakeStatus);
            bar.Child = grid; Grid.SetColumnSpan(bar, 2); Grid.SetRow(bar, 1); root.Children.Add(bar);
        }
        void BuildMain()
        {
            Place(main, new TextBlock { Text = "Timer", FontSize = Design.Title, FontWeight = FontWeights.SemiBold }, Design.PageEdge, Design.PageEdge);
            Place(main, Label("Times use this PC's time zone.", Design.Caption, true), Design.PageEdge, Design.PageEdge + Design.Field);
            Place(main, face, Design.DialLeft, Design.DialTop);
            picker.Style = (Style)FindResource("DropdownButton");
            Place(main, picker, Design.MainActionLeft, Design.MainActionTop);
            picker.Click += (s, e) =>
            {
                if (actionMenu.Visible || actionMenu.JustClosed) return;
                actionMenu.Show(PowerAction.All.Select(a => new MenuRow { Text = a.Label, Selected = a.Key == action.Key, Click = () => SetAction(a.Key) }), picker);
            };
            mode = new Canvas { Width = Design.DialWidth, Height = Design.Field };
            modeSelector.RenderTransform = modeSlide;
            Place(mode, modeSelector, 0, 0);
            Place(mode, clockField, Design.ClockLeft, 0, Design.TimeWidth, Design.Field);
            Place(main, mode, Design.DialLeft, Design.ModeTop);
            modeSelector.Changed += SetMode;
            clockField.LostKeyboardFocus += (s, e) => CommitClock();
            clockField.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { CommitClock(); Keyboard.ClearFocus(); e.Handled = true; }
                else if (e.Key == Key.Up || e.Key == Key.Down)
                { EditClock((e.Key == Key.Up ? 1 : -1) * (clockField.CaretIndex < 3 ? 60 : 1)); e.Handled = true; }
            };
            clockField.PreviewMouseWheel += (s, e) =>
            {
                if (!clockField.IsEnabled) return;
                EditClock(Math.Sign(e.Delta) * (e.GetPosition(clockField).X < clockField.ActualWidth / 2 ? 60 : 1)); e.Handled = true;
            };
            Place(main, Label("Keep awake", Design.Label), Design.PageEdge, Design.AwakeTop, Design.InputWidth + Design.PageEdge, Design.Field);
            awakeChip.Style = (Style)FindResource("DropdownButton");
            Place(main, awakeChip, Design.InputWidth + Design.DialogEdge + Design.DialogEdge, Design.AwakeTop);
            Place(main, awakeSwitch, Design.PageWidth - Design.PageEdge - Design.SwitchWidth, Design.AwakeTop + (Design.Field - Design.SwitchHeight) / 2);
            awakeChip.Click += (s, e) => { if (!awakeMenu.Visible && !awakeMenu.JustClosed) awakeMenu.Show(AwakeRows(), awakeChip); };
            awakeSwitch.Click += (s, e) => { if (!syncing) SetAwake(awakeSwitch.IsChecked == true ? -1 : 0); };
            awakeSwitch.ToolTip = "Keep this PC awake";
            Place(main, Label("Scroll to adjust · Click to start or pause", Design.Caption, true), Design.PageEdge, Design.HintTop);
        }
        void BuildSettings()
        {
            settings.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            settings.RowDefinitions.Add(new RowDefinition());
            pageTitle.Margin = new Thickness(Design.PageEdge); settings.Children.Add(pageTitle);
            var scroll = new CompactScrollViewer { Content = settingsBlocks, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(Design.PageEdge, 0, Design.PageEdge, Design.PageEdge) };
            Grid.SetRow(scroll, 1); settings.Children.Add(scroll);
            var general = AddBlock(0, "Startup and power");
            AddSwitchRow(general, "Start with Windows", "Opens in the tray when you sign in.", startupSwitch);
            AddSwitchRow(general, "Force close apps", null, forceSwitch, forceNote);
            startupSwitch.Click += (s, e) => { bool on = startupSwitch.IsChecked == true; Startup.Set(on); Settings.Set("start_with_windows", on); };
            forceSwitch.Click += (s, e) => Settings.Set("force", forceSwitch.IsChecked == true);
            var help = AddBlock(0, "Timer controls");
            var helpPanel = new StackPanel();
            string[,] keys = { { "Left-click the dial", "Start / pause" }, { "Right-click the dial", "Cancel" }, { "Scroll", "±5 min" },
                { "Alt + scroll", "±1 min" }, { "Shift + scroll", "±15 min" }, { "Ctrl + scroll", "±30 min" }, { "Ctrl+Shift + scroll", "±60 min" }, { "Whole-hour stop", "400 ms" } };
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                var row = new Grid { Height = Design.Field }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(Label(keys[i, 0], Design.Body, true));
                var value = Label(keys[i, 1], Design.Mono); value.FontFamily = VisualText.Mono; Grid.SetColumn(value, 1); row.Children.Add(value); helpPanel.Children.Add(row);
            }
            help.Children.Add(new Border { Child = helpPanel, Background = Theme.Brush(Theme.Surface), CornerRadius = Design.CardCorner, Padding = new Thickness(Design.BlockPadding) });

            var accent = AddBlock(1, "Accent colour");
            var colourCells = PairGrid();
            var currentCell = new StackPanel();
            currentCell.Children.Add(new TextBlock { Text = "Current colour", FontSize = Design.Label, Margin = new Thickness(0, 0, 0, Design.Space) });
            accentButton.Style = (Style)FindResource("DropdownButton"); accentButton.Content = AccentPreview();
            accentButton.Click += (s, e) => OpenColorHelper(); currentCell.Children.Add(accentButton); colourCells.Children.Add(currentCell);
            var custom = new StackPanel();
            custom.Children.Add(new TextBlock { Text = "Custom colour", FontSize = Design.Label, Margin = new Thickness(0, 0, 0, Design.Space) });
            accentField.HorizontalAlignment = HorizontalAlignment.Left;
            custom.Children.Add(accentField); Grid.SetColumn(custom, 2); colourCells.Children.Add(custom); accent.Children.Add(colourCells);
            accentNote.Margin = new Thickness(0, Design.Space, 0, 0); accent.Children.Add(accentNote);
            accentField.Text = "#" + Theme.Accent.R.ToString("X2") + Theme.Accent.G.ToString("X2") + Theme.Accent.B.ToString("X2");
            accentField.TextChanged += (s, e) =>
            {
                if (syncing) return;
                string hex = accentField.Text.Trim(); if (!hex.StartsWith("#")) hex = "#" + hex;
                if (System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9A-Fa-f]{6}$")) ApplyAccent(Theme.Hex(hex), true);
            };
            var animation = AddBlock(1, "Animation duration", "Lower values are faster. Use 0 for an instant change.");
            var durationGrid = PairGrid();
            AddAnimationInput(durationGrid, "Circle sweep (ms)", circleDuration, 0, true);
            AddAnimationInput(durationGrid, "Counter (ms)", counterDuration, 2, false);
            animation.Children.Add(durationGrid); animationNote.Margin = new Thickness(0, Design.Space, 0, 0);
            animationNote.Text = "Defaults: 600 ms. Values save when you leave a field."; animation.Children.Add(animationNote);

            var timers = AddBlock(2, "Quick timers", "Start presets from the tray using the selected action.");
            timers.Children.Add(Label("Built in: 30 min, 1 h, 2 h, 4 h", Design.Caption, true));
            timers.Children.Add(new TextBlock { Text = "Duration (minutes)", FontSize = Design.Label, Margin = new Thickness(0, Design.BlockPadding, 0, Design.Space) });
            var addRow = new StackPanel { Orientation = Orientation.Horizontal };
            addRow.Children.Add(quickMinutes);
            var add = new Button { Content = "Add timer", MinWidth = Design.ButtonMin, Margin = new Thickness(Design.Gap, 0, 0, 0), Style = (Style)FindResource("PrimaryButton") };
            addRow.Children.Add(add); timers.Children.Add(addRow);
            add.Click += (s, e) => AddQuickTimer();
            quickMinutes.KeyDown += (s, e) => { if (e.Key == Key.Enter) { AddQuickTimer(); e.Handled = true; } };
            quickNote.Margin = new Thickness(0, Design.Space, 0, Design.BlockPadding);
            quickNote.Text = "Enter 1–1440 minutes."; quickNote.Foreground = Theme.Brush(Theme.Faint); timers.Children.Add(quickNote);
            customChip.Style = (Style)FindResource("DropdownButton"); customChip.HorizontalAlignment = HorizontalAlignment.Left; timers.Children.Add(customChip);
            customChip.Click += (s, e) => customMenu.Show(quickTimers.Custom.Select(m => new MenuRow { Text = QuickTimers.Label(m), Right = "Remove", Danger = true, Click = () =>
            { quickTimers.Remove(m); Settings.Set("quick_timers", quickTimers.Save()); RefreshQuickTimers(); } }), customChip);
            RefreshQuickTimers(); AccentNote(); SelectTab(0);
        }
        StackPanel AddBlock(int tab, string title, string help = null)
        {
            var block = Design.Block(title, help); block.Tag = tab; cards.Add(block); settingsBlocks.Children.Add(block); return block;
        }
        static Grid PairGrid()
        {
            var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Design.FieldPairGap) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); return grid;
        }
        static void AddSwitchRow(StackPanel parent, string title, string caption, ToggleButton control, TextBlock description = null)
        {
            var row = new Grid { MinHeight = caption == null && description == null ? Design.SettingsRow : Design.DescribedRow };
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Label(title, Design.Label));
            if (description != null || caption != null)
            {
                var note = description ?? Label(caption, Design.Caption, true);
                note.TextWrapping = TextWrapping.Wrap; note.MaxWidth = Design.SettingsContent - Design.SwitchWidth - Design.PageEdge;
                text.Children.Add(note);
            }
            row.Children.Add(text); control.VerticalAlignment = VerticalAlignment.Center; control.Margin = new Thickness(Design.Gap, 0, 0, 0);
            Grid.SetColumn(control, 1); row.Children.Add(control); parent.Children.Add(row);
        }
        void AddAnimationInput(Grid grid, string label, TextBox input, int column, bool circle)
        {
            var cell = new StackPanel();
            cell.Children.Add(new TextBlock { Text = label, FontSize = Design.Label, Margin = new Thickness(0, 0, 0, Design.Space) });
            input.HorizontalAlignment = HorizontalAlignment.Left;
            input.Text = (circle ? face.CircleMilliseconds : face.CounterMilliseconds).ToString(CultureInfo.InvariantCulture);
            input.ToolTip = "0–10000 ms. Default 600 ms. Enter or leave the field to save."; cell.Children.Add(input);
            Grid.SetColumn(cell, column); grid.Children.Add(cell);
            input.LostKeyboardFocus += (s, e) => CommitAnimationInput(input, circle);
            input.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { CommitAnimationInput(input, circle); e.Handled = true; }
                else if (e.Key == Key.Escape)
                { input.Text = (circle ? face.CircleMilliseconds : face.CounterMilliseconds).ToString(CultureInfo.InvariantCulture); CommitAnimationInput(input, circle); e.Handled = true; }
            };
        }
        FrameworkElement AccentPreview()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Border { Width = Design.Icon, Height = Design.Icon, CornerRadius = Design.ControlCorner, Background = Theme.Brush(Theme.Accent),
                Margin = new Thickness(0, 0, Design.IconGap, 0), VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(Label("#" + Theme.Accent.R.ToString("X2") + Theme.Accent.G.ToString("X2") + Theme.Accent.B.ToString("X2")));
            return row;
        }
        void CommitAnimationInput(TextBox input, bool circle)
        {
            if (!AnimationTiming.TryParse(input.Text.Trim(), out int milliseconds))
            {
                input.BorderBrush = Theme.Brush(Theme.Danger); animationNote.Text = "Enter a whole number from 0 to 10000 ms."; animationNote.Foreground = Theme.Brush(Theme.Danger); return;
            }
            if (circle) face.CircleMilliseconds = milliseconds; else face.CounterMilliseconds = milliseconds;
            Settings.Set(circle ? "circle_sweep_ms" : "counter_sweep_ms", milliseconds); input.Text = milliseconds.ToString(CultureInfo.InvariantCulture);
            input.SetResourceReference(TextBox.BorderBrushProperty, "BorderBrush");
            bool valid = AnimationTiming.TryParse(circleDuration.Text.Trim(), out _) && AnimationTiming.TryParse(counterDuration.Text.Trim(), out _);
            animationNote.Text = valid ? "Defaults: 600 ms. Values save when you leave a field." : "Enter a whole number from 0 to 10000 ms.";
            animationNote.Foreground = Theme.Brush(valid ? Theme.Faint : Theme.Danger);
        }
        void SelectTab(int tab)
        {
            selectedTab = tab;
            foreach (var block in cards) block.Visibility = (int)block.Tag == tab ? Visibility.Visible : Visibility.Collapsed;
            pageTitle.Text = tab == 0 ? "General" : tab == 1 ? "Appearance" : "Timers";
            if (settingsOpen) ShowPage(true);
            RefreshNavigation();
        }
        void ShowPage(bool showSettings)
        {
            settingsOpen = showSettings;
            var from = showSettings ? (FrameworkElement)main : settings;
            var to = showSettings ? (FrameworkElement)settings : main;
            to.Visibility = Visibility.Visible; to.IsHitTestVisible = true; from.IsHitTestVisible = false;
            from.Visibility = Visibility.Collapsed;
            to.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(Design.Motion)));
            RefreshNavigation(); Keyboard.ClearFocus();
        }
        void ShowTimer() => ShowPage(false);
        void ToggleSettings() { if (settingsOpen) ShowTimer(); else { settingsOpen = true; SelectTab(selectedTab); } }
        void SetMode(bool clock)
        {
            if (countdown.State != TimerState.Idle || executing) return;
            hourStop.Reset(); clockMode = clock;
            if (clock && !clockTouched) clockMinute = TimeText.DefaultClock(DateTime.Now, countdown.Duration);
            Settings.Set("clock_mode", clock); SyncMode(true); UpdateView();
        }
        void SyncMode(bool animate)
        {
            double x = clockMode ? 0 : (Design.DialWidth - Design.ModeWidth) / 2;
            if (animate) modeSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(modeSlide.X, x, TimeSpan.FromMilliseconds(Design.TabMotion)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else { modeSlide.BeginAnimation(TranslateTransform.XProperty, null); modeSlide.X = x; }
            modeSelector.Set(clockMode, animate);
            if (clockMode) clockField.Visibility = Visibility.Visible;
            if (animate)
            {
                var fade = new DoubleAnimation(clockField.Opacity, clockMode ? 1 : 0, TimeSpan.FromMilliseconds(Design.TabMotion)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
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
        void OpenColorHelper()
        {
            if (colorPicker != null && colorPicker.IsVisible) return;
            colorPicker = new ColorPicker(Theme.Accent) { Owner = this };
            colorPicker.Picked += ApplyAccent; colorPicker.Show();
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
            accentButton.Content = AccentPreview();
            string hex = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
            Settings.Set("accent", hex);
            if (!fromField) { syncing = true; accentField.Text = hex; syncing = false; }
            AccentNote(); iconKey = null; RefreshNavigation(); SyncMode(false); UpdateView();
            foreach (var visual in main.Children.OfType<FrameworkElement>().Concat(settings.Children.OfType<FrameworkElement>())) visual.InvalidateVisual();
            picker.InvalidateVisual();
        }
        void AccentNote()
        {
            double cr = Theme.Contrast(Theme.Accent, Theme.Surface);
            accentNote.Text = cr >= 4.5 ? "Applied to the dial, selection and controls." : "Small accent labels are lightened for readability.";
            bool closeToStatus = new[] { Design.Success, Design.Warning, Design.Danger, Design.Info }.Any(s => Math.Pow(s.R - Theme.Accent.R, 2) + Math.Pow(s.G - Theme.Accent.G, 2) + Math.Pow(s.B - Theme.Accent.B, 2) < 65 * 65);
            if (closeToStatus) accentNote.Text = "This accent is close to a status colour.";
            accentNote.Foreground = Theme.Brush(cr >= 4.5 ? Theme.Faint : Design.Warning);
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
        List<MenuRow> ChipRows() => AwakeRows();
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
            menu.Add(new MenuRow { Separator = true });
            menu.Add(new MenuRow { Text = "Project on GitHub", Click = () => Native.OpenUrl("https://github.com/jurasekdominik/sleep-timer") });
            menu.Add(new MenuRow { Text = "Quit", Danger = true, Click = Quit });
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
            if (!string.IsNullOrEmpty(face.Title)) face.Title = char.ToUpperInvariant(face.Title[0]) + face.Title.Substring(1).ToLowerInvariant();
            face.Tone = TimerStatus.Tone(state, seconds); face.UpdatePosition(seconds, animate, state == TimerState.Running);
            picker.Content = action.Label; picker.IsEnabled = mode.IsEnabled = idle;
            Title = face.Time + " · " + (idle ? clockMode ? "Scheduled" : "Ready" : state == TimerState.Running ? "Running" : "Paused");
            status.Text = Title; status.Foreground = Theme.Brush(state == TimerState.Running ? Theme.SmallAccent : Theme.Second);
            forceSwitch.IsEnabled = true;
            forceNote.Text = action.Key == "sleep" ? "Ignored for sleep; used for shutdown and restart." : "Skips save prompts when shutting down or restarting.";
            string awake = awakeMinutes < 0 ? "on" : awakeMinutes > 0 ? TimeText.Clock(awakeEnd - clock.Elapsed.TotalSeconds) : null;
            awakeChip.Content = awake == null ? "Off" : awakeMinutes < 0 ? "Indefinitely" : awake + " left";
            awakeChip.Background = Theme.Brush(Theme.Surface);
            awakeChip.Foreground = Theme.Brush(Theme.Text);
            syncing = true; awakeSwitch.IsChecked = awake != null; syncing = false;
            if (awakeMenu.Visible) awakeMenu.SetRows(ChipRows());
            if (trayMenu.Visible) trayMenu.SetRows(TrayRows());
            string tip = state == TimerState.Running ? "Sleep Timer · " + TimeText.Clock(seconds) + " left · " + TimerStatus.Complete(countdown.Fraction) + "% complete"
                : state == TimerState.Paused ? "Paused · " + TimeText.Clock(seconds) + " left · " + TimerStatus.Complete(countdown.Fraction) + "% complete"
                : "Sleep Timer - not started (" + idleLabel + ")";
            if (awake != null) tip += "\nKeep awake: " + awake;
            awakeStatus.Text = awake == null ? "Keep awake off" : awakeMinutes < 0 ? "Keep awake on" : "Awake " + awake;
            awakeStatus.Foreground = Theme.Brush(Theme.Faint);
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
            Title = "00:00 · " + action.FinalSub.TrimEnd('.'); status.Text = Title;
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
            var dialog = new Window { Owner = this, Title = "Cancel timer?", Width = Design.PageWidth, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Theme.Brush(Theme.Bg), ShowInTaskbar = false };
            var panel = new StackPanel { Margin = new Thickness(Design.DialogEdge) };
            panel.Children.Add(Label("Quit and cancel the active timer?", Design.Title));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, Design.PageEdge, 0, 0) };
            var stay = new Button { Content = "Keep timer", MinWidth = Design.DialogButtonMin, Margin = new Thickness(0, 0, Design.Gap, 0), IsCancel = true };
            var quit = new Button { Content = "Quit", IsDefault = true, Style = (Style)FindResource("DangerButton"), MinWidth = Design.DialogButtonMin };
            stay.Click += (s, e) => dialog.DialogResult = false; quit.Click += (s, e) => dialog.DialogResult = true;
            buttons.Children.Add(stay); buttons.Children.Add(quit); panel.Children.Add(buttons); dialog.Content = panel;
            dialog.SourceInitialized += (s, e) => Native.DarkTitlebar(new WindowInteropHelper(dialog).Handle);
            return dialog.ShowDialog() == true;
        }
        public void EndSession() { sessionEnding = quitting = true; }
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
