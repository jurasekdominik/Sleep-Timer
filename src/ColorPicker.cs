using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SleepTimer
{
    sealed class ColorPicker : Window
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

        readonly ColorField field = new ColorField { Width = 476, Height = 150 };
        readonly TextBlock note = new TextBlock { FontSize = 12, Margin = new Thickness(0, 12, 0, 0) };
        readonly Border sample = new Border { Width = 84, Height = 30, CornerRadius = new CornerRadius(8), Margin = new Thickness(12, 0, 0, 0) };
        public event Action<Color> Picked;
        public ColorPicker(Color current)
        {
            Title = "Accent colour"; Width = 540; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; Background = Theme.Brush(Theme.Bg); Foreground = Theme.Brush(Theme.Text);
            var panel = new StackPanel { Margin = new Thickness(16) };
            var tabs = new StackPanel { Orientation = Orientation.Horizontal };
            var paletteTab = new Button { Content = "PALETTE", Margin = new Thickness(0, 0, 8, 0) };
            var mixerTab = new Button { Content = "MIXER" };
            tabs.Children.Add(paletteTab); tabs.Children.Add(mixerTab); panel.Children.Add(tabs);
            var pages = new Grid { Margin = new Thickness(0, 12, 0, 0), Height = 178 };
            var paletteGrid = new Grid();
            for (int x = 0; x < Palette.Length; x++) paletteGrid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int y = 0; y < 5; y++) paletteGrid.RowDefinitions.Add(new RowDefinition());
            for (int x = 0; x < Palette.Length; x++)
                for (int y = 0; y < 5; y++)
                {
                    Color color = Theme.Hex(Palette[x].shades[y]);
                    double contrast = Theme.Contrast(color, Theme.Bg);
                    var chip = new Border { CornerRadius = new CornerRadius(4), Background = Theme.Brush(color),
                        Margin = new Thickness(2), Opacity = contrast >= 4.5 ? 1 : .55, Cursor = Cursors.Hand,
                        ToolTip = Palette[x].hue + " " + (200 + y * 100) + " · " + contrast.ToString("0.0", CultureInfo.InvariantCulture) + " : 1" };
                    chip.MouseEnter += (s, e) => Preview(color);
                    chip.MouseLeftButtonUp += (s, e) => { Picked?.Invoke(color); Close(); };
                    Grid.SetColumn(chip, x); Grid.SetRow(chip, y); paletteGrid.Children.Add(chip);
                }
            var mixer = new StackPanel { Visibility = Visibility.Collapsed };
            mixer.Children.Add(field);
            var hue = new Slider { Minimum = 0, Maximum = 359.9, Height = 22, Margin = new Thickness(0, 5, 0, 0),
                Background = new LinearGradientBrush(new GradientStopCollection {
                    new GradientStop(Colors.Red, 0), new GradientStop(Colors.Yellow, 1d/6), new GradientStop(Colors.Lime, 2d/6),
                    new GradientStop(Colors.Cyan, .5), new GradientStop(Colors.Blue, 4d/6), new GradientStop(Colors.Magenta, 5d/6), new GradientStop(Colors.Red, 1) }, new Point(0, 0), new Point(1, 0)) };
            mixer.Children.Add(hue); pages.Children.Add(paletteGrid); pages.Children.Add(mixer); panel.Children.Add(pages);
            paletteTab.Click += (s, e) => { paletteGrid.Visibility = Visibility.Visible; mixer.Visibility = Visibility.Collapsed; };
            mixerTab.Click += (s, e) => { paletteGrid.Visibility = Visibility.Collapsed; mixer.Visibility = Visibility.Visible; };
            hue.ValueChanged += (s, e) => { field.Hue = hue.Value; field.InvalidateVisual(); Preview(field.Mixed); };
            field.Changed += color => Preview(color);
            var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            note.Width = 370; footer.Children.Add(note); sample.Child = new TextBlock { Text = "Sample", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            footer.Children.Add(sample); panel.Children.Add(footer);
            var apply = new Button { Content = "Apply mixed colour", Margin = new Thickness(0, 12, 0, 0) };
            apply.Click += (s, e) => { Picked?.Invoke(field.Mixed); Close(); }; panel.Children.Add(apply);
            Content = panel;
            var drawingColor = System.Drawing.Color.FromArgb(current.R, current.G, current.B);
            field.Hue = drawingColor.GetHue();
            double max = Math.Max(current.R, Math.Max(current.G, current.B)) / 255d;
            double min = Math.Min(current.R, Math.Min(current.G, current.B)) / 255d;
            field.Value = max; field.Saturation = max == 0 ? 0 : (max - min) / max;
            hue.Value = field.Hue; Preview(current);
            SourceInitialized += (s, e) => Native.DarkTitlebar(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        }
        void Preview(Color color)
        {
            double ratio = Theme.Contrast(color, Theme.Bg);
            note.Text = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2") + " · " + ratio.ToString("0.0", CultureInfo.InvariantCulture)
                + " : 1 · " + (ratio >= 4.5 ? "readable" : ratio >= 3 ? "borderline" : "low contrast");
            note.Foreground = Theme.Brush(ratio >= 4.5 ? Theme.Second : Theme.Danger);
            sample.Background = Theme.Brush(color);
            ((TextBlock)sample.Child).Foreground = Theme.Brush(Theme.Contrast(color, Theme.Bg) >= Theme.Contrast(color, Colors.White) ? Theme.Bg : Colors.White);
        }
    }

    sealed class ColorField : FrameworkElement
    {
        public double Hue, Saturation = 1, Value = 1;
        public Color Mixed => Hsv(Hue, Saturation, Value);
        public event Action<Color> Changed;
        public ColorField()
        {
            Cursor = Cursors.Cross;
            MouseLeftButtonDown += (s, e) => { CaptureMouse(); Pick(e.GetPosition(this)); e.Handled = true; };
            MouseMove += (s, e) => { if (IsMouseCaptured) Pick(e.GetPosition(this)); };
            MouseLeftButtonUp += (s, e) => { if (IsMouseCaptured) { Pick(e.GetPosition(this)); ReleaseMouseCapture(); } };
        }
        void Pick(Point p)
        {
            Saturation = Math.Max(0, Math.Min(1, p.X / ActualWidth));
            Value = Math.Max(0, Math.Min(1, 1 - p.Y / ActualHeight)); InvalidateVisual(); Changed?.Invoke(Mixed);
        }
        public static Color Hsv(double hue, double saturation, double value)
        {
            double c = value * saturation, h = hue / 60, x = c * (1 - Math.Abs(h % 2 - 1)), m = value - c;
            double r = 0, g = 0, b = 0;
            if (h < 1) { r = c; g = x; } else if (h < 2) { r = x; g = c; }
            else if (h < 3) { g = c; b = x; } else if (h < 4) { g = x; b = c; }
            else if (h < 5) { r = x; b = c; } else { r = c; b = x; }
            return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }
        protected override void OnRender(DrawingContext g)
        {
            var rect = new Rect(0, 0, ActualWidth, ActualHeight);
            g.PushClip(new RectangleGeometry(rect, 8, 8));
            g.DrawRectangle(new LinearGradientBrush(Colors.White, Hsv(Hue, 1, 1), new Point(0, 0), new Point(1, 0)), null, rect);
            g.DrawRectangle(new LinearGradientBrush(Colors.Transparent, Colors.Black, new Point(0, 0), new Point(0, 1)), null, rect);
            // The dotted curve marks 4.5:1 contrast for the current hue.
            var path = new StreamGeometry();
            using (var p = path.Open())
                for (int x = 0; x <= ActualWidth; x += 3)
                {
                    double sat = x / ActualWidth, low = 0, high = 1;
                    for (int i = 0; i < 16; i++) { double mid = (low + high) / 2; if (Theme.Contrast(Hsv(Hue, sat, mid), Theme.Bg) >= 4.5) high = mid; else low = mid; }
                    var at = new Point(x, (1 - high) * ActualHeight);
                    if (x == 0) p.BeginFigure(at, false, false); else p.LineTo(at, true, false);
                }
            g.DrawGeometry(null, new Pen(Brushes.White, 1) { DashStyle = DashStyles.Dot }, path);
            g.Pop();
            var marker = new Point(Saturation * ActualWidth, (1 - Value) * ActualHeight);
            g.DrawEllipse(null, new Pen(Brushes.Black, 3), marker, 6, 6);
            g.DrawEllipse(null, new Pen(Brushes.White, 2), marker, 6, 6);
        }
    }
}
