using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SleepTimer
{
    static class VisualText
    {
        public static FontFamily Mono { get; } = new FontFamily(Fonts.SystemFontFamilies.Any(f => f.Source == "Cascadia Code") ? "Cascadia Code"
            : Fonts.SystemFontFamilies.Any(f => f.Source == "Cascadia Mono") ? "Cascadia Mono" : "Consolas");
        public static FormattedText Make(Visual owner, string text, double size, Color color, bool bold = false, bool mono = false) =>
            new FormattedText(text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                mono ? new Typeface(Mono, FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal)
                    : bold ? AppFonts.Ui.BoldTypeface : AppFonts.Ui.Typeface,
                size, Theme.Brush(color), VisualTreeHelper.GetDpi(owner).PixelsPerDip);
        public static double Width(Visual owner, string text, double size = 14) => Make(owner, text, size, Theme.Text).WidthIncludingTrailingWhitespace;
        public static void Draw(DrawingContext g, Visual owner, string text, double size, Color color, double x, double y, bool bold = false, bool mono = false) =>
            g.DrawText(Make(owner, text, size, color, bold, mono), new Point(x, y));
        public static void Center(DrawingContext g, Visual owner, string text, double size, Color color, Rect box, bool bold = false)
        {
            var t = Make(owner, text, size, color, bold);
            g.DrawText(t, new Point(box.X + (box.Width - t.WidthIncludingTrailingWhitespace) / 2, box.Y + (box.Height - t.Height) / 2));
        }
    }

    sealed class BrandFooter : FrameworkElement
    {
        // GitHub Octicons mark-github-16, MIT; see docs/THIRD_PARTY_NOTICES.md.
        static readonly Geometry github = GithubMark();
        static Geometry GithubMark()
        {
            var geometry = Geometry.Parse("M6.766 11.328c-2.063-.25-3.516-1.734-3.516-3.656 0-.781.281-1.625.75-2.188-.203-.515-.172-1.609.063-2.062.625-.078 1.468.25 1.968.703.594-.187 1.219-.281 1.985-.281.765 0 1.39.094 1.953.265.484-.437 1.344-.765 1.969-.687.218.422.25 1.515.046 2.047.5.593.766 1.39.766 2.203 0 1.922-1.453 3.375-3.547 3.64.531.344.89 1.094.89 1.954v1.625c0 .468.391.734.86.547C13.781 14.359 16 11.53 16 8.03 16 3.61 12.406 0 7.984 0 3.563 0 0 3.61 0 8.031a7.88 7.88 0 0 0 5.172 7.422c.422.156.828-.125.828-.547v-1.25c-.219.094-.5.156-.75.156-1.031 0-1.64-.562-2.078-1.609-.172-.422-.36-.672-.719-.719-.187-.015-.25-.093-.25-.187 0-.188.313-.328.625-.328.453 0 .844.281 1.25.86.313.452.64.655 1.031.655s.641-.14 1-.5c.266-.265.47-.5.657-.656");
            geometry.Freeze(); return geometry;
        }
        bool hot;
        public BrandFooter()
        {
            Width = 320; Height = 28;
            MouseMove += (s, e) => { bool next = e.GetPosition(this).X >= 296; if (next != hot) { hot = next; Cursor = hot ? Cursors.Hand : Cursors.Arrow; InvalidateVisual(); } };
            MouseLeave += (s, e) => { hot = false; InvalidateVisual(); };
            MouseLeftButtonUp += (s, e) => { if (e.GetPosition(this).X >= 296) Native.OpenUrl("https://github.com/jurasekdominik"); };
        }
        protected override void OnRender(DrawingContext g)
        {
            g.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 320, 28));
            var center = new Point(8.5, 14);
            g.DrawEllipse(null, new Pen(Theme.Brush(Theme.Border), 2.4), center, 7, 7);
            TimerFace.DrawArc(g, center, 7, -90, 270, 2.4, Theme.Accent);
            double x = 24.5;
            var sleep = VisualText.Make(this, "sleep", 14, Theme.Text, true, true);
            g.DrawText(sleep, new Point(x, (28 - sleep.Height) / 2));
            x += sleep.WidthIncludingTrailingWhitespace;
            var timer = VisualText.Make(this, "timer", 14, Theme.Accent, true, true);
            g.DrawText(timer, new Point(x, (28 - timer.Height) / 2));
            var author = VisualText.Make(this, "Dominik Jurásek", 12, Theme.Faint, false, true);
            var version = VisualText.Make(this, "v" + Program.Version, 12, Theme.Faint, false, true);
            double authorX = 292 - author.WidthIncludingTrailingWhitespace, dotX = authorX - 7;
            g.DrawText(author, new Point(authorX, (28 - author.Height) / 2));
            g.DrawText(version, new Point(dotX - 7 - version.WidthIncludingTrailingWhitespace, (28 - version.Height) / 2));
            g.DrawEllipse(Theme.Brush(Theme.Accent), null, new Point(dotX, 14), 1.6, 1.6);
            var tint = Theme.Brush(hot ? Theme.Hex("#0FBF3E") : Theme.Faint);
            g.PushTransform(new TranslateTransform(304, 6));
            g.DrawGeometry(tint, null, github);
            g.Pop();
        }
    }

    sealed class GearMark : FrameworkElement
    {
        public bool Active;
        public GearMark() { Width = Height = 30; }
        protected override void OnRender(DrawingContext g)
        {
            Color color = Active ? Theme.Accent : IsMouseOver ? Theme.Text : Theme.Second;
            var path = new StreamGeometry();
            using (var p = path.Open())
            {
                for (int i = 0; i < 8; i++)
                {
                    double a = i * 45;
                    double[] angles = { a - .27 * 45, a - .14 * 45, a + .14 * 45, a + .27 * 45 };
                    for (int j = 0; j < 4; j++)
                    {
                        double r = j == 0 || j == 3 ? 5.6 : 7.5, rad = angles[j] * Math.PI / 180;
                        var at = new Point(15 + r * Math.Cos(rad), 15 + r * Math.Sin(rad));
                        if (i == 0 && j == 0) p.BeginFigure(at, false, true); else p.LineTo(at, true, false);
                    }
                }
            }
            var pen = new Pen(Theme.Brush(color), 1.5) { LineJoin = PenLineJoin.Round };
            g.DrawGeometry(null, pen, path); g.DrawEllipse(null, pen, new Point(15, 15), 2.4, 2.4);
        }
    }

    sealed class ModeSelector : FrameworkElement
    {
        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register("Progress", typeof(double), typeof(ModeSelector),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
        public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
        public event Action<bool> Changed;
        bool right;
        int hover = -1;
        public ModeSelector()
        {
            Width = 210.6666667; Height = 32; Cursor = Cursors.Hand; Focusable = true;
            MouseMove += (s, e) => { hover = e.GetPosition(this).X >= ActualWidth / 2 ? 1 : 0; InvalidateVisual(); };
            MouseLeave += (s, e) => { hover = -1; InvalidateVisual(); };
            MouseLeftButtonDown += (s, e) => { if (IsEnabled) Changed?.Invoke(e.GetPosition(this).X >= ActualWidth / 2); e.Handled = true; };
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Space)
                { Changed?.Invoke(e.Key == Key.Space ? !right : e.Key == Key.Right); e.Handled = true; }
            };
            IsEnabledChanged += (s, e) => InvalidateVisual();
        }
        public void Set(bool clock, bool animate)
        {
            right = clock;
            if (animate) BeginAnimation(ProgressProperty, new DoubleAnimation(Progress, clock ? 1 : 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else { BeginAnimation(ProgressProperty, null); Progress = clock ? 1 : 0; }
        }
        protected override void OnRender(DrawingContext g)
        {
            const double inset = 4;
            double half = (ActualWidth - 2 * inset) / 2;
            g.DrawRoundedRectangle(Theme.Brush(Theme.Surface), null, new Rect(.5, .5, ActualWidth - 1, 31), 8, 8);
            g.DrawRoundedRectangle(Theme.Brush(IsEnabled ? Theme.Blend(Theme.Bg, Theme.Text, .09) : Theme.Surface), null,
                new Rect(inset + Progress * half, inset, half, 23), 6, 6);
            for (int i = 0; i < 2; i++)
            {
                double amount = i == 1 ? Progress : 1 - Progress;
                Color color = Theme.Blend(hover == i && amount < .5 ? Theme.Text : Theme.Second, Theme.Accent, amount);
                if (!IsEnabled) color = Theme.Blend(color, Theme.Bg, .5);
                VisualText.Center(g, this, i == 0 ? "Duration" : "At time", 14, color, new Rect(inset + i * half, 0, half, 32));
            }
        }
    }

    sealed class ActionSelector : FrameworkElement
    {
        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register("Progress", typeof(double), typeof(ActionSelector),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
        public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
        public event Action<string> Changed;
        public string Current { get; private set; } = "shutdown";
        public bool IsOpen { get; private set; }
        Window owner;
        int hover = -1;
        public ActionSelector()
        {
            Width = 280; Height = 32; Cursor = Cursors.Hand; Focusable = true;
            Loaded += (s, e) => { owner = Window.GetWindow(this); if (owner != null) owner.PreviewMouseDown += Outside; };
            Unloaded += (s, e) => { if (owner != null) owner.PreviewMouseDown -= Outside; owner = null; };
            IsEnabledChanged += (s, e) => { if (!IsEnabled) SetOpen(false); InvalidateVisual(); };
            MouseMove += (s, e) => { hover = At(e.GetPosition(this).X); InvalidateVisual(); };
            MouseLeave += (s, e) => { hover = -1; InvalidateVisual(); };
            MouseLeftButtonDown += (s, e) =>
            {
                double x = e.GetPosition(this).X;
                if (IsOpen) { int i = At(x); if (i >= 0) Changed?.Invoke(PowerAction.All[i].Key); SetOpen(false); }
                else if (InPill(x)) SetOpen(true);
                e.Handled = true;
            };
            KeyDown += (s, e) => { if (e.Key == Key.Escape) { SetOpen(false); e.Handled = true; } else if (e.Key == Key.Space || e.Key == Key.Enter) { SetOpen(!IsOpen); e.Handled = true; } };
        }
        void Outside(object sender, MouseButtonEventArgs e) { if (!IsMouseOver) SetOpen(false); }
        double[] Segments => PowerAction.All.Select(a => VisualText.Width(this, a.Label) + 26).ToArray();
        double Expanded => Segments.Sum() + 8;
        double Collapsed => VisualText.Width(this, PowerAction.Find(Current).Label) + 32;
        double Shown => Collapsed + (Expanded - Collapsed) * Progress;
        public bool ContainsAction(Point p) => p.Y >= 0 && p.Y <= ActualHeight && InPill(p.X);
        bool InPill(double x) => x >= (ActualWidth - Shown) / 2 && x <= (ActualWidth + Shown) / 2;
        int At(double x)
        {
            if (!InPill(x)) return -1;
            if (!IsOpen) return 0;
            double left = (ActualWidth - Shown) / 2 + 4;
            var widths = Segments;
            for (int i = 0; i < widths.Length; i++) { if (x >= left && x < left + widths[i]) return i; left += widths[i]; }
            return -1;
        }
        void SetOpen(bool open)
        {
            if (open == IsOpen) return; IsOpen = open;
            BeginAnimation(ProgressProperty, new DoubleAnimation(Progress, open ? 1 : 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        public void SetCurrent(string key) { if (key != Current) { Current = key; SetOpen(false); } InvalidateVisual(); }
        protected override void OnRender(DrawingContext g)
        {
            double x = (ActualWidth - Shown) / 2;
            Color fill = Theme.Blend(Theme.Surface, Theme.Blend(Theme.Bg, Theme.Text, .09), IsEnabled && !IsOpen && hover >= 0 ? 1 : Progress);
            var rect = new Rect(x + .5, .5, Shown - 1, 31);
            g.DrawRoundedRectangle(Theme.Brush(fill), null, rect, 4, 4);
            double alpha = 1 - Math.Min(1, Progress * 2);
            if (alpha > 0)
            {
                g.PushOpacity(alpha);
                Color color = !IsEnabled ? Theme.Blend(Theme.Bg, Theme.Text, .24) : hover >= 0 ? Theme.Text : Theme.Second;
                var text = VisualText.Make(this, PowerAction.Find(Current).Label, 14, color);
                g.DrawText(text, new Point(x + 16, (32 - text.Height) / 2));
                g.Pop();
            }
            alpha = Math.Max(0, Math.Min(1, Progress * 2 - 1));
            if (alpha <= 0) return;
            g.PushClip(new RectangleGeometry(rect, 4, 4)); g.PushOpacity(alpha);
            double left = x + 4; var widths = Segments;
            for (int i = 0; i < widths.Length; i++)
            {
                var box = new Rect(left, 4, widths[i], 24); bool current = PowerAction.All[i].Key == Current;
                if (current) g.DrawRoundedRectangle(Theme.Brush(Theme.Blend(fill, Theme.Accent, .16)), null, box, 4, 4);
                VisualText.Center(g, this, PowerAction.All[i].Label, 14, current ? Theme.Blend(Theme.Accent, Colors.White, .22) : hover == i ? Theme.Text : Theme.Second, box);
                left += widths[i];
            }
            g.Pop(); g.Pop();
        }
    }
}
