using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SleepTimer
{
    sealed class ColorPicker
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

        readonly System.Windows.Controls.Primitives.Popup popup = new System.Windows.Controls.Primitives.Popup
        {
            AllowsTransparency = true, StaysOpen = false,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };
        readonly ColorField field = new ColorField { Height = 112 };
        readonly TextBlock hex = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock origin = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock note = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        readonly Border dot = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), VerticalAlignment = VerticalAlignment.Center };
        readonly Border sample = new Border { Width = 84, Height = 30, CornerRadius = new CornerRadius(8) };
        readonly Color original;
        Color preview;
        bool mixerMode;
        public bool IsVisible => popup.IsOpen;
        public event Action<Color> Picked;
        public ColorPicker(Color current)
        {
            original = current;
            var panel = new StackPanel();
            var tabs = new Grid { Height = 22 };
            var tabButtons = new StackPanel { Orientation = Orientation.Horizontal };
            var paletteTab = new Button { Content = "P A L E T T E", FontSize = 12, FontWeight = FontWeights.SemiBold,
                Style = (Style)Application.Current.FindResource("TextButton"), Margin = new Thickness(0, 0, 16, 0) };
            var mixerTab = new Button { Content = "M I X E R", FontSize = 12, FontWeight = FontWeights.SemiBold,
                Style = (Style)Application.Current.FindResource("TextButton") };
            var paletteLine = new Border { Height = 2, VerticalAlignment = VerticalAlignment.Bottom };
            var mixerLine = new Border { Height = 2, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Hidden };
            paletteLine.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
            mixerLine.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
            var paletteHead = new Grid(); paletteHead.Children.Add(paletteTab); paletteHead.Children.Add(paletteLine);
            paletteLine.Margin = new Thickness(0, 0, 16, 0);
            var mixerHead = new Grid(); mixerHead.Children.Add(mixerTab); mixerHead.Children.Add(mixerLine);
            tabButtons.Children.Add(paletteHead); tabButtons.Children.Add(mixerHead); tabs.Children.Add(tabButtons);
            var legend = new TextBlock { Text = "faded = low contrast", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            legend.SetResourceReference(TextBlock.ForegroundProperty, "FaintBrush"); tabs.Children.Add(legend); panel.Children.Add(tabs);
            var pages = new Grid { Margin = new Thickness(0, 8, 0, 0), Height = 140 };
            var paletteGrid = new Grid();
            for (int x = 0; x < Palette.Length; x++) paletteGrid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int y = 0; y < 5; y++) paletteGrid.RowDefinitions.Add(new RowDefinition());
            for (int x = 0; x < Palette.Length; x++)
                for (int y = 0; y < 5; y++)
                {
                    Color color = Theme.Hex(Palette[x].shades[y]);
                    double contrast = Theme.Contrast(color, Theme.Bg);
                    var chip = new Border { CornerRadius = new CornerRadius(6), Background = Theme.Brush(color),
                        Margin = new Thickness(2), Opacity = contrast >= 4.5 ? 1 : .55, Cursor = Cursors.Hand,
                        ToolTip = Palette[x].hue + " " + (200 + y * 100) + " · " + contrast.ToString("0.0", CultureInfo.InvariantCulture) + " : 1" };
                    chip.MouseEnter += (s, e) => Preview(color);
                    chip.MouseLeftButtonUp += (s, e) => { Picked?.Invoke(color); Close(); e.Handled = true; };
                    Grid.SetColumn(chip, x); Grid.SetRow(chip, y); paletteGrid.Children.Add(chip);
                }
            paletteGrid.MouseLeave += (s, e) => Preview(original);
            var mixer = new StackPanel { Visibility = Visibility.Collapsed };
            mixer.Children.Add(field);
            var hue = new HueSlider { Height = 22, Margin = new Thickness(0, 6, 0, 0) };
            mixer.Children.Add(hue); pages.Children.Add(paletteGrid); pages.Children.Add(mixer); panel.Children.Add(pages);
            paletteTab.Click += (s, e) =>
            {
                mixerMode = false;
                paletteGrid.Visibility = Visibility.Visible; mixer.Visibility = Visibility.Collapsed;
                paletteLine.Visibility = Visibility.Visible; mixerLine.Visibility = Visibility.Hidden;
                paletteTab.SetResourceReference(Button.ForegroundProperty, "TextBrush"); mixerTab.SetResourceReference(Button.ForegroundProperty, "SecondBrush");
                legend.Text = "faded = low contrast";
                Preview(original);
            };
            mixerTab.Click += (s, e) =>
            {
                mixerMode = true;
                paletteGrid.Visibility = Visibility.Collapsed; mixer.Visibility = Visibility.Visible;
                paletteLine.Visibility = Visibility.Hidden; mixerLine.Visibility = Visibility.Visible;
                mixerTab.SetResourceReference(Button.ForegroundProperty, "TextBrush"); paletteTab.SetResourceReference(Button.ForegroundProperty, "SecondBrush");
                legend.Text = "below the dotted line = low contrast";
                Preview(field.Mixed);
            };
            mixerTab.SetResourceReference(Button.ForegroundProperty, "SecondBrush");
            hue.Changed += value => { field.Hue = value; field.InvalidateVisual(); Preview(field.Mixed); };
            field.Changed += Preview;
            var separator = new Border { Height = 1, Margin = new Thickness(0, 10, 0, 8) };
            separator.SetResourceReference(Border.BackgroundProperty, "BorderBrush"); panel.Children.Add(separator);
            var footer = new Grid { Height = 30 };
            foreach (double width in new[] { 32d, 84d, 82d, 0d, 84d })
                footer.ColumnDefinitions.Add(new ColumnDefinition { Width = width == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(width) });
            UIElement[] parts = { dot, hex, origin, note, sample };
            for (int i = 0; i < parts.Length; i++) { Grid.SetColumn(parts[i], i); footer.Children.Add(parts[i]); }
            origin.SetResourceReference(TextBlock.ForegroundProperty, "FaintBrush");
            sample.Child = new TextBlock { Text = "Sample", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            sample.Cursor = Cursors.Hand; sample.ToolTip = "Apply this colour";
            sample.MouseLeftButtonUp += (s, e) => { Picked?.Invoke(preview); Close(); e.Handled = true; };
            panel.Children.Add(footer);
            var border = new Border { Width = 504, Padding = new Thickness(12), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Child = panel, Focusable = true, UseLayoutRounding = true, SnapsToDevicePixels = true };
            border.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "MenuEdgeBrush");
            border.SetResourceReference(TextElement.ForegroundProperty, "TextBrush");
            border.SetValue(TextElement.FontFamilyProperty, new FontFamily("Segoe UI"));
            border.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
            popup.Child = border;
            // Keep keyboard focus and the caret in the anchor field until a popup control is clicked.
            var drawingColor = System.Drawing.Color.FromArgb(current.R, current.G, current.B);
            field.Hue = drawingColor.GetHue();
            double max = Math.Max(current.R, Math.Max(current.G, current.B)) / 255d;
            double min = Math.Min(current.R, Math.Min(current.G, current.B)) / 255d;
            field.Value = max; field.Saturation = max == 0 ? 0 : (max - min) / max;
            hue.Value = field.Hue; Preview(current);
        }
        public void Show(UIElement anchor)
        {
            popup.PlacementTarget = anchor; popup.VerticalOffset = 6;
            popup.HorizontalOffset = anchor is FrameworkElement element ? element.ActualWidth - 504 : 0;
            popup.IsOpen = true;
        }
        public void Close() => popup.IsOpen = false;
        void Preview(Color color)
        {
            preview = color;
            double ratio = Theme.Contrast(color, Theme.Bg);
            hex.Text = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
            hex.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            origin.Text = color == original ? "current" : mixerMode ? "mixed" : "preview";
            note.Text = ratio.ToString("0.0", CultureInfo.InvariantCulture) + " : 1 " + (ratio >= 4.5 ? "readable" : ratio >= 3 ? "borderline" : "low contrast");
            note.Foreground = Theme.Brush(ratio >= 4.5 ? Theme.Second : Theme.Danger);
            dot.Background = sample.Background = Theme.Brush(color);
            ((TextBlock)sample.Child).Foreground = Theme.Brush(Theme.Contrast(color, Theme.Bg) >= Theme.Contrast(color, Colors.White) ? Theme.Bg : Colors.White);
        }
    }

    /// <summary>Vector hue strip with a circular handle and no operating-system slider chrome.</summary>
    sealed class HueSlider : FrameworkElement
    {
        static readonly Brush rainbow = CreateRainbow();
        double value;
        public double Value
        {
            get => value;
            set { this.value = Math.Max(0, Math.Min(359.9, value)); InvalidateVisual(); }
        }
        public event Action<double> Changed;
        public HueSlider()
        {
            Focusable = true; Cursor = Cursors.Hand;
            ToolTip = "Hue · drag or use arrow keys";
            MouseLeftButtonDown += (s, e) => { Focus(); CaptureMouse(); Pick(e.GetPosition(this).X); e.Handled = true; };
            MouseMove += (s, e) => { if (IsMouseCaptured) Pick(e.GetPosition(this).X); };
            MouseLeftButtonUp += (s, e) => { if (IsMouseCaptured) { Pick(e.GetPosition(this).X); ReleaseMouseCapture(); e.Handled = true; } };
            KeyDown += (s, e) =>
            {
                double next = Value;
                if (e.Key == Key.Left || e.Key == Key.Down) next -= 1;
                else if (e.Key == Key.Right || e.Key == Key.Up) next += 1;
                else if (e.Key == Key.Home) next = 0;
                else if (e.Key == Key.End) next = 359.9;
                else return;
                Value = next; Changed?.Invoke(Value); e.Handled = true;
            };
        }
        void Pick(double x)
        {
            Value = Math.Max(0, Math.Min(1, (x - 8) / Math.Max(1, ActualWidth - 16))) * 359.9;
            Changed?.Invoke(Value);
        }
        static Brush CreateRainbow()
        {
            var brush = new LinearGradientBrush(new GradientStopCollection {
                new GradientStop(Colors.Red, 0), new GradientStop(Colors.Yellow, 1d / 6),
                new GradientStop(Colors.Lime, 2d / 6), new GradientStop(Colors.Cyan, .5),
                new GradientStop(Colors.Blue, 4d / 6), new GradientStop(Colors.Magenta, 5d / 6),
                new GradientStop(Colors.Red, 1) }, new Point(0, 0), new Point(1, 0));
            brush.Freeze(); return brush;
        }
        protected override void OnRender(DrawingContext g)
        {
            double y = ActualHeight / 2;
            g.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            g.DrawRoundedRectangle(rainbow, null, new Rect(0, y - 7, ActualWidth, 14), 7, 7);
            var point = new Point(8 + Value / 359.9 * Math.Max(1, ActualWidth - 16), y);
            g.DrawEllipse(Theme.Brush(ColorField.Hsv(Value, 1, 1)), new Pen(Theme.Brush(Theme.Blend(Colors.Black, Colors.White, .2)), 3.5), point, 7.5, 7.5);
            g.DrawEllipse(null, new Pen(Brushes.White, 1.8), point, 7.5, 7.5);
        }
    }

    sealed class ColorField : FrameworkElement
    {
        DrawingGroup fieldArtwork;
        double cachedHue, cachedWidth, cachedHeight;
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
            if (fieldArtwork == null || cachedHue != Hue || cachedWidth != ActualWidth || cachedHeight != ActualHeight)
            {
                cachedHue = Hue; cachedWidth = ActualWidth; cachedHeight = ActualHeight;
                var artwork = new DrawingGroup();
                using (var drawing = artwork.Open())
                {
                    drawing.PushClip(new RectangleGeometry(rect, 8, 8));
                    drawing.DrawRectangle(new LinearGradientBrush(Colors.White, Hsv(Hue, 1, 1), new Point(0, 0), new Point(1, 0)), null, rect);
                    drawing.DrawRectangle(new LinearGradientBrush(Colors.Transparent, Colors.Black, new Point(0, 0), new Point(0, 1)), null, rect);
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
                    drawing.DrawGeometry(null, new Pen(Brushes.White, 1) { DashStyle = DashStyles.Dot }, path);
                    drawing.Pop();
                }
                artwork.Freeze(); fieldArtwork = artwork;
            }
            g.DrawDrawing(fieldArtwork);
            var marker = new Point(Saturation * ActualWidth, (1 - Value) * ActualHeight);
            g.DrawEllipse(null, new Pen(Brushes.Black, 3), marker, 6, 6);
            g.DrawEllipse(null, new Pen(Brushes.White, 2), marker, 6, 6);
        }
    }
}
