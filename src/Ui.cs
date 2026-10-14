using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace SleepTimer
{
    static class Motion
    {
        public const int Progress = 100, Press = 90, Switch = 240, SwitchPress = 140,
                         Tabs = 220, Selection = 320, Reveal = 160;
    }

    static class Theme
    {
        public static Color Blend(Color a, Color b, double t) => Color.FromArgb(
            (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));

        public static Color Alpha(Color c, double a) => Color.FromArgb((int)Math.Round(255 * Math.Max(0, Math.Min(1, a))), c);

        // Background and text are fixed; every neutral is a blend of the two. The accent is the user's choice
        // (SetAccent) and its shades are recomputed from it.
        public static readonly Color Bg = ColorTranslator.FromHtml("#111111"), Text = ColorTranslator.FromHtml("#EDEDED"),
            GithubGreen = ColorTranslator.FromHtml("#0FBF3E");
        public static readonly Color Surface = Blend(Bg, Text, 0.05), Surface2 = Blend(Bg, Text, 0.09),
            Border = Blend(Bg, Text, 0.15), Disabled = Blend(Bg, Text, 0.24), Faint = Blend(Bg, Text, 0.40),
            Second = Blend(Bg, Text, 0.58), TrayTrack = Blend(Bg, Text, 0.30), MenuEdge = Blend(Border, Disabled, 0.8);
        public const string DefaultAccent = "#0CD29D";
        public static Color Accent, OnAccent, AccentHi, AccentLo, AccentLine, Soft, SoftHi, SoftLine, CurrentRow, Dot,
            Danger, DangerHi, DangerSoft, Warn;
        static Theme() { SetAccent(ColorTranslator.FromHtml(DefaultAccent)); }

        /// <summary>Recompute every accent shade. Warm accents (red, orange, amber, pink) would blend with the
        /// salmon error colour, so errors turn a clearer, purer red with them.</summary>
        public static void SetAccent(Color a)
        {
            Accent = Color.FromArgb(a.R, a.G, a.B);
            OnAccent = OnColor(Accent);
            AccentHi = Blend(Accent, Color.White, 0.22); AccentLo = Blend(Accent, Bg, 0.18); AccentLine = Blend(Bg, Accent, 0.50);
            Soft = Blend(Surface, Accent, 0.18); SoftHi = Blend(Surface, Accent, 0.28); SoftLine = Blend(Border, Accent, 0.60);
            CurrentRow = Blend(Surface, Accent, 0.06); Dot = Blend(Accent, Color.White, 0.55);
            float hue = Accent.GetHue();
            bool warm = Accent.GetSaturation() > 0.25f && (hue < 60 || hue >= 300);
            Danger = ColorTranslator.FromHtml(warm ? "#FF3B30" : "#E0685C");
            Warn = ColorTranslator.FromHtml(hue >= 25 && hue <= 65 && Accent.GetSaturation() > 0.25f ? "#F2D64B" : "#FFB142");
            DangerHi = Blend(Danger, Color.White, 0.30); DangerSoft = Blend(Bg, Danger, 0.14);
        }

        public static Color TimerColor(TimerTone tone) => tone == TimerTone.Idle ? Blend(Bg, Accent, 0.55)
            : tone == TimerTone.Paused ? Second : tone == TimerTone.Warning ? Warn : tone == TimerTone.Urgent ? Danger : Accent;

        /// <summary>Text colour on a fill of c: dark or white, whichever reads better.</summary>
        public static Color OnColor(Color c) => Contrast(c, Bg) >= Contrast(c, Color.White) ? Bg : Color.White;

        /// <summary>WCAG contrast ratio of two colours (1 to 21).</summary>
        public static double Contrast(Color a, Color b)
        {
            double L(Color c)
            {
                double Ch(int v) { double x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
                return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
            }
            double p = L(a), q = L(b);
            return (Math.Max(p, q) + 0.05) / (Math.Min(p, q) + 0.05);
        }

        public static void DisposeFonts() { foreach (var font in cache.Values) font.Dispose(); cache.Clear(); }

        public static string HexOf(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        /// <summary>DPI scale, 1 = 96 dpi. All layout is written in 96-dpi pixels and passed through <see cref="D"/>.</summary>
        public static float S = 1f;
        public static int D(double v) => (int)Math.Round(v * S);

        static string textFamily, displayFamily, monoFamily;
        static HashSet<string> installed;
        static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();

        public static void Init()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) S = g.DpiX / 96f;
            installed = new HashSet<string>(new InstalledFontCollection().Families.Select(f => f.Name));
            textFamily = First("Segoe UI Variable Text", "Segoe UI");
            displayFamily = First("Segoe UI Variable Display", "Segoe UI");
            monoFamily = First("Cascadia Code", "Cascadia Mono", "Consolas");
        }

        static string First(params string[] names) => names.FirstOrDefault(installed.Contains) ?? FontFamily.GenericSansSerif.Name;

        /// <summary>Weight 400 regular, 600 semibold, 700 bold.</summary>
        public static Font Ui(float pt, int weight = 400) => Get(textFamily, pt, weight);
        public static Font Display(float pt, int weight = 400) => Get(displayFamily, pt, weight);
        public static Font Mono(float pt, int weight = 400) => Get(monoFamily, pt, weight);

        static Font Get(string family, float pt, int weight)
        {
            string key = $"{family}|{pt}|{weight}";
            if (cache.TryGetValue(key, out Font f)) return f;
            var style = weight >= 600 ? FontStyle.Bold : FontStyle.Regular;
            if (weight == 600 && installed.Contains(family + " Semibold")) { family += " Semibold"; style = FontStyle.Regular; }
            return cache[key] = new Font(family, pt, style);
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, RectangleF r, float radius, Color fill, Color? border = null)
        {
            using (var path = Round(r, radius))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (border.HasValue) using (var pen = new Pen(border.Value, Theme.S)) g.DrawPath(pen, path);
            }
        }

        public const int MenuRadius = 10;

        public static GraphicsPath PopupShape(int width, int height) =>
            Round(new RectangleF(0, 0, width, height), Math.Min(MenuRadius * S, height / 2f));

        public static void PaintPopup(Graphics g, int width, int height)
        {
            // Keep antialiased ink inside the hard native Region; concentric curves share one radius.
            float inset = 1.5f * S;
            var bounds = new RectangleF(inset, inset, width - 2 * inset, height - 2 * inset);
            FillRound(g, bounds, Math.Max(0, Math.Min(MenuRadius * S, height / 2f) - inset), Surface, MenuEdge);
        }

        // Text goes through GDI (TextRenderer): it lays out and renders with the same hinted glyph widths, like
        // every Windows app. GDI+ typographic layout uses unhinted widths, so ClearType-hinted letters touch at
        // small sizes. GDI has no transparency: callers blend translucent text onto its background themselves.
        const TextFormatFlags Flat = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix
                                   | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform;

        public static SizeF Measure(Graphics g, string s, Font f) =>
            TextRenderer.MeasureText(g, s, f, new Size(int.MaxValue, int.MaxValue), Flat);

        /// <summary>One line of text in r: aligned, optionally ending in "..." when it does not fit.</summary>
        public static void Draw(Graphics g, string s, Font f, Color c, RectangleF r,
            StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center, bool ellipsis = false)
        {
            var flags = Flat
                | (h == StringAlignment.Center ? TextFormatFlags.HorizontalCenter : h == StringAlignment.Far ? TextFormatFlags.Right : TextFormatFlags.Left)
                | (v == StringAlignment.Center ? TextFormatFlags.VerticalCenter : v == StringAlignment.Far ? TextFormatFlags.Bottom : TextFormatFlags.Top)
                | (ellipsis ? TextFormatFlags.EndEllipsis : 0);
            TextRenderer.DrawText(g, s, f, Rectangle.Round(r), Color.FromArgb(c.R, c.G, c.B), flags);
        }

        /// <summary>Centered text with extra letter spacing (GDI+ has no letter-spacing option).</summary>
        public static void DrawSpaced(Graphics g, string s, Font f, Color c, RectangleF r, float spacing)
        {
            var widths = s.Select(ch => Measure(g, ch.ToString(), f).Width).ToArray();
            float x = r.X + (r.Width - widths.Sum() - spacing * Math.Max(0, s.Length - 1)) / 2;
            for (int i = 0; i < s.Length; i++)
            {
                Draw(g, s[i].ToString(), f, c, new RectangleF(x, r.Y, widths[i] + 4, r.Height));
                x += widths[i] + spacing;
            }
        }

        /// <summary>Tray / window icon: idle = empty ring, running = accent arc, paused = grey, last seconds = red.</summary>
        public static Bitmap RingIcon(int size, double fraction, TimerState state, TimerTone tone)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float k = size / 64f, w = 9 * k;
                var r = new RectangleF(9 * k, 9 * k, 46 * k, 46 * k);
                using (var pen = new Pen(state == TimerState.Idle ? TimerColor(TimerTone.Idle) : TrayTrack, w)) g.DrawEllipse(pen, r);
                if (state != TimerState.Idle && fraction > 0.001)
                    using (var pen = new Pen(TimerColor(tone), w) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                        g.DrawArc(pen, r, -90, (float)(fraction * 360));
                if (state == TimerState.Paused)
                {
                    using (var pen = new Pen(Second, 6 * k) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        g.DrawLine(pen, 26 * k, 25 * k, 26 * k, 39 * k);
                        g.DrawLine(pen, 38 * k, 25 * k, 38 * k, 39 * k);
                    }
                }
            }
            return bmp;
        }
    }

    /// <summary>Eased (out-cubic) value animation driven by a UI timer. While it runs the system timer
    /// resolution is 1 ms; animation frames are capped at approximately 60 Hz.</summary>
    sealed class Tween : IDisposable
    {
        readonly Timer timer = new Timer { Interval = 16 };
        bool disposed;
        readonly Stopwatch clock = new Stopwatch();
        readonly int duration;
        readonly Action changed;
        readonly bool linear;
        double from, to;

        public Tween(int durationMs, double value, Action changed, bool linear = false)
        {
            duration = durationMs;
            this.linear = linear;
            Value = to = value;
            this.changed = changed;
            timer.Tick += (s, e) => Step();
        }

        public double Value { get; private set; }
        public bool Running => timer.Enabled;

        public void To(double target)
        {
            if (disposed) return;
            if (Running) Sample();
            from = Value;
            to = target;
            clock.Restart();
            if (!timer.Enabled) { Native.HiResTimer(true); timer.Start(); }
        }

        public void Set(double value)
        {
            if (disposed || (!Running && Value == value)) return;
            Stop();
            Value = to = value;
            changed();
        }

        void Stop()
        {
            if (!timer.Enabled) return;
            timer.Stop();
            Native.HiResTimer(false);
        }

        public void Dispose() { if (disposed) return; Stop(); disposed = true; timer.Dispose(); }

        void Sample()
        {
            double t = Math.Min(1, clock.Elapsed.TotalMilliseconds / duration);
            Value = from + (to - from) * (linear ? t : 1 - Math.Pow(1 - t, 3));
            if (t >= 1) Stop();
        }

        void Step()
        {
            if (disposed) return;
            Sample();
            changed();
        }
    }

    class Painted : Control
    {
        readonly List<IDisposable> owned = new List<IDisposable>();
        protected T Own<T>(T resource) where T : IDisposable { owned.Add(resource); return resource; }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { foreach (var resource in owned) resource.Dispose(); owned.Clear(); }
            base.Dispose(disposing);
        }

        public Color Under = Theme.Bg;
        protected bool Hot, Down;

        public Painted()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        protected override void OnMouseEnter(EventArgs e) { Hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hot = false; Down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        /// <summary>WinForms raises Click for the right button too; here only a left click is a click.</summary>
        protected static bool LeftClick(EventArgs e) => !(e is MouseEventArgs m) || m.Button == MouseButtons.Left;
        protected override void OnClick(EventArgs e) { if (LeftClick(e)) base.OnClick(e); }

        protected Graphics Prep(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            if (Under.A > 0) g.Clear(Under);
            return g;
        }

        protected RectangleF Box => new RectangleF(Theme.S / 2, Theme.S / 2, Width - Theme.S, Height - Theme.S);

        protected override void OnPaint(PaintEventArgs e) => Prep(e);
    }

    class Lbl : Painted
    {
        public Color Color = Theme.Second;
        public StringAlignment Align = StringAlignment.Near;

        public Lbl() { Font = Theme.Ui(10.5f); }

        protected override void OnPaint(PaintEventArgs e) =>
            Theme.Draw(Prep(e), Text, Font, Enabled ? Color : Theme.Disabled, new RectangleF(0, 0, Width, Height), Align);
    }

    /// <summary>Button with a text and a chevron that opens a list; its corners match the list it turns into.</summary>
    class Chip : Painted
    {
        bool active;

        public Chip() { Cursor = Cursors.Hand; }

        /// <summary>Accent text while what it shows is switched on.</summary>
        public bool Active { get => active; set { if (value != active) { active = value; Invalidate(); } } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            float h = Height, s = Theme.S;
            Theme.FillRound(g, Box, Height / 2f, active ? (Hot ? Theme.SoftHi : Theme.Soft) : Hot ? Theme.Surface2 : Theme.Surface,
                            active ? Theme.SoftLine : (Color?)null);
            var col = !Enabled ? Theme.Disabled : active ? Theme.AccentHi : Hot ? Theme.Text : Theme.Second;
            Theme.Draw(g, Text, Theme.Ui(10.5f), col, new RectangleF(Theme.D(16), 0, Width - Theme.D(40), h));
            float cx = Width - Theme.D(18), cy = h / 2;
            using (var pen = new Pen(col, 1.6f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { new PointF(cx - 4 * s, cy - 2 * s), new PointF(cx, cy + 2 * s), new PointF(cx + 4 * s, cy - 2 * s) });
        }
    }

    enum BtnKind { Primary, Danger }

    class Btn : Painted
    {
        public BtnKind Kind;
        readonly Tween press;

        public Btn(string text, BtnKind kind)
        {
            Text = text;
            Kind = kind;
            press = Own(new Tween(Motion.Press, 0, Invalidate, linear: true));
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (Enabled && e.Button == MouseButtons.Left) press.To(1); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); press.To(0); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); press.To(0); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            Color bg, fg;
            if (Kind == BtnKind.Primary)
            {
                if (!Enabled) { bg = Theme.Surface2; fg = Theme.Disabled; }
                else { bg = Down ? Theme.AccentLo : Hot ? Theme.AccentHi : Theme.Accent; fg = Theme.OnAccent; }
            }
            else if (!Enabled) { bg = Theme.Bg; fg = Theme.Disabled; }
            else { bg = Theme.Blend(Theme.Surface, Theme.Danger, Down ? 0.12 : Hot ? 0.28 : 0.18); fg = Theme.DangerHi; }

            var face = Box;
            face.Inflate(-(float)press.Value * Width * 0.015f, -(float)press.Value * Height * 0.015f);
            Theme.FillRound(g, face, Height >= Theme.D(36) ? Height / 2f : Theme.D(8), bg);
            var font = Kind == BtnKind.Primary ? Theme.Ui(10.5f, 600) : Theme.Ui(10.5f);
            Theme.Draw(g, Text, font, fg, new RectangleF(0, 0, Width, Height), StringAlignment.Center);
        }
    }

    class Switch : Painted
    {
        readonly Tween knob, press;
        bool on;

        public Switch(bool on)
        {
            this.on = on;
            knob = Own(new Tween(Motion.Switch, on ? 1 : 0, Invalidate));
            press = Own(new Tween(Motion.SwitchPress, 0, Invalidate));
            Cursor = Cursors.Hand;
            Size = new Size(Theme.D(44), Theme.D(24));
        }

        public event Action<bool> Toggled;

        public bool On
        {
            get => on;
            set
            {
                if (value == on) return;
                on = value;
                knob.To(on ? 1 : 0);
                Toggled?.Invoke(on);
            }
        }

        public void Toggle() { if (Enabled) On = !On; }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (Enabled && e.Button == MouseButtons.Left) press.To(1); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); press.To(0); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); press.To(0); }

        protected override void OnClick(EventArgs e) { if (LeftClick(e)) Toggle(); base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            double p = knob.Value, a = Enabled ? 1 : 0.35;
            var r = new RectangleF(0, 0, Width, Height);
            var off = Theme.Blend(Theme.Bg, Theme.Text, Hot ? 0.18 : 0.13);
            Theme.FillRound(g, r, r.Height / 2, Theme.Blend(Under, Theme.Blend(off, Theme.Accent, p), a));
            float inset = 2 * Theme.S, d = Height - 2 * inset;
            float width = (float)SwitchGeometry.ThumbWidth(Width, Height, Theme.S, press.Value);
            float x = inset + (float)p * (Width - 2 * inset - width);
            var thumb = new RectangleF(x, inset, width, d);
            for (int blur = 4; blur >= 1; blur--)
            {
                var shadow = thumb; shadow.Offset(0, 1.5f * Theme.S);
                shadow.Inflate(blur * Theme.S / 2, blur * Theme.S / 2);
                Theme.FillRound(g, shadow, shadow.Height / 2, Theme.Alpha(Color.Black, 0.025 * a));
            }
            Theme.FillRound(g, thumb, d / 2, Theme.Blend(Under, Color.White, a));
        }
    }

    /// <summary>
    /// Countdown ring with title, time and subtitle. Left click starts or pauses, right click cancels,
    /// the mouse wheel adjusts the time.
    /// </summary>
    class Ring : Painted
    {
        /// <summary>Minutes per wheel notch, fine to coarse.</summary>
        public static readonly (string keys, int minutes)[] WheelSteps =
            { ("Scroll", 5), ("Alt", 1), ("Shift", 15), ("Ctrl", 30), ("Ctrl+Shift", 60) };

        readonly Tween sweep, hours;
        readonly WheelInput wheelInput = new WheelInput();
        readonly Timer wheelFrame;
        long pendingWheel;
        bool hot;

        public TimerState State;
        public bool Urgent, Interactive = true;
        public TimerTone Tone = TimerTone.Idle;
        public string Title = "", Time = "", Sub = "";
        TimerDial dial = TimerDial.For(1800);

        public void SetTime(double seconds, bool animate)
        {
            bool motion = Visible && (animate || State == TimerState.Running);
            var next = TimerDial.For(seconds);
            bool hourChanged = next.ExtraHours != dial.ExtraHours;
            if (Math.Abs(hours.Value - next.HourFill) > 0.000001)
            {
                if (motion) hours.To(next.HourFill);
                else hours.Set(next.HourFill);
            }
            dial = next;
            double fraction = dial.Fraction(seconds);
            // Snap across the hour boundary rather than sweeping backwards around the dial.
            if (hourChanged && !animate) sweep.Set(fraction);
            else SetFraction(fraction, motion);
        }

        public Ring()
        {
            sweep = Own(new Tween(160, 1, Invalidate, linear: true));
            hours = Own(new Tween(Motion.Progress, 0, Invalidate, linear: true));
            wheelFrame = Own(new Timer { Interval = 16 });
            wheelFrame.Tick += (s, e) => FlushWheel();
        }

        public event Action<int> Wheel;
        public event Action ToggleClicked, CancelClicked;

        public void SetFraction(double f, bool animate)
        {
            if (animate) sweep.To(f);
            else if (!sweep.Running) sweep.Set(f);
        }

        public void Snap(double f) => sweep.Set(f);

        float Side => Math.Min(Width, Height) - Theme.D(40);
        PointF Center => new PointF(Width / 2f, Height / 2f);

        /// <summary>Top of the slot inside the ring where the action picker sits.</summary>
        public int SlotTop => (int)Center.Y + Theme.D(6);

        bool OnRing(Point p)
        {
            float dx = p.X - Center.X, dy = p.Y - Center.Y, r = Side / 2 + 18 * Theme.S;
            return Interactive && dx * dx + dy * dy <= r * r;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool h = OnRing(e.Location);
            if (h != hot) { hot = h; Cursor = h ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hot = false; base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!OnRing(e.Location)) return;
            FlushWheel();
            if (e.Button == MouseButtons.Left) ToggleClicked?.Invoke();
            else if (e.Button == MouseButtons.Right && State != TimerState.Idle) CancelClicked?.Invoke();
        }

        /// <summary>Children (the action picker) draw the ring as their background, so they repaint with it.</summary>
        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            base.OnInvalidated(e);
            foreach (Control c in Controls) c.Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (!Interactive) return;
            var m = ModifierKeys;
            bool ctrl = (m & Keys.Control) != 0, shift = (m & Keys.Shift) != 0, alt = (m & Keys.Alt) != 0;
            string keys = ctrl && shift ? "Ctrl+Shift" : ctrl ? "Ctrl" : shift ? "Shift" : alt ? "Alt" : "Scroll";
            pendingWheel += wheelInput.Add(e.Delta, WheelSteps.First(s => s.keys == keys).minutes * 60);
            if (pendingWheel != 0 && !wheelFrame.Enabled) wheelFrame.Start();
        }

        public void FlushWheel()
        {
            wheelFrame.Stop();
            int delta = (int)Math.Max(-Countdown.MaxSet, Math.Min(Countdown.MaxSet, pendingWheel));
            pendingWheel = 0;
            if (Interactive && delta != 0) Wheel?.Invoke(delta);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            float side = Side, stroke = 16 * Theme.S;
            var c = Center;
            var rect = new RectangleF(c.X - side / 2, c.Y - side / 2, side, side);
            using (var pen = new Pen(hot ? Theme.Border : Theme.Surface2, stroke)) g.DrawEllipse(pen, rect);
            var outline = rect; outline.Inflate(stroke / 2 + 2 * Theme.S, stroke / 2 + 2 * Theme.S);
            using (var pen = new Pen(Theme.Border, Theme.S)) g.DrawEllipse(pen, outline);

            Color c1 = Theme.TimerColor(Tone);

            double shown = sweep.Value;
            if (shown > 0.001) DrawArc(g, rect, shown, stroke, c1);
            DrawTicks(g, c, side / 2, c1);
            DrawHours(g, c, side / 2 - stroke / 2 - 8 * Theme.S, c1);

            Theme.DrawSpaced(g, Title, Theme.Ui(9f, 600), Theme.Second, new RectangleF(rect.X, c.Y - Theme.D(96), side, Theme.D(24)), 2 * Theme.S);
            Theme.Draw(g, Time, Theme.Display(34f, 400), Theme.Text, new RectangleF(rect.X, c.Y - Theme.D(72), side, Theme.D(72)), StringAlignment.Center);
            Theme.Draw(g, Sub, Theme.Ui(10.5f), Theme.Second, new RectangleF(rect.X, c.Y + Theme.D(48), side, Theme.D(24)), StringAlignment.Center);
        }

        void DrawTicks(Graphics g, PointF center, float radius, Color active)
        {
            for (int i = 0; i < dial.Ticks; i++)
            {
                double angle = (i / (double)dial.Ticks * 360 - 90) * Math.PI / 180;
                bool major = i % dial.MajorEvery == 0;
                float length = (major ? 6 : 3) * Theme.S;
                float inner = radius + 12 * Theme.S, outer = inner + length;
                var color = Theme.Blend(Theme.Bg, active, major ? 0.65 : 0.28);
                using (var pen = new Pen(color, (major ? 1.7f : 1f) * Theme.S) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(pen, center.X + inner * (float)Math.Cos(angle), center.Y + inner * (float)Math.Sin(angle),
                                   center.X + outer * (float)Math.Cos(angle), center.Y + outer * (float)Math.Sin(angle));
            }
        }

        void DrawHours(Graphics g, PointF center, float radius, Color color)
        {
            float half = 5 * Theme.S;
            var outer = new RectangleF(center.X - radius - half, center.Y - radius - half, 2 * (radius + half), 2 * (radius + half));
            var inner = new RectangleF(center.X - radius + half, center.Y - radius + half, 2 * (radius - half), 2 * (radius - half));
            // Adjacent annular rectangles share radial edges: squared ends, no pill caps or gaps.
            for (int i = 0; i < (int)Math.Ceiling(hours.Value); i++)
            {
                double opacity = Math.Min(1, hours.Value - i);
                float start = -90 + i * 15;
                using (var path = new GraphicsPath())
                {
                    path.AddArc(outer, start, 15);
                    path.AddArc(inner, start + 15, -15);
                    path.CloseFigure();
                    using (var fill = new SolidBrush(Theme.Blend(Theme.Bg, color, opacity))) g.FillPath(fill, path);
                    using (var edge = new Pen(Theme.Blend(Theme.Bg, Theme.Blend(Theme.Border, color, 0.3), opacity), Theme.S)
                           { LineJoin = LineJoin.Miter }) g.DrawPath(edge, path);
                }
            }
        }

        /// <summary>Squared ends match the outlined hour segments.</summary>
        void DrawArc(Graphics g, RectangleF rect, double shown, float stroke, Color color)
        {
            using (var pen = new Pen(color, stroke) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
            {
                if (shown >= 1) g.DrawEllipse(pen, rect);
                else g.DrawArc(pen, rect, -90, (float)(shown * 360));
            }
        }
    }

    /// <summary>Chip showing what happens at zero. Click expands it into all options; any click collapses it.</summary>
    class ActionPicker : Painted, IMessageFilter
    {
        readonly PowerAction[] options;
        readonly float[] textWidths, segments;
        readonly float expandedWidth;
        readonly Tween open;
        bool isOpen;
        int hover = -1;

        public ActionPicker(PowerAction[] options, string current)
        {
            this.options = options;
            Current = current;
            open = Own(new Tween(Motion.Tabs, 0, Invalidate));
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                textWidths = options.Select(o => Theme.Measure(g, o.Label, Theme.Ui(10.5f)).Width).ToArray();
            segments = textWidths.Select(w => w + Theme.D(26)).ToArray();
            expandedWidth = segments.Sum() + Theme.D(8);
            Size = new Size((int)expandedWidth + 2, Theme.D(32));
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Under = Color.Transparent;
        }

        public string Current { get; private set; }

        public void SetCurrent(string key)
        {
            if (!options.Any(o => o.Key == key) || Current == key) return;
            Current = key;
            SetOpen(false);
            Invalidate();
        }
        public event Action<string> Changed;

        int Index => Array.FindIndex(options, o => o.Key == Current);

        void SetOpen(bool on)
        {
            if (on == isOpen) return;
            isOpen = on;
            if (on) Application.AddMessageFilter(this);
            else Application.RemoveMessageFilter(this);
            open.To(on ? 1 : 0);
        }

        (float x, float w) Geometry()
        {
            float collapsed = textWidths[Index] + Theme.D(46);
            float w = collapsed + (expandedWidth - collapsed) * (float)open.Value;
            return ((Width - w) / 2, w);
        }

        int SegmentAt(float x)
        {
            float sx = Geometry().x + Theme.D(4);
            for (int i = 0; i < segments.Length; sx += segments[i++])
                if (x >= sx && x < sx + segments[i]) return i;
            return -1;
        }

        bool InPill(float x) { var (x0, w) = Geometry(); return x >= x0 && x <= x0 + w; }

        public bool PreFilterMessage(ref Message m)
        {
            const int LButtonDown = 0x201, RButtonDown = 0x204, NcLButtonDown = 0xA1;
            if ((m.Msg == LButtonDown || m.Msg == RButtonDown || m.Msg == NcLButtonDown)
                && !RectangleToScreen(ClientRectangle).Contains(MousePosition))
                SetOpen(false);
            return false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = isOpen ? SegmentAt(e.X) : InPill(e.X) ? 0 : -1;
            if (h != hover) { hover = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (!isOpen) { if (InPill(e.X)) SetOpen(true); return; }
            int i = SegmentAt(e.X);
            if (i >= 0 && options[i].Key != Current) { Current = options[i].Key; Changed?.Invoke(Current); }
            SetOpen(false);
        }

        protected override void OnEnabledChanged(EventArgs e) { if (!Enabled) SetOpen(false); base.OnEnabledChanged(e); }
        protected override void OnHandleDestroyed(EventArgs e) { Application.RemoveMessageFilter(this); base.OnHandleDestroyed(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            float h = Height, p = (float)open.Value;
            var (x0, w) = Geometry();
            var rect = new RectangleF(x0 + 0.5f, 0.5f, w - 1, h - 1);
            bool hotChip = Enabled && !isOpen && hover >= 0;

            // the chip sits on the ring's background (Bg); every colour is blended onto it, as GDI text is opaque
            var fill = Theme.Blend(Theme.Surface, Theme.Surface2, hotChip ? 1 : p);
            Theme.FillRound(g, rect, h / 2, fill);

            double collapsedAlpha = 1 - Math.Min(1, p * 2);
            if (collapsedAlpha > 0)
            {
                var col = Theme.Blend(fill, !Enabled ? Theme.Disabled : hotChip ? Theme.Text : Theme.Second, collapsedAlpha);
                Theme.Draw(g, options[Index].Label, Theme.Ui(10.5f), col, new RectangleF(x0 + Theme.D(16), 0, w, h));
                float cx = x0 + w - Theme.D(18), cy = h / 2, s = Theme.S;
                using (var pen = new Pen(col, 1.6f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(pen, new[] { new PointF(cx - 4 * s, cy - 2 * s), new PointF(cx, cy + 2 * s), new PointF(cx + 4 * s, cy - 2 * s) });
            }

            double expandedAlpha = Math.Max(0, Math.Min(1, p * 2 - 1));
            if (expandedAlpha <= 0) return;
            using (var clip = Theme.Round(rect, h / 2)) g.SetClip(clip);
            float sx = x0 + Theme.D(4), inset = Theme.D(4);
            for (int i = 0; i < options.Length; sx += segments[i++])
            {
                var seg = new RectangleF(sx, inset, segments[i], h - 2 * inset);
                bool current = options[i].Key == Current;
                var under = current ? Theme.Blend(fill, Theme.Accent, 0.16 * expandedAlpha) : fill;
                if (current) Theme.FillRound(g, seg, seg.Height / 2, under);
                var col = current ? Theme.AccentHi : i == hover ? Theme.Text : Theme.Second;
                Theme.Draw(g, options[i].Label, Theme.Ui(10.5f), Theme.Blend(under, col, expandedAlpha), seg, StringAlignment.Center);
            }
        }
    }

    /// <summary>
    /// HH:mm input. Click a part to select it, type digits, use Up/Down or the mouse wheel.
    /// Owner drawn so it can fade in and out with the mode row.
    /// </summary>
    class TimeBox : Painted
    {
        int minutes, part, pendingDigit = -1;
        bool editing;
        public double Opacity = 1;

        public TimeBox()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.IBeam;
        }

        public event Action Changed;

        public int Minutes
        {
            get => minutes;
            set { minutes = (value % 1440 + 1440) % 1440; Invalidate(); }
        }

        void Commit(int value) { Minutes = value; Changed?.Invoke(); }

        int H => minutes / 60;
        int M => minutes % 60;

        void Step(int delta)
        {
            pendingDigit = -1;
            Commit(part == 0 ? ((H + delta) % 24 + 24) % 24 * 60 + M : H * 60 + ((M + delta) % 60 + 60) % 60);
        }

        void TypeDigit(int d)
        {
            int max = part == 0 ? 23 : 59;
            int value = pendingDigit < 0 ? d : Math.Min(max, pendingDigit * 10 + d);
            bool done = pendingDigit >= 0 || d * 10 > max;
            Commit(part == 0 ? value * 60 + M : H * 60 + value);
            pendingDigit = done ? -1 : d;
            if (done && part == 0) part = 1;
        }

        protected override bool IsInputKey(Keys k) =>
            k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right || base.IsInputKey(k);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up) Step(1);
            else if (e.KeyCode == Keys.Down) Step(-1);
            else if (e.KeyCode == Keys.Left) { part = 0; pendingDigit = -1; }
            else if (e.KeyCode == Keys.Right) { part = 1; pendingDigit = -1; }
            else return;
            editing = e.Handled = true;
            Invalidate();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (char.IsDigit(e.KeyChar)) TypeDigit(e.KeyChar - '0');
            else if (e.KeyChar == ':' || e.KeyChar == '.') { part = 1; pendingDigit = -1; }
            else return;
            editing = e.Handled = true;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            editing = true;
            part = e.X < Width / 2 ? 0 : 1;
            pendingDigit = -1;
            base.OnMouseDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            part = e.X < Width / 2 ? 0 : 1;
            Step(Math.Sign(e.Delta));
        }

        protected override void OnLostFocus(EventArgs e) { editing = false; pendingDigit = -1; Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            if (Opacity <= 0) return;
            double a = Opacity;
            Color border = !Enabled ? Theme.Border : editing ? Theme.Accent : Hot ? Theme.Disabled : Theme.Border;
            // fades in over Bg: colours are blended onto it (GDI text has no transparency)
            var fill = Theme.Blend(Theme.Bg, Enabled ? Theme.Surface : Theme.Bg, a);
            Theme.FillRound(g, Box, Theme.D(8), fill, Theme.Blend(Theme.Bg, border, a));

            var font = Theme.Ui(10.5f, 600);
            string hh = H.ToString("00"), mm = M.ToString("00");
            float wh = Theme.Measure(g, hh, font).Width, wc = Theme.Measure(g, ":", font).Width, wm = Theme.Measure(g, mm, font).Width;
            float x = (Width - wh - wc - wm) / 2;
            Color fg = Theme.Blend(fill, Enabled ? Theme.Text : Theme.Disabled, a);
            var parts = new[] { (hh, wh), (":", wc), (mm, wm) };
            for (int i = 0; i < parts.Length; i++)
            {
                var r = new RectangleF(x, 0, parts[i].Item2 + 1, Height);
                bool selected = editing && Enabled && i == part * 2;
                if (selected) Theme.FillRound(g, new RectangleF(r.X - Theme.D(2), Height / 2f - Theme.D(11), r.Width + Theme.D(4), Theme.D(22)), Theme.D(6), Theme.Accent);
                Theme.Draw(g, parts[i].Item1, font, selected ? Theme.OnAccent : fg, r);
                x += parts[i].Item2;
            }
        }
    }

    /// <summary>Two-option sliding switch.</summary>
    class ModeSlider : Painted
    {
        readonly string[] labels;
        double position;
        int hover = -1;

        public ModeSlider(string left, string right)
        {
            labels = new[] { left, right };
            Cursor = Cursors.Hand;
        }

        public bool RightSelected { get; private set; }
        public event Action<bool> Changed;

        public void Set(bool right, bool animate)
        {
            RightSelected = right;
            if (!animate) SetProgress(right ? 1 : 0);
        }

        public void SetProgress(double value) { position = value; Invalidate(); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = e.X >= Width / 2 ? 1 : 0;
            if (h != hover) { hover = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            bool right = e.X >= Width / 2;
            if (right != RightSelected) Changed?.Invoke(right);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            float inset = Theme.D(4), h = Height, half = (Width - 2 * inset) / 2, p = (float)position;
            Theme.FillRound(g, Box, Theme.D(8), Theme.Surface);
            Theme.FillRound(g, new RectangleF(inset + p * half, inset, half, h - 2 * inset - 1), Theme.D(6),
                            Enabled ? Theme.Surface2 : Theme.Surface);
            for (int i = 0; i < 2; i++)
            {
                double amount = i == 1 ? p : 1 - p;
                var c = Theme.Blend(hover == i && amount < 0.5 ? Theme.Text : Theme.Second, Theme.Accent, amount);
                if (!Enabled) c = Theme.Blend(c, Theme.Bg, 0.5);
                Theme.Draw(g, labels[i], Theme.Ui(10.5f), c,
                           new RectangleF(inset + i * half, 0, half, h), StringAlignment.Center);
            }
        }
    }

    /// <summary>
    /// [Duration | At time] slider and an HH:mm box on a three-column grid. The slider is centered
    /// while the box is hidden; choosing "At time" slides it left and fades the box in.
    /// </summary>
    class ModeRow : Painted
    {
        public readonly ModeSlider Mode = new ModeSlider("Duration", "At time");
        public readonly TimeBox Time = new TimeBox();
        readonly Tween expand;
        const int Gap = 8;

        public ModeRow()
        {
            expand = Own(new Tween(Motion.Selection, 0, Place));
            Controls.AddRange(new Control[] { Mode, Time });
        }

        public void SetClockMode(bool on, bool animate)
        {
            Mode.Set(on, animate);
            if (animate) expand.To(on ? 1 : 0);
            else expand.Set(on ? 1 : 0);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Place(); }

        void Place()
        {
            double t = expand.Value;
            Mode.SetProgress(t);
            float gap = Theme.D(Gap), w = (Width - 2 * gap) / 3, x = (float)((Width - (2 * w + gap)) / 2 * (1 - t));
            Mode.SetBounds((int)Math.Round(x), 0, (int)Math.Round(2 * w + gap), Height);
            Time.SetBounds((int)Math.Round(2 * (w + gap)), 0, (int)Math.Round(w), Height);
            Time.Opacity = t;
            Time.Visible = t > 0.01;
            Time.Invalidate();
        }
    }

    /// <summary>Full-window page (main view or settings); swapped with a crossfade.</summary>
    class Page : Painted { }

    /// <summary>Crossfades between two pages inside the window: both are rendered to bitmaps and blended
    /// on this double-buffered layer, then the real target page is brought to the front.</summary>
    sealed class FadeLayer : Painted
    {
        readonly Tween progress;
        Bitmap from, to;
        Action finish;

        public FadeLayer()
        {
            Visible = false;
            progress = Own(new Tween(Motion.Reveal, 0, Step));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { from?.Dispose(); to?.Dispose(); from = to = null; finish = null; }
            base.Dispose(disposing);
        }

        public bool Running => Visible;

        public void Run(Control fromPage, Control toPage, Action swap)
        {
            from = Snapshot(fromPage);
            to = Snapshot(toPage);
            finish = swap;
            Bounds = fromPage.Bounds;
            progress.Set(0);
            Visible = true;
            BringToFront();
            progress.To(1);
        }

        void Step()
        {
            Invalidate();
            if (progress.Value < 1 || finish == null) return;
            var swap = finish;
            finish = null;
            swap();
            Visible = false;
            from.Dispose(); to.Dispose(); from = to = null;
        }

        /// <summary>Renders a control and its visible children, back to front, into a bitmap.</summary>
        static Bitmap Snapshot(Control page)
        {
            var bmp = new Bitmap(page.Width, page.Height, PixelFormat.Format32bppRgb);  // opaque: GDI text leaves alpha 0 in ARGB
            using (var g = Graphics.FromImage(bmp)) Render(page, g, Point.Empty, new Rectangle(Point.Empty, page.Size));
            return bmp;
        }

        /// <summary>Each control is clipped to its parents, as on screen - rows scrolled out of a list
        /// must not be painted over its neighbours.</summary>
        static void Render(Control c, Graphics g, Point at, Rectangle clip)
        {
            clip = Rectangle.Intersect(clip, new Rectangle(at, c.Size));
            if (clip.IsEmpty) return;
            g.SetClip(clip);
            using (var part = new Bitmap(Math.Max(1, c.Width), Math.Max(1, c.Height), PixelFormat.Format32bppRgb))
            {
                c.DrawToBitmap(part, new Rectangle(Point.Empty, part.Size));
                g.DrawImage(part, new Rectangle(at, part.Size));
            }
            for (int i = c.Controls.Count - 1; i >= 0; i--)
            {
                var child = c.Controls[i];
                if (child.Visible) Render(child, g, new Point(at.X + child.Left, at.Y + child.Top), clip);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            if (from == null || !Visible) return;
            var rect = new Rectangle(Point.Empty, from.Size);
            g.DrawImage(from, rect);
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(new ColorMatrix { Matrix33 = (float)progress.Value });
                g.DrawImage(to, rect, 0, 0, to.Width, to.Height, GraphicsUnit.Pixel, ia);
            }
        }
    }

    class GearButton : Painted
    {
        public bool Active;

        public GearButton()
        {
            Cursor = Cursors.Hand;
            Size = new Size(Theme.D(30), Theme.D(30));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            if (Hot || Active) Theme.FillRound(g, Box, Height / 2f, Theme.Surface);
            Color color = Active ? Theme.Accent : Hot ? Theme.Text : Theme.Second;

            const int teeth = 8;
            float s = Theme.S, cx = Width / 2f, cy = Height / 2f, outer = 7.5f * s, inner = 5.6f * s, step = 360f / teeth;
            var points = new PointF[teeth * 4];
            for (int i = 0; i < teeth; i++)
            {
                float a = i * step;
                var spec = new[] { (inner, a - 0.27f * step), (outer, a - 0.14f * step), (outer, a + 0.14f * step), (inner, a + 0.27f * step) };
                for (int j = 0; j < 4; j++)
                {
                    double rad = spec[j].Item2 * Math.PI / 180;
                    points[i * 4 + j] = new PointF(cx + spec[j].Item1 * (float)Math.Cos(rad), cy + spec[j].Item1 * (float)Math.Sin(rad));
                }
            }
            using (var pen = new Pen(color, 1.5f * s) { LineJoin = LineJoin.Round })
            {
                g.DrawPolygon(pen, points);
                float hole = 2.4f * s;
                g.DrawEllipse(pen, cx - hole, cy - hole, 2 * hole, 2 * hole);
            }
        }
    }

    /// <summary>Brand on the left, version · author and a GitHub link on the right.</summary>
    class Footer : Painted
    {
        public const string Author = "Dominik Jurásek", GithubUrl = "https://github.com/jurasekdominik";
        readonly Bitmap markOff, markOn;
        bool hoverMark;

        public Footer()
        {
            using (var s = typeof(Footer).Assembly.GetManifestResourceStream("github-mark-white.png"))
            using (var src = new Bitmap(s))
            {
                markOff = Own(Tint(src, Theme.Faint));
                markOn = Own(Tint(src, Theme.GithubGreen));
            }
        }

        static Bitmap Tint(Bitmap src, Color c)
        {
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            var matrix = new ColorMatrix(new[] {
                new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 }, new float[] { c.R / 255f, c.G / 255f, c.B / 255f, 0, 1 } });
            using (var g = Graphics.FromImage(bmp))
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(matrix);
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
            return bmp;
        }

        RectangleF MarkRect => new RectangleF(Width - Theme.D(16), (Height - Theme.D(16)) / 2f, Theme.D(16), Theme.D(16));

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var r = MarkRect;
            r.Inflate(Theme.D(4), Theme.D(4));
            bool h = r.Contains(e.Location);
            if (h != hoverMark) { hoverMark = h; Cursor = h ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoverMark = false; Cursor = Cursors.Default; base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (hoverMark && e.Button == MouseButtons.Left) Native.OpenUrl(GithubUrl);
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            float h = Height, mid = h / 2, glyph = Theme.D(14);
            var ring = new RectangleF(1.5f * Theme.S, mid - glyph / 2, glyph, glyph);
            using (var pen = new Pen(Theme.Border, 2.4f * Theme.S)) g.DrawEllipse(pen, ring);
            using (var pen = new Pen(Theme.Accent, 2.4f * Theme.S) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                g.DrawArc(pen, ring, -90, 270);

            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            var brand = Theme.Mono(10.5f, 700);  // 14 px: the narrow window leaves no room for the 17 px logo size
            float x = ring.Right + Theme.D(9);
            Theme.Draw(g, "sleep", brand, Theme.Text, new RectangleF(x, 0, Width, h));
            x += Theme.Measure(g, "sleep", brand).Width;
            Theme.Draw(g, "timer", brand, Theme.Accent, new RectangleF(x, 0, Width, h));

            var small = Theme.Mono(9f);  // mono-small: 12 px
            string version = "v" + Program.Version;
            float right = Width - Theme.D(28);
            float authorX = right - Theme.Measure(g, Author, small).Width;
            float dotX = authorX - Theme.D(7);
            float versionX = dotX - Theme.D(7) - Theme.Measure(g, version, small).Width;
            Theme.Draw(g, version, small, Theme.Faint, new RectangleF(versionX, 0, Width, h));
            float r = 1.6f * Theme.S;
            using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, dotX - r, mid - r, 2 * r, 2 * r);
            Theme.Draw(g, Author, small, Theme.Faint, new RectangleF(authorX, 0, Width, h));

            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(hoverMark ? markOn : markOff, MarkRect);
        }
    }

    /// <summary>One row of a <see cref="PickList"/>.</summary>
    sealed class PickRow
    {
        public string Text, Right;           // Right: dim text at the right edge, e.g. the current value
        public bool Sep, Check, More, Back, Header, KeepOpen, Danger;  // Header: the button it opened from
        public bool Enabled = true;
        public Action Click;
    }

    /// <summary>Owner-drawn list used for every menu: the tray menu and the keep-awake dropdown.</summary>
    sealed class PickList : Painted
    {
        const int RowH = 28, SepH = 9, Pad = 4;
        List<PickRow> rows;
        int hover = -1, scroll, contentHeight;
        readonly int maxHeight;

        public PickList(List<PickRow> rows, int width, int maxHeight)
        {
            Under = Theme.Bg;
            Cursor = Cursors.Hand;
            Width = Theme.D(width);
            this.maxHeight = maxHeight;
            SetRows(rows);
        }

        /// <summary>New rows in place: the hover survives as long as the rows keep their places (a ticking time).</summary>
        public void SetRows(List<PickRow> next)
        {
            if (rows == null || next.Count != rows.Count) { hover = -1; scroll = 0; }
            rows = next;
            contentHeight = Top0 + Theme.D(Pad + rows.Sum(r => r.Sep ? SepH : RowH));
            Height = Math.Min(contentHeight, maxHeight);
            scroll = Math.Min(scroll, Math.Max(0, contentHeight - Height));
            Invalidate();
        }

        public event Action<PickRow> Picked;

        /// <summary>A header row is flush with the top edge, so it lies exactly over the button the list opened from.</summary>
        int Top0 => rows.Count > 0 && rows[0].Header ? 0 : Theme.D(Pad);
        public int HeaderHeight => Theme.D(RowH);

        IEnumerable<(PickRow row, int i, Rectangle r)> Cells()
        {
            int y = Top0 - scroll;
            for (int i = 0; i < rows.Count; i++)
            {
                int h = Theme.D(rows[i].Sep ? SepH : RowH);
                yield return (rows[i], i, new Rectangle(0, y, Width, h));
                y += h;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scroll = Math.Max(0, Math.Min(contentHeight - Height, scroll - Math.Sign(e.Delta) * Theme.D(3 * RowH)));
            hover = At(e.Location);
            Invalidate();
        }

        int At(Point p) => Cells().Where(l => !l.row.Sep && l.row.Enabled && l.r.Contains(p)).Select(l => l.i).DefaultIfEmpty(-1).First();

        protected override void OnMouseMove(MouseEventArgs e) { int h = At(e.Location); if (h != hover) { hover = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int i = At(e.Location);
            if (e.Button == MouseButtons.Left && i >= 0) Picked?.Invoke(rows[i]);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            float s = Theme.S;
            Theme.PaintPopup(g, Width, Height);
            var font = Theme.Ui(10.5f);
            var saved = g.Save();
            g.SetClip(new Rectangle(Theme.D(Pad), Theme.D(Pad), Width - 2 * Theme.D(Pad), Height - 2 * Theme.D(Pad)), CombineMode.Intersect);
            foreach (var (row, i, r) in Cells())
            {
                if (row.Sep)
                {
                    using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, Theme.D(12), r.Y + r.Height / 2, Width - Theme.D(12), r.Y + r.Height / 2);
                    continue;
                }
                var cell = new RectangleF(Theme.D(Pad), r.Y, Width - 2 * Theme.D(Pad), r.Height);
                if (row.Header) { cell.Y += Theme.D(Pad); cell.Height -= Theme.D(Pad); }  // flush with the top edge: its highlight needs a top margin
                bool hot = i == hover;
                if (hot || row.Check) Theme.FillRound(g, cell, Theme.D(6), hot ? (row.Danger ? Theme.DangerSoft : Theme.Soft) : Theme.CurrentRow);
                var col = !row.Enabled ? Theme.Disabled : row.Danger ? Theme.DangerHi : row.Check ? Theme.Accent : hot ? Theme.AccentHi : row.Header ? Theme.Second : Theme.Text;
                float textX = Theme.D(row.Back && !row.Header ? 34 : 16), cy = r.Y + r.Height / 2f, mark = Theme.D(19);
                using (var pen = new Pen(row.Check ? Theme.Accent : Theme.Blend(Theme.Surface, col, 0.8), 1.7f * s)
                       { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    if (row.Back)
                        g.DrawLines(pen, new[] { new PointF(mark + 2 * s, cy - 4 * s), new PointF(mark - 2 * s, cy), new PointF(mark + 2 * s, cy + 4 * s) });
                    float ax = Width - Theme.D(18);
                    if (row.More)
                        g.DrawLines(pen, new[] { new PointF(ax - 2 * s, cy - 4 * s), new PointF(ax + 2 * s, cy), new PointF(ax - 2 * s, cy + 4 * s) });
                    if (row.Header)
                        g.DrawLines(pen, new[] { new PointF(ax - 4 * s, cy + 2 * s), new PointF(ax, cy - 2 * s), new PointF(ax + 4 * s, cy + 2 * s) });
                }
                Theme.Draw(g, row.Text, row.Check ? Theme.Ui(10.5f, 600) : font, col, new RectangleF(textX, r.Y, Width - textX, r.Height));
                if (row.Right != null)
                    Theme.Draw(g, row.Right, font, row.Enabled ? Theme.Faint : Theme.Disabled,
                               new RectangleF(0, r.Y, Width - Theme.D(row.More ? 32 : 16), r.Height), StringAlignment.Far);
            }
            g.Restore(saved);
            if (contentHeight > Height)
            {
                float track = Height - 2 * Theme.D(8), thumb = Math.Max(Theme.D(24), track * Height / contentHeight);
                float top = Theme.D(8) + (track - thumb) * scroll / (contentHeight - Height);
                Theme.FillRound(g, new RectangleF(Width - Theme.D(8), top, Theme.D(4), thumb), Theme.D(2), Theme.Disabled);
            }
        }
    }

    /// <summary>A <see cref="PickList"/> in a popup that rolls down from a button, or opens above the tray.
    /// Rows can be replaced while it is open (drill into a submenu); the anchored edge stays put.</summary>
    sealed class PopupList : IDisposable
    {
        readonly Drop drop = new Drop();
        readonly Tween roll;
        readonly int width;
        ToolStripDropDown popup;
        ToolStripControlHost host;
        PickList list;
        int left, edge;
        bool up;

        public PopupList(int width)
        {
            this.width = width;
            roll = new Tween(Motion.Reveal, 1, Fit);
        }

        public bool Visible => drop.Visible;
        public bool JustClosed => drop.JustClosed;

        /// <summary>Opens with its top-left (or, with up, bottom-left) at the given screen point, kept on that screen.</summary>
        public void Show(List<PickRow> rows, Point at, bool up, bool animate)
        {
            this.up = up;
            var area = Screen.FromPoint(at).WorkingArea;
            left = Math.Max(area.Left, Math.Min(at.X, area.Right - Theme.D(width)));
            edge = at.Y;
            list = NewList(rows);
            popup = drop.Show(list, Theme.PopupShape(list.Width, list.Height));
            host = (ToolStripControlHost)popup.Items[0];
            roll.Set(animate ? Math.Max(0.001, (double)FirstRow(rows) / list.Height) : 1);
            Fit();
            popup.Show(popup.Location);
            Fit();
            if (animate) roll.To(1);
        }

        public void SetRows(List<PickRow> rows)
        {
            if (!Visible) return;
            int h = list.Height;
            list.SetRows(rows);
            if (list.Height != h) Fit();
        }

        public void Close() => popup?.Close();
        public void Dispose() { roll.Dispose(); drop.Dispose(); }

        int FirstRow(List<PickRow> rows) => rows.Count > 0 && rows[0].Header ? list.HeaderHeight : 0;

        PickList NewList(List<PickRow> rows)
        {
            var l = new PickList(rows, width, Math.Max(Theme.D(56), Screen.FromPoint(new Point(left, edge)).WorkingArea.Height - Theme.D(16)));
            // close first: an action may open a dialog (Quit with a timer running) that must not sit under the menu
            l.Picked += r => { if (!r.KeepOpen) Close(); r.Click?.Invoke(); };
            return l;
        }

        /// <summary>Sizes the popup to the visible part of the list, clipped to rounded corners, anchored at the edge.</summary>
        void Fit()
        {
            if (popup == null || popup.IsDisposed) return;
            int w = list.Width, h = Math.Max(1, (int)Math.Round(list.Height * roll.Value));
            var area = Screen.FromPoint(new Point(left, edge)).WorkingArea;
            int top = up ? edge - h : Math.Min(edge, area.Bottom - list.Height);
            host.Size = list.Size;
            popup.Bounds = new Rectangle(left, Math.Max(area.Top, top), w, h);
            var old = popup.Region;
            using (var shape = Theme.PopupShape(w, h)) popup.Region = new Region(shape);
            old?.Dispose();
        }
    }

    /// <summary>Rounded card (surface + border) that hosts other controls.</summary>
    class Card : Painted
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            Theme.FillRound(g, Box, Theme.D(12), Theme.Surface, Theme.Border);
            using (var clip = Theme.Round(Box, Theme.D(12)))
            {
                var saved = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                using (var pen = new Pen(Theme.Blend(Theme.Surface, Theme.Text, 0.06), Theme.S))
                    g.DrawLine(pen, Theme.D(12), Theme.S, Width - Theme.D(12), Theme.S);
                g.Restore(saved);
            }
        }
    }

    /// <summary>Rounded single-line input: border shows hover / focus / invalid.</summary>
    class Field : Painted
    {
        const int EM_SETCUEBANNER = 0x1501;
        public readonly TextBox Box_ = new TextBox();
        public event Action Edited, Submit;
        bool invalid, setting;
        string placeholder = "";

        public Field(string placeholder = "")
        {
            this.placeholder = placeholder;
            Box_.BorderStyle = BorderStyle.None;
            Box_.BackColor = Theme.Surface;
            Box_.ForeColor = Theme.Text;
            Box_.Font = Theme.Ui(10.5f);
            Controls.Add(Box_);
            Box_.GotFocus += (s, e) => Invalidate();
            Box_.LostFocus += (s, e) => Invalidate();
            Box_.MouseEnter += (s, e) => { Hot = true; Invalidate(); };
            Box_.MouseLeave += (s, e) => { Hot = ClientRectangle.Contains(PointToClient(Cursor.Position)); Invalidate(); };
            Box_.TextChanged += (s, e) => { if (!setting) Edited?.Invoke(); };
            Box_.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Submit?.Invoke(); }
            };
            Box_.HandleCreated += (s, e) => ApplyPlaceholder();
            Cursor = Cursors.IBeam;
        }

        void ApplyPlaceholder() { try { if (Box_.IsHandleCreated) Native.SendMessage(Box_.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder); } catch { } }

        public override string Text { get => Box_.Text; set { setting = true; Box_.Text = value ?? ""; setting = false; } }
        public bool Invalid { get => invalid; set { if (invalid != value) { invalid = value; Invalidate(); } } }
        public int MaxLength { set => Box_.MaxLength = value; }

        protected override void OnMouseDown(MouseEventArgs e) { Box_.Focus(); base.OnMouseDown(e); }
        protected override void OnEnabledChanged(EventArgs e) { Box_.BackColor = Enabled ? Theme.Surface : Theme.Bg; base.OnEnabledChanged(e); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            int pad = Theme.D(11);
            Box_.SetBounds(pad, (Height - Box_.PreferredHeight) / 2 + 1, Width - 2 * pad, Box_.PreferredHeight);
            base.OnLayout(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            Color border = !Enabled ? Theme.Border : invalid ? Theme.Danger : Box_.Focused ? Theme.Accent
                         : Hot ? Theme.Disabled : Theme.Border;
            Theme.FillRound(g, Box, Theme.D(8), Enabled ? Theme.Surface : Theme.Bg, border);
        }
    }

    /// <summary>Text tabs - the active one bright with an accent underline.</summary>
    class TextTabs : Painted
    {
        readonly string[] Names;
        public int Active;
        readonly Tween underline;
        readonly List<(float x, float w)> pos = new List<(float, float)>();
        public event Action<int> Changed;

        public TextTabs(params string[] names)
        {
            Names = names;
            underline = Own(new Tween(Motion.Tabs, 0, Invalidate));
            Cursor = Cursors.Hand;
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            {
                float x = 0;
                foreach (var n in Names)
                {
                    float w = Theme.Measure(g, Spaced(n), Theme.Ui(9f, 600)).Width;
                    pos.Add((x, w));
                    x += w + Theme.D(22);
                }
                Size = new Size((int)x, Theme.D(22));
            }
        }

        static string Spaced(string s) => string.Join(" ", s.ToCharArray());  // hair space = letter spacing

        public void SetActive(int i)
        {
            if (i == Active) return;
            if (i < 0 || i >= Names.Length) return;
            Active = i; underline.To(i); Invalidate(); Changed?.Invoke(i);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            for (int i = 0; i < pos.Count; i++)
                if (e.X >= pos[i].x - 8 && e.X <= pos[i].x + pos[i].w + 8) SetActive(i);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            for (int i = 0; i < Names.Length; i++)
            {
                bool on = i == Active;
                Theme.Draw(g, Spaced(Names[i]), Theme.Ui(9f, 600), on ? Theme.Text : Theme.Faint,
                           new RectangleF(pos[i].x, 0, pos[i].w + 4, Theme.D(17)));
            }
            double value = underline.Value;
            int left = Math.Max(0, Math.Min(pos.Count - 1, (int)Math.Floor(value))), right = Math.Min(pos.Count - 1, left + 1);
            float t = (float)(value - left);
            using (var b = new SolidBrush(Theme.Accent))
                g.FillRectangle(b, pos[left].x + (pos[right].x - pos[left].x) * t, Theme.D(18),
                                pos[left].w + (pos[right].w - pos[left].w) * t, 2.5f * Theme.S);
        }
    }

    /// <summary>The popup behind every dropdown and menu. WinForms closes a popup on mouse-down, before the
    /// control under the mouse gets its click - so a click on the control that opened it would open it again.
    /// Owners check JustClosed first: that click is the one that closed the list.</summary>
    sealed class PopupRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Theme.Bg);
    }

    sealed class Drop : IDisposable
    {
        ToolStripDropDown last;
        DateTime closedAt;

        public void Dispose() { last?.Dispose(); last = null; }
        public bool Visible => last?.Visible ?? false;
        public bool JustClosed => (DateTime.UtcNow - closedAt).TotalMilliseconds < 300;

        /// <summary>content in a borderless popup clipped to shape (disposed here); closed runs when it closes.
        /// The caller shows the returned popup where it belongs.</summary>
        public ToolStripDropDown Show(Control content, GraphicsPath shape, Action closed = null)
        {
            var host = new ToolStripControlHost(content) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = content.Size };
            last?.Dispose();  // the previous one, now closed - never inside its own Closed handler (unsafe while closing)
            var drop = last = new ToolStripDropDown { BackColor = Theme.Bg, Renderer = new PopupRenderer(), Padding = Padding.Empty, DropShadowEnabled = false, AutoSize = false, Size = content.Size };
            drop.Items.Add(host);
            using (shape) drop.Region = new Region(shape);
            drop.Closed += (s, e) => { closedAt = DateTime.UtcNow; closed?.Invoke(); };
            return drop;
        }
    }

    /// <summary>Row of accent swatches; the chosen one has a ring.</summary>
    sealed class Swatches : Painted
    {
        public static readonly (string name, string hex)[] Colors = {
            ("Mint", "#0CD29D"), ("Lime", "#BADC58"), ("Amber", "#FFB142"), ("Pumpkin", "#FF793F"),
            ("Pink", "#FD79A8"), ("Grape", "#8C7AE6"), ("Lavender", "#A29BFE"), ("Sky", "#34ACE0") };
        string selected;
        int hover = -1;
        public event Action<string> Picked;
        float Dot => Theme.D(28);
        float Step => Theme.D(40);
        /// <summary>Swatches per row; the rest wrap onto further rows.</summary>
        public int Columns = 8;

        public string Selected { get => selected; set { selected = value; Invalidate(); } }

        int At(Point p)
        {
            int col = (int)((p.X - Theme.D(6) + (Step - Dot) / 2) / Step), row = (int)((p.Y - Theme.D(6) + (Step - Dot) / 2) / Step);
            int i = row * Columns + col;
            return col >= 0 && col < Columns && row >= 0 && i < Colors.Length ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = At(e.Location);
            if (h != hover) { hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = At(e.Location);
            if (e.Button == MouseButtons.Left && i >= 0) Picked?.Invoke(Colors[i].hex);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            float s = Theme.S;
            for (int i = 0; i < Colors.Length; i++)
            {
                var c = ColorTranslator.FromHtml(Colors[i].hex);
                float x = Theme.D(6) + i % Columns * Step, y = Theme.D(6) + i / Columns * Step;
                bool cur = string.Equals(Colors[i].hex, selected, StringComparison.OrdinalIgnoreCase);
                if (cur || i == hover)
                    using (var pen = new Pen(cur ? c : Theme.Disabled, 2f * s)) g.DrawEllipse(pen, x - 4 * s, y - 4 * s, Dot + 8 * s, Dot + 8 * s);
                using (var b = new SolidBrush(c)) g.FillEllipse(b, x, y, Dot, Dot);
            }
        }
    }

    /// <summary>Colour helper for a custom accent, two pages. Palette: 17 hues x shades
    /// 200-600. Mixer: hue bar plus a saturation / brightness field with a dotted line where contrast on the
    /// background drops under 4.5:1. Colours under that are faded / below the line - pickable, but marked.
    /// Picked(colour, close): palette clicks close the helper, mixer drags apply and keep it open.</summary>
    sealed class ColorGrid : Painted
    {
        static readonly (string hue, string[] shades)[] Palette = {
            ("red", new[] { "#fecaca", "#fca5a5", "#f87171", "#ef4444", "#dc2626" }),
            ("orange", new[] { "#fed7aa", "#fdba74", "#fb923c", "#f97316", "#ea580c" }),
            ("amber", new[] { "#fde68a", "#fcd34d", "#fbbf24", "#f59e0b", "#d97706" }),
            ("yellow", new[] { "#fef08a", "#fde047", "#facc15", "#eab308", "#ca8a04" }),
            ("lime", new[] { "#d9f99d", "#bef264", "#a3e635", "#84cc16", "#65a30d" }),
            ("green", new[] { "#bbf7d0", "#86efac", "#4ade80", "#22c55e", "#16a34a" }),
            ("emerald", new[] { "#a7f3d0", "#6ee7b7", "#34d399", "#10b981", "#059669" }),
            ("teal", new[] { "#99f6e4", "#5eead4", "#2dd4bf", "#14b8a6", "#0d9488" }),
            ("cyan", new[] { "#a5f3fc", "#67e8f9", "#22d3ee", "#06b6d4", "#0891b2" }),
            ("sky", new[] { "#bae6fd", "#7dd3fc", "#38bdf8", "#0ea5e9", "#0284c7" }),
            ("blue", new[] { "#bfdbfe", "#93c5fd", "#60a5fa", "#3b82f6", "#2563eb" }),
            ("indigo", new[] { "#c7d2fe", "#a5b4fc", "#818cf8", "#6366f1", "#4f46e5" }),
            ("violet", new[] { "#ddd6fe", "#c4b5fd", "#a78bfa", "#8b5cf6", "#7c3aed" }),
            ("purple", new[] { "#e9d5ff", "#d8b4fe", "#c084fc", "#a855f7", "#9333ea" }),
            ("fuchsia", new[] { "#f5d0fe", "#f0abfc", "#e879f9", "#d946ef", "#c026d3" }),
            ("pink", new[] { "#fbcfe8", "#f9a8d4", "#f472b6", "#ec4899", "#db2777" }),
            ("rose", new[] { "#fecdd3", "#fda4af", "#fb7185", "#f43f5e", "#e11d48" }),
        };
        static readonly int[] ShadeNames = { 200, 300, 400, 500, 600 };
        const int Rows = 5;
        const double MinContrast = 4.5;
        static int Cols => Palette.Length;

        public event Action<Color, bool> Picked;
        int mode, hover = -1, drag;            // drag: 0 none, 1 field, 2 hue bar
        double hue, sat, val;
        Color current;
        Bitmap field;
        double fieldHue = -1;
        readonly RectangleF[] tabs = new RectangleF[2];

        int Cell => Theme.D(24);
        int Gap => Theme.D(4);
        int Pad => Theme.D(16);
        int GridTop => Theme.D(34);
        int GridH => Rows * Cell + (Rows - 1) * Gap;
        RectangleF FieldRect => new RectangleF(Pad, GridTop, Width - 2 * Pad, GridH - Theme.D(24));
        RectangleF HueRect => new RectangleF(Pad, FieldRect.Bottom + Theme.D(10), Width - 2 * Pad, Theme.D(14));

        public ColorGrid(Color start)
        {
            Under = Theme.Bg;
            current = start;
            (hue, sat, val) = (start.GetHue(), 0, 0);
            int max = Math.Max(start.R, Math.Max(start.G, start.B)), min = Math.Min(start.R, Math.Min(start.G, start.B));
            val = max / 255.0;
            sat = max == 0 ? 0 : (max - min) / (double)max;
            Size = new Size(2 * Pad + Cols * Cell + (Cols - 1) * Gap, GridTop + GridH + Theme.D(58));
        }

        public static Color Hsv(double h, double s, double v)
        {
            double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c, r, g, b;
            if (h < 60) (r, g, b) = (c, x, 0); else if (h < 120) (r, g, b) = (x, c, 0); else if (h < 180) (r, g, b) = (0, c, x);
            else if (h < 240) (r, g, b) = (0, x, c); else if (h < 300) (r, g, b) = (x, 0, c); else (r, g, b) = (c, 0, x);
            return Color.FromArgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
        }

        static Color At(int i) => ColorTranslator.FromHtml(Palette[i % Cols].shades[i / Cols]);
        static string ShadeName(int i) => $"{Palette[i % Cols].hue}-{ShadeNames[i / Cols]}";
        /// <summary>Same rounding as the label, so "4.5 : 1" is never shown as low contrast.</summary>
        static bool Readable(Color c) => Math.Round(Theme.Contrast(c, Theme.Bg), 1) >= MinContrast;
        Color Mixed => Hsv(hue, sat, val);

        int CellAt(Point p)
        {
            int col = (int)((p.X - Pad) / (float)(Cell + Gap)), row = (int)((p.Y - GridTop) / (float)(Cell + Gap));
            return p.X >= Pad && p.Y >= GridTop && col < Cols && row < Rows ? row * Cols + col : -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            for (int t = 0; t < 2; t++)
                if (tabs[t].Contains(e.Location)) { mode = t; hover = -1; Invalidate(); return; }
            if (mode == 0) { int i = CellAt(e.Location); if (i >= 0) { current = At(i); Picked?.Invoke(current, true); } return; }
            var hr = HueRect; hr.Inflate(0, Theme.D(6));
            drag = FieldRect.Contains(e.Location) ? 1 : hr.Contains(e.Location) ? 2 : 0;
            if (drag != 0) { Capture = true; DragTo(e.Location); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (drag != 0) { DragTo(e.Location); return; }
            bool overTab = tabs[0].Contains(e.Location) || tabs[1].Contains(e.Location);
            if (mode == 0)
            {
                int h = CellAt(e.Location);
                if (h != hover) { hover = h; Invalidate(); }
                Cursor = h >= 0 || overTab ? Cursors.Hand : Cursors.Default;
            }
            else Cursor = overTab ? Cursors.Hand : FieldRect.Contains(e.Location) || HueRect.Contains(e.Location) ? Cursors.Cross : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (drag == 0) return;
            drag = 0;
            Capture = false;
            current = Mixed;
            Picked?.Invoke(current, false);  // the whole app recolours once, on release
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

        void DragTo(Point p)
        {
            if (drag == 1)
            {
                var f = FieldRect;
                sat = Math.Max(0, Math.Min(1, (p.X - f.X) / (f.Width - 1)));
                val = Math.Max(0, Math.Min(1, 1 - (p.Y - f.Y) / (f.Height - 1)));
            }
            else hue = Math.Max(0, Math.Min(359.9, (p.X - HueRect.X - 8 * Theme.S) / (HueRect.Width - 16 * Theme.S) * 360));
            Invalidate();
        }

        /// <summary>Saturation (x) / brightness (y) field for one hue, written straight into the bitmap.</summary>
        void BuildField(int w, int h)
        {
            field?.Dispose();
            field = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var data = field.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var px = new int[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = Hsv(hue, x / (double)(w - 1), 1 - y / (double)(h - 1)).ToArgb();
            System.Runtime.InteropServices.Marshal.Copy(px, 0, data.Scan0, px.Length);
            field.UnlockBits(data);
            fieldHue = hue;
        }

        protected override void Dispose(bool disposing) { if (disposing) field?.Dispose(); base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            Theme.PaintPopup(g, Width, Height);
            var tf = Theme.Ui(9f, 600);
            float tx = Pad;
            for (int t = 0; t < 2; t++)
            {
                string label = t == 0 ? "P A L E T T E" : "M I X E R";
                float w = Theme.Measure(g, label, tf).Width;
                tabs[t] = new RectangleF(tx - 4, Theme.D(4), w + 8, Theme.D(26));
                Theme.Draw(g, label, tf, t == mode ? Theme.Text : Theme.Faint, new RectangleF(tx, Theme.D(9), w + 4, Theme.D(16)));
                if (t == mode) using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, tx, Theme.D(25), w, 2 * Theme.S);
                tx += w + Theme.D(18);
            }
            Theme.Draw(g, mode == 0 ? "faded = low contrast" : "below the dotted line = low contrast", Theme.Ui(9f), Theme.Faint,
                       new RectangleF(Width - Pad - 260, Theme.D(9), 260, Theme.D(16)), StringAlignment.Far);
            if (mode == 0) PaintPalette(g); else PaintMixer(g);

            // footer: the colour in question, its contrast, a sample button
            var show = mode == 1 ? Mixed : hover >= 0 ? At(hover) : current;
            double cr = Math.Round(Theme.Contrast(show, Theme.Bg), 1);
            float fy = GridTop + GridH + Theme.D(16);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, Pad, fy - Theme.D(8), Width - Pad, fy - Theme.D(8));
            using (var b = new SolidBrush(show)) g.FillEllipse(b, Pad, fy + Theme.D(6), Theme.D(22), Theme.D(22));
            Theme.Draw(g, Theme.HexOf(show), Theme.Mono(10.5f), Theme.Text, new RectangleF(Pad + Theme.D(32), fy + Theme.D(4), 90, Theme.D(26)));
            string what = mode == 1 ? "mixed" : hover >= 0 ? ShadeName(hover) : "current";
            Theme.Draw(g, what, Theme.Ui(9f), Theme.Faint, new RectangleF(Pad + Theme.D(116), fy + Theme.D(4), 90, Theme.D(26)));
            Theme.Draw(g, cr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " : 1  " + (cr >= MinContrast ? "readable" : cr >= 3 ? "borderline" : "too dark"), Theme.Ui(9f, 600),
                       cr >= MinContrast ? Theme.Second : Theme.DangerHi, new RectangleF(Pad + Theme.D(206), fy + Theme.D(4), 150, Theme.D(26)));
            var btn = new RectangleF(Width - Pad - Theme.D(84), fy + Theme.D(2), Theme.D(84), Theme.D(32));
            Theme.FillRound(g, btn, Theme.D(8), show);
            Theme.Draw(g, "Sample", Theme.Ui(10.5f, 600), Theme.OnColor(show), btn, StringAlignment.Center);
        }

        void PaintPalette(Graphics g)
        {
            for (int i = 0; i < Cols * Rows; i++)
            {
                float x = Pad + i % Cols * (Cell + Gap), y = GridTop + i / Cols * (Cell + Gap);
                var c = At(i);
                bool ok = Readable(c), cur = Theme.HexOf(c) == Theme.HexOf(current);
                var r = new RectangleF(x, y, Cell, Cell);
                if (!ok) r.Inflate(-Theme.D(4), -Theme.D(4));
                Theme.FillRound(g, r, Theme.D(ok ? 6 : 4), ok ? c : Theme.Blend(Theme.Surface, c, 0.45));
                if (i == hover || cur)
                    using (var pen = new Pen(i == hover ? Theme.Text : c, 1.6f * Theme.S))
                    using (var path = Theme.Round(new RectangleF(x - 3, y - 3, Cell + 6, Cell + 6), Theme.D(8)))
                        g.DrawPath(pen, path);
            }
        }

        void PaintMixer(Graphics g)
        {
            var f = FieldRect;
            if (field == null || fieldHue != hue || field.Width != (int)f.Width) BuildField((int)f.Width, (int)f.Height);
            using (var path = Theme.Round(f, Theme.D(8)))
            {
                g.SetClip(path);
                g.DrawImage(field, f);
                var pts = new List<PointF>();  // per column, the lowest brightness that still reaches the contrast minimum
                for (int x = 0; x < field.Width; x += 3)
                {
                    double s = x / (double)(field.Width - 1), lo = 0, hi = 1;
                    for (int k = 0; k < 16; k++) { double mid = (lo + hi) / 2; if (Theme.Contrast(Hsv(hue, s, mid), Theme.Bg) >= MinContrast) hi = mid; else lo = mid; }
                    pts.Add(new PointF(f.X + x, f.Y + (float)((1 - hi) * (f.Height - 1))));
                }
                using (var pen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.3f * Theme.S) { DashStyle = DashStyle.Dot })
                    if (pts.Count > 1) g.DrawLines(pen, pts.ToArray());
                g.ResetClip();
            }
            float s1 = Theme.S, mx = f.X + (float)sat * (f.Width - 1), my = f.Y + (float)(1 - val) * (f.Height - 1), r = 6 * s1;
            using (var pen = new Pen(Color.FromArgb(120, 0, 0, 0), 1)) g.DrawEllipse(pen, mx - r - 1.5f, my - r - 1.5f, 2 * r + 3, 2 * r + 3);
            using (var pen = new Pen(Color.White, 2 * s1)) g.DrawEllipse(pen, mx - r, my - r, 2 * r, 2 * r);
            var hb = HueRect;
            using (var brush = new LinearGradientBrush(hb, Color.Red, Color.Red, 0f))
            {
                var blend = new ColorBlend(7);
                for (int i = 0; i < 7; i++) { blend.Colors[i] = Hsv(i * 60 % 360, 1, 1); blend.Positions[i] = i / 6f; }
                brush.InterpolationColors = blend;
                using (var path = Theme.Round(hb, hb.Height / 2)) g.FillPath(brush, path);
            }
            float hr = 8 * s1, hx = hb.X + hr + (float)(hue / 360) * (hb.Width - 2 * hr), hy = hb.Y + hb.Height / 2;
            using (var b = new SolidBrush(Hsv(hue, 1, 1))) g.FillEllipse(b, hx - hr, hy - hr, 2 * hr, 2 * hr);
            using (var pen = new Pen(Color.White, 2 * s1)) g.DrawEllipse(pen, hx - hr, hy - hr, 2 * hr, 2 * hr);
        }
    }
}
