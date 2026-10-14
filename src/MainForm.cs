using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

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

    sealed class MainForm : Form, IMessageFilter
    {
        // layout in 96-dpi pixels on the 4 px grid: 24 margins, 16 between groups, controls 28 / 32 / 36 tall
        const int ContentW = 320, Side = 24, WinW = ContentW + 2 * Side, Gap = 16, RingY = 12, RingH = 328, ModeY = RingY + RingH + Gap, ModeH = 32,
                  AwakeY = ModeY + ModeH + Gap, AwakeH = 32, FooterY = AwakeY + AwakeH + Gap, TabsY = 56, CardY = 88, WarnSeconds = 60;

        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Countdown countdown;
        readonly HourStop hourStop = new HourStop();
        readonly ToolTip tooltips = new ToolTip();
        System.Windows.Forms.Timer aborted;
        bool durationDirty, resourcesDisposed;
        readonly Stopwatch wheelSave = Stopwatch.StartNew();
        readonly Ring ring = new Ring();
        readonly ActionPicker picker;
        readonly ModeRow modeRow = new ModeRow();
        readonly GearButton gear = new GearButton();
        readonly Switch startupSwitch, forceSwitch;
        readonly Page main = new Page(), settings = new Page();
        readonly TextTabs tabs = new TextTabs("GENERAL", "APPEARANCE", "TIMERS");
        readonly List<(int tab, Card card)> tabCards = new List<(int, Card)>();
        readonly Swatches swatches = new Swatches { Under = Theme.Surface, Columns = 4 };
        readonly Field accentEdit = new Field("#RRGGBB") { Under = Theme.Surface };
        readonly Lbl accentNote = new Lbl { Color = Theme.Faint, Font = Theme.Ui(9f), Under = Theme.Surface };
        readonly Drop colorDrop = new Drop();
        readonly FadeLayer fade = new FadeLayer();
        bool settingsOpen;
        readonly Lbl forceLabel = new Lbl { Text = "Force close apps", Color = Theme.Text },
                     forceNote = new Lbl { Text = "Skips \u201Csave changes?\u201D prompts (/f)", Color = Theme.Faint, Font = Theme.Ui(9f) };
        readonly NotifyIcon tray = new NotifyIcon();
        readonly System.Windows.Forms.Timer ticker = new System.Windows.Forms.Timer { Interval = Motion.Progress };

        // Keep awake (independent of the countdown), independent of the timer: 0 = off, -1 = indefinitely, else minutes
        static readonly int[] AwakeChoices = { 0, -1, 15, 30, 60, 120, 240 };
        const int ChipW = 160, TrayW = 224;
        readonly PopupList trayMenu = new PopupList(TrayW), awakeList = new PopupList(ChipW);
        readonly Switch awakeSwitch;
        readonly Chip awakeChip = new Chip();
        int awakeMinutes;
        double awakeEnd;
        bool syncingAwake, screenOn, trayAwakePage, trayQuickPage, trayQuickActionPage;
        int lastQuickMinutes = Settings.Get("quick_timer_last", 30);
        int QuickShortcut => quickTimers.All.Contains(lastQuickMinutes) ? lastQuickMinutes : 30;
        readonly QuickTimers quickTimers = new QuickTimers(Settings.Get("quick_timers", ""));
        readonly PopupList customTimerList = new PopupList(240);
        readonly Field quickMinutes = new Field("Minutes") { Under = Theme.Surface };
        readonly Chip customTimerChip = new Chip { Under = Theme.Surface };
        readonly Lbl quickNote = new Lbl { Under = Theme.Surface, Font = Theme.Ui(9f), Text = "Enter 1–1440 minutes", Color = Theme.Faint };

        PowerAction action, quickAction;
        bool clockMode, clockTouched, quitting, warned;
        int clockMinute;
        string iconKey;
        IntPtr trayIconHandle, windowIconHandle;

        public MainForm()
        {
            countdown = new Countdown(Settings.Get("duration", Countdown.DefaultSeconds), () => clock.Elapsed.TotalSeconds);
            clockMode = Settings.Get("clock_mode", false);
            clockMinute = TimeText.DefaultClock(DateTime.Now, countdown.Duration);
            action = PowerAction.Find(Settings.Get("action", "shutdown"));
            quickAction = PowerAction.Find(Settings.Get("quick_action", action.Key));
            awakeMinutes = Settings.Get("awake", false) ? -1 : 0;  // only "indefinitely" survives a restart
            screenOn = Settings.Get("awake_screen", false);
            bool startup = Settings.Get("start_with_windows", false);
            if (startup) Startup.Set(true);  // re-point the Run entry in case the exe was moved

            Text = "Sleep Timer";
            BackColor = Theme.Bg;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Theme.D(WinW), Theme.D(FooterY + 56));
            KeyPreview = true;
            Application.AddMessageFilter(this);

            Place(gear, Side + ContentW - 30, RingY, 30, 30);
            Controls.Add(fade);
            main.Bounds = ClientRectangle;
            Controls.Add(main);
            Place(ring, Side, RingY, ContentW, RingH, main);
            picker = new ActionPicker(PowerAction.All, action.Key);
            ring.Controls.Add(picker);
            picker.Location = new Point((ring.Width - picker.Width) / 2, ring.SlotTop);
            Place(modeRow, Side, ModeY, ContentW, ModeH, main);
            awakeSwitch = new Switch(awakeMinutes != 0);
            AddAwakeRow();
            AddFooter(main);

            startupSwitch = new Switch(startup);
            forceSwitch = new Switch(Settings.Get("force", true));
            BuildSettings();
            gear.Click += (s, e) => ToggleSettings();
            tooltips.SetToolTip(gear, "Settings");

            ring.Wheel += AddTime;
            picker.Changed += SetAction;
            modeRow.Mode.Changed += SetMode;
            modeRow.Time.Changed += () => { clockMinute = modeRow.Time.Minutes; clockTouched = true; UpdateView(); };
            ring.ToggleClicked += StartPause;
            ring.CancelClicked += Cancel;
            startupSwitch.Toggled += on => { Settings.Set("start_with_windows", on); Startup.Set(on); };
            forceSwitch.Toggled += on => Settings.Set("force", on);
            ticker.Tick += (s, e) => Tick();

            BuildTray();
            ApplyAwake();
            SyncMode(false);
            SyncAction();
            CreateHandle();
            ticker.Start();
            UpdateView();
        }

        void Place(Control c, int x, int y, int w, int h, Control parent = null)
        {
            c.SetBounds(Theme.D(x), Theme.D(y), Theme.D(w), Theme.D(h));
            (parent ?? this).Controls.Add(c);
        }

        /// <summary>Keep awake on the main view: the switch is on (indefinitely) / off, the chip rolls open the durations.</summary>
        void AddAwakeRow()
        {
            var title = new Lbl { Text = "Keep awake", Color = Theme.Text, Cursor = Cursors.Hand };
            Place(title, Side, AwakeY + 6, 96, 20, main);
            Place(awakeChip, Side + ContentW - 56 - ChipW, AwakeY + 2, ChipW, 28, main);
            awakeSwitch.Location = new Point(Theme.D(Side + ContentW) - awakeSwitch.Width, Theme.D(AwakeY) + (Theme.D(AwakeH) - awakeSwitch.Height) / 2);
            main.Controls.Add(awakeSwitch);
            title.Click += (s, e) => awakeSwitch.Toggle();
            awakeSwitch.Toggled += on => { if (!syncingAwake) SetAwake(on ? -1 : 0); };
            awakeChip.MouseDown += (s, e) =>
            {
                // JustClosed: this mouse-down is the one that closed the list - it must not reopen it
                if (e.Button != MouseButtons.Left || awakeList.Visible || awakeList.JustClosed) return;
                // the list opens over the chip, the chip becoming its first row, and grows down from there
                awakeList.Show(ChipRows(), awakeChip.PointToScreen(Point.Empty), false, true);
            };
            tooltips.SetToolTip(awakeSwitch, "Keeps the PC from sleeping until you turn it off");
        }

        static string AwakeLabel(int minutes) =>
            minutes == 0 ? "Off" : minutes < 0 ? "Indefinitely" : minutes < 60 ? $"{minutes} minutes" : $"{minutes / 60} hour{(minutes > 60 ? "s" : "")}";

        /// <summary>withCurrent false: under the chip, which already shows the current choice.</summary>
        List<PickRow> AwakeRows(bool withCurrent = true)
        {
            var rows = AwakeChoices.Where(m => withCurrent || m != awakeMinutes)
                .Select(m => new PickRow { Text = AwakeLabel(m), Check = m == awakeMinutes, Click = () => SetAwake(m) }).ToList();
            rows.Add(new PickRow { Sep = true });
            rows.Add(new PickRow
            {
                Text = "Keep screen on", Right = screenOn ? "On" : "Off", Check = screenOn, KeepOpen = true,
                Click = () => { screenOn = !screenOn; Settings.Set("awake_screen", screenOn); ApplyAwake(); UpdateView(); },
            });
            return rows;
        }

        List<PickRow> ChipRows()
        {
            var rows = new List<PickRow> { new PickRow { Text = awakeChip.Text, Header = true }, new PickRow { Sep = true } };
            rows.AddRange(AwakeRows(false));
            return rows;
        }

        List<PickRow> TrayRows()
        {
            if (trayQuickActionPage)
            {
                var rows = new List<PickRow>
                {
                    new PickRow { Text = "Timer action", Back = true, KeepOpen = true,
                                  Click = () => { trayQuickActionPage = false; trayMenu.SetRows(TrayRows()); } },
                    new PickRow { Sep = true },
                };
                rows.AddRange(PowerAction.All.Select(a => new PickRow
                {
                    Text = a.Label, Check = a.Key == quickAction.Key, KeepOpen = true, Enabled = ring.Interactive,
                    Click = () =>
                    {
                        quickAction = a;
                        Settings.Set("quick_action", a.Key);
                        if (countdown.State == TimerState.Idle) SetAction(a.Key);
                        trayQuickActionPage = false;
                        UpdateView();
                    },
                }));
                return rows;
            }
            if (trayQuickPage)
            {
                var rows = new List<PickRow>
                {
                    new PickRow { Text = "Quick timers", Back = true, KeepOpen = true, Click = () => { trayQuickPage = false; trayMenu.SetRows(TrayRows()); } },
                    new PickRow { Text = "Action", Right = quickAction.Label, More = true, KeepOpen = true, Enabled = ring.Interactive,
                                  Click = () => { trayQuickActionPage = true; trayMenu.SetRows(TrayRows()); } },
                    new PickRow { Sep = true },
                };
                rows.AddRange(quickTimers.All.Select(m => new PickRow { Text = QuickTimers.Label(m), Enabled = ring.Interactive, Click = () => StartQuickTimer(m) }));
                return rows;
            }
            if (trayAwakePage)
            {
                var rows = new List<PickRow>
                {
                    new PickRow { Text = "Keep awake", Back = true, KeepOpen = true, Click = () => { trayAwakePage = false; trayMenu.SetRows(TrayRows()); } },
                    new PickRow { Sep = true },
                };
                rows.AddRange(AwakeRows());
                return rows;
            }
            var state = countdown.State;
            bool idle = state == TimerState.Idle;
            var menu = new List<PickRow>
            {
                new PickRow { Text = "Keep awake", More = true, KeepOpen = true,
                              Right = awakeMinutes == 0 ? "Off" : awakeMinutes < 0 ? "On" : TimeText.Clock(awakeEnd - clock.Elapsed.TotalSeconds),
                              Click = () => { trayAwakePage = true; trayMenu.SetRows(TrayRows()); } },
                new PickRow { Text = "Quick timers", Right = quickAction.Label, More = true, KeepOpen = true, Enabled = ring.Interactive,
                              Click = () => { trayQuickPage = true; trayMenu.SetRows(TrayRows()); } },
            };
            menu.Add(new PickRow { Sep = true });
            menu.Add(new PickRow { Text = idle ? "Start timer" : "Cancel timer",
                                   Right = idle ? (clockMode ? "At " + TimeText.HourMinute(clockMinute) : TimeText.Clock(countdown.Duration)) : null, Enabled = ring.Interactive,
                                   Click = () => { if (countdown.State == TimerState.Idle) StartPause(); else Cancel(); } });
            if (!idle)
                menu.Add(new PickRow { Text = state == TimerState.Running ? "Pause" : "Resume", Enabled = ring.Interactive, Click = StartPause });
            if (idle)
            {
                menu.Add(new PickRow { Sep = true });
                menu.Add(new PickRow { Text = "Quick start", Right = TimeText.Human(QuickShortcut * 60), Enabled = ring.Interactive,
                                       Click = () => StartQuickTimer(QuickShortcut) });
            }
            menu.Add(new PickRow { Sep = true });
            menu.Add(new PickRow { Text = "Quit", Danger = true, Click = Quit });
            return menu;
        }

        void ShowTrayMenu()
        {
            if (trayMenu.Visible || trayMenu.JustClosed) return;
            trayAwakePage = trayQuickPage = trayQuickActionPage = false;
            Native.SetForegroundWindow(Handle);  // so a click anywhere else deactivates the app and closes the menu
            var p = Cursor.Position;
            var area = Screen.FromPoint(p).WorkingArea;
            trayMenu.Show(TrayRows(), new Point(p.X - Theme.D(TrayW), Math.Min(p.Y, area.Bottom)), true, false);
        }

        void AddFooter(Control parent)
        {
            Place(new Painted { Under = Theme.Border }, Side, FooterY, ContentW, 1, parent);
            Place(new Footer(), Side, FooterY + 12, ContentW, 28, parent);
        }

        void BuildSettings()
        {
            settings.Bounds = ClientRectangle;
            Controls.Add(settings);
            Place(new Lbl { Text = "Settings", Color = Theme.Text, Font = Theme.Ui(11f, 600) }, Side, RingY, 200, 32, settings);
            tabs.Location = new Point(Theme.D(Side), Theme.D(TabsY));
            settings.Controls.Add(tabs);
            tabs.Changed += i => { foreach (var (tab, card) in tabCards) card.Visible = tab == i; Defocus(this); };

            // general: two switch rows
            var general = AddCard(0, CardY, 2 * 64);
            SettingsRow(general, new Lbl { Text = "Start with Windows", Color = Theme.Text },
                        new Lbl { Text = "Opens in the tray when you sign in", Color = Theme.Faint, Font = Theme.Ui(9f) }, startupSwitch, 0);
            Place(new Painted { Under = Theme.Border }, 20, 64, ContentW - 40, 1, general);
            SettingsRow(general, forceLabel, forceNote, forceSwitch, 64);

            // appearance: accent colour - swatches, or any colour through the custom field and its helper
            var look = AddCard(1, CardY, 160);
            Place(new Lbl { Text = "Accent colour", Color = Theme.Text, Under = Theme.Surface }, 20, 16, ContentW - 40, 20, look);
            Place(accentNote, 20, 38, ContentW - 40, 18, look);
            Place(swatches, 14, 64, 4 * 40 + 12, 80, look);
            Place(new Lbl { Text = "Custom", Font = Theme.Ui(9f), Under = Theme.Surface }, ContentW - 108, 70, 88, 16, look);
            Place(accentEdit, ContentW - 108, 104, 88, 32, look);
            accentEdit.MaxLength = 7;
            string hex = Theme.HexOf(Theme.Accent);
            swatches.Selected = hex;
            accentEdit.Text = hex;
            AccentNote();
            swatches.Picked += h => ApplyAccent(ColorTranslator.FromHtml(h));
            accentEdit.Edited += () =>
            {
                string t = accentEdit.Text.Trim();
                if (!t.StartsWith("#")) t = "#" + t;
                if (System.Text.RegularExpressions.Regex.IsMatch(t, "^#[0-9A-Fa-f]{6}$")) ApplyAccent(ColorTranslator.FromHtml(t), true);
                else accentEdit.Invalid = t.Length >= 7;
            };
            accentEdit.MouseDown += (s, e) => OpenColorHelper();
            accentEdit.Box_.MouseDown += (s, e) => OpenColorHelper();
            var tip = tooltips;
            tip.SetToolTip(accentEdit.Box_, "Type a hex colour, or pick one from the palette or the mixer");

            // controls: what the mouse does on the ring - under the switches, on the General tab
            var controls = new List<(string, string)> { ("Left-click the ring", "Start \u00B7 Pause"), ("Right-click the ring", "Cancel") };
            controls.AddRange(Ring.WheelSteps.Select(w => (w.keys == "Scroll" ? "Scroll" : w.keys + " + scroll", $"\u00B1{w.minutes} min")));
            const int rowH = 20;
            var help = AddCard(0, CardY + 2 * 64 + Gap, controls.Count * rowH + 24);
            for (int i = 0; i < controls.Count; i++)
            {
                int y = 12 + i * rowH;
                Place(new Lbl { Text = controls[i].Item1, Font = Theme.Ui(9f), Under = Theme.Surface }, 20, y, 160, rowH, help);
                Place(new Lbl { Text = controls[i].Item2, Font = Theme.Ui(9f), Color = Theme.Text, Under = Theme.Surface, Align = StringAlignment.Far },
                      180, y, ContentW - 200, rowH, help);
            }
            BuildQuickTimerSettings();
            foreach (var (tab, card) in tabCards) card.Visible = tab == 0;
            AddFooter(settings);
        }

        void StartQuickTimer(int minutes)
        {
            if (!ring.Interactive) return;
            ring.FlushWheel();
            hourStop.Reset();
            durationDirty = false;
            countdown.Reset();
            SetAction(quickAction.Key);
            countdown.SetDuration(minutes * 60);
            lastQuickMinutes = minutes;
            Settings.Set("quick_timer_last", minutes);
            clockMode = false;
            Settings.Set("clock_mode", false);
            Settings.Set("duration", countdown.Duration);
            SyncMode(false);
            countdown.Start(countdown.Duration);
            warned = false;
            ApplyAwake();
            UpdateView(true);
        }

        void BuildQuickTimerSettings()
        {
            var card = AddCard(2, CardY, 224);
            Place(new Lbl { Text = "Quick timers", Color = Theme.Text, Under = Theme.Surface }, 16, 16, 288, 24, card);
            Place(new Lbl { Text = "Start from the tray using the selected action.", Under = Theme.Surface, Font = Theme.Ui(9f) }, 16, 48, 288, 20, card);
            Place(new Lbl { Text = "Built in: 30 min, 1 h, 2 h, 4 h", Under = Theme.Surface, Font = Theme.Ui(9f) }, 16, 72, 288, 20, card);
            Place(quickMinutes, 16, 104, 184, 32, card);
            quickMinutes.MaxLength = 4;
            var add = new Btn("Add timer", BtnKind.Primary) { Under = Theme.Surface };
            Place(add, 208, 104, 96, 32, card);
            Place(quickNote, 16, 144, 288, 20, card);
            Place(customTimerChip, 16, 176, 288, 32, card);
            add.Click += (sender, e) => AddQuickTimer();
            quickMinutes.Submit += AddQuickTimer;
            quickMinutes.Edited += () => { quickMinutes.Invalid = false; quickNote.Text = "Enter 1–1440 minutes"; quickNote.Color = Theme.Faint; quickNote.Invalidate(); };
            customTimerChip.MouseDown += (sender, e) =>
            {
                if (e.Button != MouseButtons.Left || customTimerList.Visible || customTimerList.JustClosed) return;
                customTimerList.Show(CustomTimerRows(), customTimerChip.PointToScreen(new Point(0, customTimerChip.Height)), false, true);
            };
            RefreshQuickTimers();
        }

        void AddQuickTimer()
        {
            if (!QuickTimers.TryMinutes(quickMinutes.Text.Trim(), out int minutes))
            {
                quickMinutes.Invalid = true;
                quickNote.Text = "Use a whole number from 1 to 1440 minutes.";
                quickNote.Color = Theme.DangerHi;
            }
            else if (!quickTimers.Add(minutes))
            {
                quickNote.Text = "That timer is already in the quick menu.";
                quickNote.Color = Theme.Second;
            }
            else
            {
                Settings.Set("quick_timers", quickTimers.Save());
                quickMinutes.Text = "";
                quickNote.Text = "Added " + QuickTimers.Label(minutes) + ".";
                quickNote.Color = Theme.Accent;
                RefreshQuickTimers();
            }
            quickNote.Invalidate();
        }

        List<PickRow> CustomTimerRows() => quickTimers.Custom.Select(m => new PickRow
        {
            Text = QuickTimers.Label(m), Right = "Remove", Click = () =>
            {
                quickTimers.Remove(m);
                Settings.Set("quick_timers", quickTimers.Save());
                RefreshQuickTimers();
            },
        }).ToList();

        void RefreshQuickTimers()
        {
            int count = quickTimers.Custom.Count();
            customTimerChip.Text = count == 0 ? "No custom timers" : $"Custom timers ({count})";
            customTimerChip.Enabled = count > 0;
            trayMenu.SetRows(TrayRows());
        }

        /// <summary>A card under the tabs, shown while its tab is active.</summary>
        Card AddCard(int tab, int y, int height)
        {
            var card = new Card();
            Place(card, Side, y, ContentW, height, settings);
            tabCards.Add((tab, card));
            return card;
        }

        void SettingsRow(Control card, Lbl title, Lbl note, Switch toggle, int y)
        {
            title.Under = note.Under = toggle.Under = Theme.Surface;
            Place(title, 20, y + 12, ContentW - 92, 20, card);
            Place(note, 20, y + 34, ContentW - 92, 18, card);
            toggle.Location = new Point(Theme.D(ContentW - 20) - toggle.Width, Theme.D(y + 20));
            card.Controls.Add(toggle);
            foreach (var label in new[] { title, note })
            {
                label.Cursor = Cursors.Hand;
                label.Click += (s, e) => toggle.Toggle();
            }
        }

        /// <summary>Make c the accent everywhere at once - ring, controls, tray and window icon - and save it.</summary>
        void ApplyAccent(Color c, bool fromField = false)
        {
            Theme.SetAccent(c);
            string hex = Theme.HexOf(c);
            Settings.Set("accent", hex);
            swatches.Selected = hex;
            if (!fromField) { accentEdit.Text = hex; accentEdit.Invalid = false; }
            AccentNote();
            iconKey = null;  // redraw the ring icons
            UpdateView();
            foreach (Form f in Application.OpenForms) Repaint(f);
        }

        static void Repaint(Control c) { c.Invalidate(); foreach (Control k in c.Controls) Repaint(k); }

        void AccentNote()
        {
            double cr = Math.Round(Theme.Contrast(Theme.Accent, Theme.Bg), 1);
            accentNote.Text = cr >= 3 ? "The ring, buttons, switches and the tray icon"
                                        : $"Low contrast ({cr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} : 1) - hard to read";
            accentNote.Color = cr >= 3 ? Theme.Faint : Theme.DangerHi;
            accentNote.Invalidate();
        }

        /// <summary>Palette / mixer helper above the custom colour field, centred on the window.</summary>
        void OpenColorHelper()
        {
            if (colorDrop.Visible || colorDrop.JustClosed) return;
            var grid = new ColorGrid(Theme.Accent);
            var drop = colorDrop.Show(grid, Theme.PopupShape(grid.Width, grid.Height));
            grid.Picked += (c, close) => { ApplyAccent(c); if (close) drop.Close(); else grid.Invalidate(); };
            var at = accentEdit.PointToScreen(Point.Empty);
            int left = PointToScreen(Point.Empty).X + (ClientSize.Width - grid.Width) / 2;
            drop.Show(new Point(left, at.Y - grid.Height - Theme.D(8)));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && settingsOpen) { ToggleSettings(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        void ToggleSettings()
        {
            if (fade.Running) return;
            bool open = !settingsOpen;
            Control from = open ? main : settings, to = open ? settings : main;
            gear.Active = open;
            gear.Invalidate();
            fade.Run(from, to, () =>
            {
                settingsOpen = open;
                to.BringToFront();
                gear.BringToFront();
                Defocus(this);
            });
            gear.BringToFront();
        }

        void BuildTray()
        {
            tray.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) ShowTrayMenu(); };
            tray.MouseDoubleClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };
            tray.BalloonTipClicked += (s, e) => ShowWindow();
        }

        void SyncMode(bool animate)
        {
            modeRow.SetClockMode(clockMode, animate);
            modeRow.Time.Minutes = clockMinute;
        }

        void SyncAction()
        {
            bool canForce = action.Key != "sleep";
            forceSwitch.Enabled = forceLabel.Enabled = forceNote.Enabled = canForce;
        }

        void SetAction(string key)
        {
            if (countdown.State != TimerState.Idle) return;
            action = PowerAction.Find(key);
            quickAction = action;
            picker.SetCurrent(key);
            Settings.Set("action", key);
            Settings.Set("quick_action", key);
            SyncAction();
            UpdateView();
        }

        void SetMode(bool clock)
        {
            if (countdown.State != TimerState.Idle) return;
            hourStop.Reset();
            clockMode = clock;
            if (clock && !clockTouched) clockMinute = TimeText.DefaultClock(DateTime.Now, countdown.Duration);
            Settings.Set("clock_mode", clock);
            SyncMode(true);
            UpdateView();
        }

        void AddTime(int delta)
        {
            double current = countdown.State == TimerState.Idle
                ? clockMode ? clockMinute * 60 : countdown.Duration : countdown.Remaining;
            double adjustment = hourStop.Adjust(current, delta, clock.Elapsed.TotalMilliseconds);
            if (adjustment == 0) return;
            if (countdown.State == TimerState.Idle)
            {
                if (clockMode)
                {
                    clockMinute = ((clockMinute + (int)(adjustment / 60)) % 1440 + 1440) % 1440;
                    clockTouched = true;
                    modeRow.Time.Minutes = clockMinute;
                }
                else
                {
                    countdown.SetDuration(countdown.Duration + (int)adjustment);
                    durationDirty = true;
                    wheelSave.Restart();
                }
            }
            else
            {
                countdown.Adjust(adjustment);
                if (countdown.Remaining > WarnSeconds) warned = false;
            }
            UpdateView(true);
        }

        void StartPause()
        {
            ring.FlushWheel();
            hourStop.Reset();
            SaveDuration();
            switch (countdown.State)
            {
                case TimerState.Running:
                    countdown.Pause();
                    break;
                case TimerState.Paused:
                    countdown.Resume();
                    break;
                default:
                    double seconds = countdown.Duration;
                    if (clockMode) seconds = Math.Max(1, (TimeText.NextOccurrence(DateTime.Now, clockMinute) - DateTime.Now).TotalSeconds);
                    else Settings.Set("duration", countdown.Duration);
                    countdown.Start(seconds);
                    warned = false;
                    break;
            }
            ApplyAwake();  // a running timer keeps the PC awake so it can finish; a paused one does not
            UpdateView();
        }

        void Cancel()
        {
            hourStop.Reset();
            countdown.Reset();
            ApplyAwake();
            UpdateView(true);
        }

        void SaveDuration()
        {
            if (!durationDirty) return;
            Settings.Set("duration", countdown.Duration);
            durationDirty = false;
        }

        void Tick()
        {
            if (durationDirty && wheelSave.ElapsedMilliseconds >= 250) SaveDuration();
            if (awakeMinutes > 0 && clock.Elapsed.TotalSeconds >= awakeEnd) SetAwake(0);
            if (countdown.Expired) { Fire(); return; }
            if (countdown.State == TimerState.Running && countdown.Remaining <= WarnSeconds && !warned)
            {
                warned = true;
                tray.ShowBalloonTip(10000, "Sleep Timer",
                    $"{Capitalize(action.RunTitle)} {TimeText.Clock(countdown.Remaining)}. Click to pause or cancel.", ToolTipIcon.Warning);
            }
            UpdateView();
        }

        static string Capitalize(string s) => s.Substring(0, 1) + s.Substring(1).ToLowerInvariant();

        void Fire()
        {
            ticker.Stop();
            countdown.Reset();
            ApplyAwake();
            ring.State = TimerState.Idle;
            ring.Urgent = false;
            ring.Tone = TimerTone.Idle;
            ring.Snap(0);
            ring.Title = action.FinalTitle;
            ring.Time = "00:00";
            ring.Sub = action.FinalSub;
            ring.Invalidate();
            ring.Interactive = false;
            tray.Text = "Sleep Timer - " + action.FinalSub.ToLowerInvariant();

            bool force = forceSwitch.On;
            if (action.Key == "sleep")
            {
                new Thread(() =>
                {
                    Exception failure = null;
                    try { Native.Sleep(force); } catch (Exception ex) { failure = ex; }
                    try
                    {
                        if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)(() =>
                        {
                            OnResumed();
                            if (failure != null && !IsDisposed)
                                tray.ShowBalloonTip(10000, "Sleep Timer", "Could not sleep: " + failure.Message, ToolTipIcon.Error);
                        }));
                    }
                    catch (InvalidOperationException) { }
                }) { IsBackground = true }.Start();
                return;
            }
            try { Native.Shutdown(action.Key == "restart", force); }
            catch (Exception ex)
            {
                OnResumed();
                tray.ShowBalloonTip(10000, "Sleep Timer", $"Could not {action.Verb}: {ex.Message}", ToolTipIcon.Error);
                return;
            }
            // A real shutdown closes this app. Still here after a minute: it was aborted (an app blocked it,
            // the user cancelled, policy denied it) - unfreeze the UI instead of showing "Shutting down..." forever.
            aborted?.Dispose();
            var resumeTimer = new System.Windows.Forms.Timer { Interval = 60000 };
            aborted = resumeTimer;
            resumeTimer.Tick += (s, e) =>
            {
                resumeTimer.Dispose();
                if (ReferenceEquals(aborted, resumeTimer)) { aborted = null; OnResumed(); }
            };
            resumeTimer.Start();
        }

        void OnResumed()
        {
            if (IsDisposed || Disposing) return;
            ring.Interactive = true;
            ticker.Start();
            UpdateView(true);
        }

        void SetAwake(int minutes)
        {
            awakeMinutes = minutes;
            awakeEnd = clock.Elapsed.TotalSeconds + minutes * 60;
            Settings.Set("awake", minutes < 0);
            ApplyAwake();
            UpdateView();
        }

        /// <summary>The one place that sets the execution state: the timer and keep awake share it.</summary>
        void ApplyAwake()
        {
            bool awake = awakeMinutes != 0;
            Native.KeepAwake(awake || countdown.State == TimerState.Running, awake && screenOn);
        }

        void UpdateView(bool animate = false)
        {
            var state = countdown.State;
            double seconds;
            string idleLabel = "";
            ring.State = state;

            if (state == TimerState.Idle)
            {
                if (clockMode)
                {
                    var target = TimeText.NextOccurrence(DateTime.Now, clockMinute);
                    seconds = Math.Max(0, (target - DateTime.Now).TotalSeconds);
                    ring.Title = action.ClockTitle;
                    ring.Time = TimeText.HourMinute(clockMinute);
                    ring.Sub = (target.Date > DateTime.Today ? "tomorrow, " : "") + "in " + TimeText.Human(seconds);
                    idleLabel = "at " + ring.Time;
                }
                else
                {
                    seconds = countdown.Duration;
                    ring.Title = "";
                    ring.Time = idleLabel = TimeText.Clock(seconds);
                    ring.Sub = action.Would + " " + DateTime.Now.AddSeconds(seconds).ToString("HH:mm");
                }
            }
            else
            {
                seconds = countdown.Remaining;
                ring.Time = TimeText.Clock(seconds);
                bool running = state == TimerState.Running;
                ring.Title = running ? action.RunTitle : "PAUSED";
                ring.Sub = running ? action.At + " " + DateTime.Now.AddSeconds(seconds).ToString("HH:mm") : "Resume to continue";
            }
            ring.Tone = TimerStatus.Tone(state, seconds);
            ring.SetTime(seconds, animate);
            ring.Urgent = state == TimerState.Running && seconds <= 10;

            bool idle = state == TimerState.Idle;
            modeRow.Mode.Enabled = modeRow.Time.Enabled = picker.Enabled = idle;
            ring.Invalidate();

            string awake = awakeMinutes < 0 ? "on" : awakeMinutes > 0 ? TimeText.Clock(awakeEnd - clock.Elapsed.TotalSeconds) : null;
            awakeChip.Text = awake == null ? "Off" : awakeMinutes < 0 ? "Indefinitely" : awake + " left";
            awakeChip.Active = awake != null;
            awakeList.SetRows(ChipRows());  // open lists follow along: ticking time, checks, Start/Pause
            trayMenu.SetRows(TrayRows());
            syncingAwake = true;
            awakeSwitch.On = awake != null;
            syncingAwake = false;
            string tip = state == TimerState.Running ? $"Sleep Timer · {TimeText.Clock(seconds)} left · {TimerStatus.Complete(countdown.Fraction)}% complete"
                       : state == TimerState.Paused ? $"Paused · {TimeText.Clock(seconds)} left · {TimerStatus.Complete(countdown.Fraction)}% complete"
                       : $"Sleep Timer - not started ({idleLabel})";
            if (awake != null) tip += "\nKeep awake: " + awake;
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;  // .NET Framework throws above 63
            UpdateIcons(idle ? 1 : countdown.Fraction, state, ring.Tone);
        }

        void UpdateIcons(double fraction, TimerState state, TimerTone tone)
        {
            string key = $"{state}|{tone}|{Math.Round(fraction * 48)}";
            if (key == iconKey) return;
            iconKey = key;
            IntPtr oldTray = trayIconHandle, oldWindow = windowIconHandle;
            using (var small = Theme.RingIcon(SystemInformation.SmallIconSize.Width, fraction, state, tone))
            using (var large = Theme.RingIcon(SystemInformation.IconSize.Width, fraction, state, tone))
            {
                trayIconHandle = small.GetHicon();
                windowIconHandle = large.GetHicon();
            }
            var oldTrayIcon = tray.Icon;
            var oldWindowIcon = Icon;
            tray.Icon = Icon.FromHandle(trayIconHandle);
            Icon = Icon.FromHandle(windowIconHandle);
            if (oldTray != IntPtr.Zero) oldTrayIcon?.Dispose();
            if (oldWindow != IntPtr.Zero) oldWindowIcon?.Dispose();
            tray.Visible = true;
            if (oldTray != IntPtr.Zero) Native.DestroyIcon(oldTray);
            if (oldWindow != IntPtr.Zero) Native.DestroyIcon(oldWindow);
        }

        public void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        void Quit()
        {
            quitting = true;
            if (countdown.State != TimerState.Idle) ShowWindow();
            Close();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitlebar(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            const int SysCommand = 0x0112, Minimize = 0xF020, KeyMenu = 0xF100;
            if (m.Msg == SysCommand && ((int)m.WParam & 0xFFF0) == Minimize) { Hide(); return; }
            // Alt is a fine-scroll modifier; releasing it must not activate the native menu.
            // A character in lParam (for example Alt+Space) still follows normal system-menu handling.
            if (m.Msg == SysCommand && ((int)m.WParam & 0xFFF0) == KeyMenu && m.LParam == IntPtr.Zero)
            { m.Result = IntPtr.Zero; return; }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !quitting)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            if (countdown.State != TimerState.Idle && e.CloseReason != CloseReason.WindowsShutDown)
                using (var dialog = new ConfirmDialog(action.Verb))
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        quitting = false;
                        e.Cancel = true;
                        return;
                    }
            base.OnFormClosing(e);
        }

        /// <summary>A left click anywhere except on a text field takes the focus out of the field.</summary>
        public bool PreFilterMessage(ref Message m)
        {
            const int WM_LBUTTONDOWN = 0x201;
            if (m.Msg == WM_LBUTTONDOWN && Control.FromHandle(m.HWnd) is Control c && !(c is TextBox) && !(c is Field)
                && c.FindForm() is Form f && f.ActiveControl is TextBox)
                Defocus(f);
            return false;
        }

        /// <summary>Park the focus on an invisible 0x0 control (ActiveControl = null lets WinForms pick the
        /// first text field again when the window is re-activated).</summary>
        public static void Defocus(Form f)
        {
            var sink = f.Controls["focusSink"];
            if (sink == null) { sink = new Control { Name = "focusSink", Size = Size.Empty, TabStop = false }; f.Controls.Add(sink); }
            f.ActiveControl = sink;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                ring.FlushWheel(); SaveDuration();
                Application.RemoveMessageFilter(this);
                ticker.Dispose(); aborted?.Dispose(); tooltips.Dispose();
                trayMenu.Dispose(); awakeList.Dispose(); customTimerList.Dispose(); colorDrop.Dispose();
                tray.Visible = false;
                if (trayIconHandle != IntPtr.Zero) tray.Icon?.Dispose();
                if (windowIconHandle != IntPtr.Zero) Icon?.Dispose();
                tray.Dispose();
                if (trayIconHandle != IntPtr.Zero) { Native.DestroyIcon(trayIconHandle); trayIconHandle = IntPtr.Zero; }
                if (windowIconHandle != IntPtr.Zero) { Native.DestroyIcon(windowIconHandle); windowIconHandle = IntPtr.Zero; }
                Native.KeepAwake(false, false);
            }
            base.Dispose(disposing);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Application.RemoveMessageFilter(this);
            Native.KeepAwake(false, false);
            tray.Visible = false;
            base.OnFormClosed(e);
            Application.ExitThread();
        }
    }

    sealed class ConfirmDialog : Form
    {
        public ConfirmDialog(string verb)
        {
            Text = "Timer active";
            BackColor = Theme.Bg;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = MaximizeBox = ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Theme.D(460), Theme.D(144));

            var title = new Lbl { Text = "Cancel the timer?", Color = Theme.Text, Font = Theme.Ui(11f, 600) };
            var body = new Lbl { Text = $"Closing the app stops the timer, so your PC won't {verb}." };
            var close = new Btn("Close anyway", BtnKind.Danger);
            var keep = new Btn("Keep timer", BtnKind.Primary);
            title.SetBounds(Theme.D(20), Theme.D(20), Theme.D(420), Theme.D(28));
            body.SetBounds(Theme.D(20), Theme.D(52), Theme.D(420), Theme.D(20));
            keep.SetBounds(Theme.D(310), Theme.D(88), Theme.D(130), Theme.D(36));
            close.SetBounds(Theme.D(172), Theme.D(88), Theme.D(130), Theme.D(36));
            keep.Click += (s, e) => DialogResult = DialogResult.Cancel;
            close.Click += (s, e) => DialogResult = DialogResult.OK;
            Controls.AddRange(new Control[] { title, body, close, keep });
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keys)
        {
            if (keys == Keys.Enter || keys == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
            return base.ProcessCmdKey(ref msg, keys);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitlebar(Handle);
        }
    }
}
