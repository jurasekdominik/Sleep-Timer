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
        public static FontFamily Mono => new FontFamily(Fonts.SystemFontFamilies.Any(f => f.Source == "Cascadia Code") ? "Cascadia Code"
            : Fonts.SystemFontFamilies.Any(f => f.Source == "Cascadia Mono") ? "Cascadia Mono" : "Consolas");
        public static FormattedText Make(Visual owner, string text, double size, Color color, bool bold = false, bool mono = false) =>
            new FormattedText(text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(mono ? Mono : new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
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

    sealed class ModeSelector : FrameworkElement
    {
        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register("Progress", typeof(double), typeof(ModeSelector),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
        public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
        public event Action<bool> Changed;
        readonly string leftText, rightText;
        bool right;
        int hover = -1;
        public ModeSelector(string left = "Duration", string rightLabel = "At time")
        {
            leftText = left; rightText = rightLabel;
            Width = Design.ModeWidth; Height = Design.Field; Cursor = Cursors.Hand; Focusable = true;
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
            if (animate) BeginAnimation(ProgressProperty, new DoubleAnimation(Progress, clock ? 1 : 0, TimeSpan.FromMilliseconds(Design.TabMotion)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else { BeginAnimation(ProgressProperty, null); Progress = clock ? 1 : 0; }
        }
        protected override void OnRender(DrawingContext g)
        {
            double half = ActualWidth / 2;
            g.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, Design.Field));
            for (int i = 0; i < 2; i++)
            {
                double amount = i == 1 ? Progress : 1 - Progress;
                Color color = Theme.Blend(hover == i ? Theme.Text : Theme.Second, Theme.SmallAccent, amount);
                if (!IsEnabled) color = Theme.Blend(color, Theme.Bg, .5);
                VisualText.Center(g, this, i == 0 ? leftText : rightText, Design.Label, color, new Rect(i * half, 0, half, Design.Field - Design.Space));
            }
            double leftWidth = VisualText.Width(this, leftText, Design.Label), rightWidth = VisualText.Width(this, rightText, Design.Label);
            double width = leftWidth + (rightWidth - leftWidth) * Progress;
            g.DrawRectangle(Theme.Brush(IsEnabled ? Theme.Accent : Theme.Faint), null,
                new Rect((half - width) / 2 + Progress * half, Design.Field - Design.Space / 2, width, Design.Space / 2));
        }
    }
}
