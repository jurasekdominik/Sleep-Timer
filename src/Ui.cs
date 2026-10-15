using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SleepTimer
{
    static class Theme
    {
        public const string DefaultAccent = "#0CD29D";
        public static readonly Color Bg = Hex("#111111"), Text = Hex("#EDEDED");
        public static Color Accent = Hex(DefaultAccent);
        public static Color Surface => Blend(Bg, Text, .05);
        public static Color Border => Blend(Bg, Text, .15);
        public static Color Second => Blend(Bg, Text, .58);
        public static Color Faint => Blend(Bg, Text, .4);
        public static Color Danger => Hex(Accent.R > Accent.G * 1.2 ? "#FF3B30" : "#E0685C");
        public static Color Hex(string s) => (Color)ColorConverter.ConvertFromString(s);
        public static Color Blend(Color a, Color b, double t) => Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
        public static SolidColorBrush Brush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        public static double Contrast(Color a, Color b)
        {
            double L(Color c)
            {
                double Ch(byte v) { double x = v / 255d; return x <= .03928 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); }
                return .2126 * Ch(c.R) + .7152 * Ch(c.G) + .0722 * Ch(c.B);
            }
            double p = L(a), q = L(b);
            return (Math.Max(p, q) + .05) / (Math.Min(p, q) + .05);
        }
        public static Color TimerColor(TimerTone tone) => tone == TimerTone.Idle ? Blend(Bg, Accent, .55)
            : tone == TimerTone.Paused ? Second : tone == TimerTone.Warning ? Hex("#FFB142")
            : tone == TimerTone.Urgent ? Danger : Accent;

        public static void Init(Application app)
        {
            string saved = Settings.Get("accent", DefaultAccent);
            if (System.Text.RegularExpressions.Regex.IsMatch(saved, "^#[0-9A-Fa-f]{6}$")) Accent = Hex(saved);
            app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(Styles));
            UpdateResources(app.Resources);
        }
        public static void UpdateResources(ResourceDictionary r)
        {
            r["BgBrush"] = Brush(Bg); r["SurfaceBrush"] = Brush(Surface);
            r["TextBrush"] = Brush(Text); r["SecondBrush"] = Brush(Second);
            r["BorderBrush"] = Brush(Border); r["AccentBrush"] = Brush(Accent);
            r["Surface2Brush"] = Brush(Blend(Bg, Text, .09));
            r["FaintBrush"] = Brush(Faint);
            r["DangerSoftBrush"] = Brush(Blend(Bg, Danger, .14));
            r["MenuEdgeBrush"] = Brush(Blend(Border, Blend(Bg, Text, .24), .8));
            r["SoftBrush"] = Brush(Blend(Surface, Accent, .18));
            r["OnAccentBrush"] = Brush(Contrast(Accent, Bg) >= Contrast(Accent, Colors.White) ? Bg : Colors.White);
        }
        const string Styles = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
 <Style TargetType='Window'><Setter Property='Foreground' Value='{DynamicResource TextBrush}'/><Setter Property='FontFamily' Value='Segoe UI'/><Setter Property='FontSize' Value='14'/></Style>
 <Style TargetType='TextBlock'>
  <Setter Property='FontFamily' Value='Segoe UI'/>
 </Style>
 <Style TargetType='Button'>
  <Setter Property='Foreground' Value='{DynamicResource TextBrush}'/>
  <Setter Property='Background' Value='{DynamicResource SurfaceBrush}'/>
  <Setter Property='BorderBrush' Value='{DynamicResource BorderBrush}'/>
  <Setter Property='Padding' Value='12,7'/><Setter Property='Cursor' Value='Hand'/>
  <Setter Property='FontFamily' Value='Segoe UI'/>
  <Setter Property='FontSize' Value='14'/>
  <Setter Property='HorizontalContentAlignment' Value='Center'/>
  <Setter Property='VerticalContentAlignment' Value='Center'/>
  <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
  <Setter Property='Template'><Setter.Value>
   <ControlTemplate TargetType='Button'>
    <Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
     BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='8' Padding='{TemplateBinding Padding}'>
     <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/>
    </Border>
    <ControlTemplate.Triggers>
     <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource SoftBrush}'/></Trigger>
     <Trigger Property='IsPressed' Value='True'><Setter TargetName='box' Property='Opacity' Value='.65'/></Trigger>
     <Trigger Property='IsEnabled' Value='False'><Setter TargetName='box' Property='Opacity' Value='.4'/></Trigger>
    </ControlTemplate.Triggers>
   </ControlTemplate>
  </Setter.Value></Setter>
 </Style>
 <Style x:Key='PrimaryButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
  <Setter Property='BorderThickness' Value='0'/><Setter Property='FontWeight' Value='SemiBold'/>
  <Setter Property='Background' Value='{DynamicResource AccentBrush}'/>
  <Setter Property='Foreground' Value='{DynamicResource OnAccentBrush}'/>
 </Style>
 <Style TargetType='TextBox'>
  <Setter Property='Foreground' Value='{DynamicResource TextBrush}'/>
  <Setter Property='Background' Value='{DynamicResource SurfaceBrush}'/>
  <Setter Property='BorderBrush' Value='{DynamicResource BorderBrush}'/>
  <Setter Property='CaretBrush' Value='{DynamicResource AccentBrush}'/>
  <Setter Property='Padding' Value='9,3'/><Setter Property='FontSize' Value='14'/>
  <Setter Property='FontFamily' Value='Segoe UI'/><Setter Property='VerticalContentAlignment' Value='Center'/>
  <Setter Property='MinHeight' Value='32'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'>
   <Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
    BorderThickness='1' CornerRadius='8'>
    <Grid>
     <ScrollViewer x:Name='PART_ContentHost' Focusable='False' Margin='0' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/>
     <TextBlock x:Name='hint' Text='{Binding Tag, RelativeSource={RelativeSource TemplatedParent}}' Margin='{TemplateBinding Padding}' VerticalAlignment='Center' Foreground='{DynamicResource FaintBrush}' IsHitTestVisible='False' Visibility='Collapsed'/>
    </Grid>
   </Border>
   <ControlTemplate.Triggers>
    <Trigger Property='Text' Value=''><Setter TargetName='hint' Property='Visibility' Value='Visible'/></Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource AccentBrush}'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter TargetName='box' Property='Opacity' Value='.4'/></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>

 <Style x:Key='TextButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
  <Setter Property='BorderThickness' Value='0'/><Setter Property='Background' Value='Transparent'/>
  <Setter Property='Padding' Value='0'/><Setter Property='Template'><Setter.Value>
   <ControlTemplate TargetType='Button'>
    <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/>
    <ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers>
   </ControlTemplate>
  </Setter.Value></Setter>
 </Style>
 <Style x:Key='MenuButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
  <Setter Property='BorderThickness' Value='0'/><Setter Property='Background' Value='Transparent'/>
  <Setter Property='Padding' Value='12,0'/><Setter Property='Height' Value='28'/>
  <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
   <Border x:Name='box' CornerRadius='6' Margin='4,0' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'>
    <ContentPresenter HorizontalAlignment='Stretch' VerticalAlignment='Center'/>
   </Border>
   <ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource SoftBrush}'/></Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource SoftBrush}'/></Trigger>
    <MultiTrigger><MultiTrigger.Conditions><Condition Property='Tag' Value='danger'/><Condition Property='IsMouseOver' Value='True'/></MultiTrigger.Conditions><Setter TargetName='box' Property='Background' Value='{DynamicResource DangerSoftBrush}'/></MultiTrigger>
    <Trigger Property='IsEnabled' Value='False'><Setter TargetName='box' Property='Opacity' Value='.4'/></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style x:Key='ChipButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
  <Setter Property='Foreground' Value='{DynamicResource SecondBrush}'/><Setter Property='BorderThickness' Value='0'/>
  <Setter Property='Padding' Value='16,0,14,0'/><Setter Property='Height' Value='32'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
   <Border x:Name='box' CornerRadius='16' Background='{TemplateBinding Background}' BorderThickness='{TemplateBinding BorderThickness}' BorderBrush='{TemplateBinding BorderBrush}' Padding='{TemplateBinding Padding}'>
    <Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='12'/></Grid.ColumnDefinitions>
     <ContentPresenter HorizontalAlignment='Left' VerticalAlignment='Center'/>
     <Path Grid.Column='1' Data='M 1,4 L 5,8 L 9,4' Stroke='{TemplateBinding Foreground}' StrokeThickness='1.6' StrokeStartLineCap='Round' StrokeEndLineCap='Round' VerticalAlignment='Center'/>
    </Grid>
   </Border>
   <ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource Surface2Brush}'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter TargetName='box' Property='Opacity' Value='.4'/></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style x:Key='SwatchButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
  <Setter Property='BorderThickness' Value='2'/><Setter Property='BorderBrush' Value='Transparent'/>
  <Setter Property='Padding' Value='2'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
   <Border x:Name='box' CornerRadius='20' Background='Transparent' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='2' Padding='2'>
    <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
   </Border>
   <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource FaintBrush}'/></Trigger></ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style x:Key='IconButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
  <Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='0'/><Setter Property='Background' Value='Transparent'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
   <Border x:Name='box' CornerRadius='15' Background='{TemplateBinding Background}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>
   <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource SurfaceBrush}'/></Trigger></ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='ToggleButton'>
  <Setter Property='Width' Value='44'/><Setter Property='Height' Value='24'/>
  <Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ToggleButton'>
   <Grid x:Name='box'>
    <Border x:Name='track' CornerRadius='12' Background='{DynamicResource BorderBrush}'/>
    <Border x:Name='thumb' Width='28' Height='20' CornerRadius='10' Background='White'
     HorizontalAlignment='Left' Margin='2,0,0,0'>
     <Border.RenderTransform><TranslateTransform x:Name='slide'/></Border.RenderTransform>
    </Border>
   </Grid>
   <ControlTemplate.Triggers>
    <Trigger Property='IsChecked' Value='True'>
     <Setter TargetName='track' Property='Background' Value='{DynamicResource AccentBrush}'/>
     <Trigger.EnterActions><BeginStoryboard><Storyboard>
      <DoubleAnimation Storyboard.TargetName='slide' Storyboard.TargetProperty='X' To='12' Duration='0:0:0.24'>
       <DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseOut'/></DoubleAnimation.EasingFunction>
      </DoubleAnimation>
     </Storyboard></BeginStoryboard></Trigger.EnterActions>
     <Trigger.ExitActions><BeginStoryboard><Storyboard>
      <DoubleAnimation Storyboard.TargetName='slide' Storyboard.TargetProperty='X' To='0' Duration='0:0:0.24'>
       <DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseOut'/></DoubleAnimation.EasingFunction>
      </DoubleAnimation>
     </Storyboard></BeginStoryboard></Trigger.ExitActions>
    </Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter TargetName='box' Property='Opacity' Value='.4'/></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
</ResourceDictionary>";
    }

    /// <summary>WPF animation-clock-driven vector dial. No 16 ms WinForms repaint timer.</summary>
    sealed class TimerFace : FrameworkElement
    {
        public const int EditMilliseconds = AnimationTiming.DefaultMilliseconds;
        public int CircleMilliseconds { get; set; } = EditMilliseconds;
        public int CounterMilliseconds { get; set; } = EditMilliseconds;
        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
            "Position", typeof(double), typeof(TimerFace), new FrameworkPropertyMetadata(1800d, FrameworkPropertyMetadataOptions.AffectsRender));
        public double Position { get => (double)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
        public static readonly DependencyProperty CounterPositionProperty = DependencyProperty.Register(
            "CounterPosition", typeof(double), typeof(TimerFace), new FrameworkPropertyMetadata(1800d, FrameworkPropertyMetadataOptions.AffectsRender));
        public double CounterPosition { get => (double)GetValue(CounterPositionProperty); set => SetValue(CounterPositionProperty, value); }
        public TimerState State;
        public TimerTone Tone;
        public bool ClockMode, Interactive = true;
        public string Title = "", Time = "", Sub = "";
        public event Action Toggle, Cancel;
        public event Action<int> Wheel;
        readonly WheelInput input = new WheelInput();
        readonly Stopwatch edit = new Stopwatch();
        double target = 1800;
        bool hot;

        public TimerFace()
        {
            Width = 320; Height = 328; Focusable = false;
            ToolTip = "Click to start or pause · Right-click to cancel · Scroll to adjust";
            MouseMove += (s, e) => { bool next = InRing(e.GetPosition(this)); if (hot != next) { hot = next; Cursor = hot ? Cursors.Hand : Cursors.Arrow; InvalidateVisual(); } };
            MouseLeave += (s, e) => { hot = false; InvalidateVisual(); };
            MouseDown += (s, e) =>
            {
                if (!InRing(e.GetPosition(this))) return;
                if (e.ChangedButton == MouseButton.Left) Toggle?.Invoke();
                else if (e.ChangedButton == MouseButton.Right && State != TimerState.Idle) Cancel?.Invoke();
                e.Handled = true;
            };
            MouseWheel += (s, e) =>
            {
                if (!Interactive) return;
                var keys = Keyboard.Modifiers;
                int minutes = (keys & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift) ? 60
                    : (keys & ModifierKeys.Control) != 0 ? 30 : (keys & ModifierKeys.Shift) != 0 ? 15
                    : (keys & ModifierKeys.Alt) != 0 ? 1 : 5;
                int delta = input.Add(e.Delta, minutes * 60);
                if (delta != 0) Wheel?.Invoke(delta);
                e.Handled = true;
            };
        }

        bool InRing(Point p) => Interactive && Math.Pow(p.X - ActualWidth / 2, 2) + Math.Pow(p.Y - ActualHeight / 2, 2) <= 158 * 158;

        public void UpdatePosition(double seconds, bool animate, bool running)
        {
            seconds = Math.Max(0, Math.Min(Countdown.MaxSet, seconds));
            int circle = AnimationTiming.Normalize(CircleMilliseconds), counter = AnimationTiming.Normalize(CounterMilliseconds);
            if (animate && IsVisible)
            {
                edit.Restart();
                Animate(PositionProperty, seconds, circle, true);
                Animate(CounterPositionProperty, seconds, counter, true);
            }
            else
            {
                bool holdingCircle = IsVisible && edit.IsRunning && edit.ElapsedMilliseconds < circle;
                bool holdingCounter = IsVisible && edit.IsRunning && edit.ElapsedMilliseconds < counter;
                // Each animation has its own clock duration; status refreshes must not interrupt either.
                if (!holdingCircle)
                {
                    if (running && IsVisible) Animate(PositionProperty, seconds, 100, false);
                    else if (seconds != target || !IsVisible) SetPosition(PositionProperty, seconds);
                }
                if (!holdingCounter)
                {
                    if (seconds != target || !IsVisible || edit.IsRunning) SetPosition(CounterPositionProperty, seconds);
                }
                if (!holdingCircle && !holdingCounter) edit.Reset();
            }
            target = seconds;
            InvalidateVisual();
        }

        void Animate(DependencyProperty property, double seconds, int milliseconds, bool eased)
        {
            if (milliseconds == 0) { SetPosition(property, seconds); return; }
            double from = (double)GetValue(property);
            var motion = new DoubleAnimation(from, seconds, TimeSpan.FromMilliseconds(milliseconds));
            if (eased) motion.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(property, motion, HandoffBehavior.SnapshotAndReplace);
        }
        void SetPosition(DependencyProperty property, double seconds)
        {
            BeginAnimation(property, null); SetValue(property, seconds);
        }
        public void Snap(double seconds)
        {
            edit.Reset(); target = seconds;
            SetPosition(PositionProperty, seconds); SetPosition(CounterPositionProperty, seconds);
            InvalidateVisual();
        }

        public string DisplayTime => !ClockMode && edit.IsRunning
            && edit.ElapsedMilliseconds < AnimationTiming.Normalize(CounterMilliseconds)
            ? TimeText.Clock(Math.Round(CounterPosition / 60) * 60) : Time;

        protected override void OnRender(DrawingContext g)
        {
            base.OnRender(g);
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            const double radius = 140, stroke = 16;
            Color color = Theme.TimerColor(Tone);
            g.DrawEllipse(null, new Pen(Theme.Brush(hot ? Theme.Border : Theme.Blend(Theme.Bg, Theme.Text, .09)), stroke), center, radius, radius);
            g.DrawEllipse(null, new Pen(Theme.Brush(Theme.Border), 1), center, radius + 10, radius + 10);
            double shown = TimerDial.ArcFraction(Position);
            if (shown > .000001) DrawArc(g, center, radius, -90, shown * 360, stroke, color);
            for (int i = 0; i < 60; i++)
            {
                double angle = (i * 6 - 90) * Math.PI / 180;
                bool major = i % 5 == 0;
                var pen = new Pen(Theme.Brush(Theme.Blend(Theme.Bg, color, major ? .65 : .28)), major ? 1.7 : 1) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                g.DrawLine(pen, Polar(center, radius + 12, angle), Polar(center, radius + 12 + (major ? 6 : 3), angle));
            }
            double hours = Math.Max(0, Position / 3600);
            for (int i = 0; i < (int)Math.Ceiling(hours); i++)
            {
                double opacity = Math.Min(1, hours - i);
                var path = Sector(center, 129, 119, -90 + i * 15, 15);
                g.DrawGeometry(Theme.Brush(Theme.Blend(Theme.Bg, color, opacity)),
                    new Pen(Theme.Brush(Theme.Blend(Theme.Bg, Theme.Blend(Theme.Border, color, .3), opacity)), 1), path);
            }
            DrawText(g, Title, center.Y - 96, 12, Theme.Second, true);
            DrawText(g, DisplayTime, center.Y - 66, 45.3, Theme.Text);
            DrawText(g, Sub, center.Y + 51, 14, Theme.Second);
        }
        void DrawText(DrawingContext g, string text, double y, double size, Color color, bool bold = false)
        {
            var ft = new FormattedText(text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
                size, Theme.Brush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            ft.MaxTextWidth = ActualWidth; ft.TextAlignment = TextAlignment.Center;
            g.DrawText(ft, new Point(0, y));
        }
        static Point Polar(Point c, double r, double a) => new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
        public static void DrawArc(DrawingContext g, Point c, double r, double start, double sweep, double width, Color color)
        {
            var pen = new Pen(Theme.Brush(color), width) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            if (sweep >= 359.9999) { g.DrawEllipse(null, pen, c, r, r); return; }
            var path = new StreamGeometry();
            using (var p = path.Open())
            {
                p.BeginFigure(Polar(c, r, start * Math.PI / 180), false, false);
                p.ArcTo(Polar(c, r, (start + sweep) * Math.PI / 180), new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise, true, false);
            }
            path.Freeze(); g.DrawGeometry(null, pen, path);
        }
        static Geometry Sector(Point c, double outer, double inner, double start, double sweep)
        {
            double a = start * Math.PI / 180, b = (start + sweep) * Math.PI / 180;
            var path = new StreamGeometry();
            using (var p = path.Open())
            {
                p.BeginFigure(Polar(c, outer, a), true, true);
                p.ArcTo(Polar(c, outer, b), new Size(outer, outer), 0, false, SweepDirection.Clockwise, true, false);
                p.LineTo(Polar(c, inner, b), true, false);
                p.ArcTo(Polar(c, inner, a), new Size(inner, inner), 0, false, SweepDirection.Counterclockwise, true, false);
            }
            path.Freeze(); return path;
        }
    }

    sealed class MenuRow
    {
        public string Text, Right;
        public bool Selected, Header = false, Separator, Danger, KeepOpen, Enabled = true;
        public Action Click;
    }

    /// <summary>A native WPF popup; scrolling, keyboard access and rounded edges share one visual tree.</summary>
    sealed class PopupMenu : IDisposable
    {
        readonly Popup popup = new Popup { AllowsTransparency = true, StaysOpen = false, PopupAnimation = PopupAnimation.None };
        readonly Border border;
        readonly StackPanel items = new StackPanel();
        readonly Stopwatch closed = new Stopwatch();
        string rowSignature;
        public bool Visible => popup.IsOpen;
        public bool JustClosed => closed.IsRunning && closed.ElapsedMilliseconds < 180;
        public PopupMenu(int width = 224)
        {
            border = new Border { Width = width, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 4, 0, 4), Child = new ScrollViewer { Content = items, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 520 } };
            border.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "MenuEdgeBrush");
            popup.Child = border;
            popup.Closed += (s, e) => closed.Restart();
            popup.Opened += (s, e) =>
            {
                border.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
                items.Children.OfType<Button>().FirstOrDefault(b => b.IsEnabled)?.Focus();
            };
            border.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        }
        public void SetRows(IEnumerable<MenuRow> rows)
        {
            var next = rows.ToArray();
            string signature = string.Join("|", next.Select(r => r.Text + "~" + r.Right + "~" + r.Selected + "~" + r.Enabled + "~" + r.Separator + "~" + r.Header));
            if (signature == rowSignature) return;
            rowSignature = signature;
            items.Children.Clear();
            foreach (var row in next)
            {
                if (row.Separator) { items.Children.Add(new Border { Height = 1, Background = Theme.Brush(Theme.Border), Margin = new Thickness(12, 4, 12, 4) }); continue; }
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = new TextBlock { Text = row.Text, FontSize = 14, FontWeight = row.Selected ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = Theme.Brush(row.Danger ? Theme.Danger : row.Selected ? Theme.Accent : Theme.Text), VerticalAlignment = VerticalAlignment.Center };
                grid.Children.Add(label);
                if (!string.IsNullOrEmpty(row.Right))
                {
                    var right = new TextBlock { Text = row.Right, FontSize = 14, Margin = new Thickness(12, 0, 0, 0), Foreground = Theme.Brush(Theme.Faint), VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(right, 1); grid.Children.Add(right);
                }
                if (row.Header)
                {
                    label.Foreground = Theme.Brush(Theme.Second);
                    grid.Margin = new Thickness(16, 7, 16, 7);
                    var arrow = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 0,4 L 4,0 L 8,4"),
                        Stroke = Theme.Brush(Theme.Second), StrokeThickness = 1.6, Width = 8, Height = 4, VerticalAlignment = VerticalAlignment.Center,
                        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
                    Grid.SetColumn(arrow, 1); grid.Children.Add(arrow);
                    items.Children.Add(grid); continue;
                }
                var button = new Button { Content = grid, IsEnabled = row.Enabled, Tag = row.Danger ? "danger" : null, Style = (Style)Application.Current.FindResource("MenuButton"),
                    Background = row.Selected ? Theme.Brush(Theme.Blend(Theme.Surface, Theme.Accent, .06)) : Brushes.Transparent };
                button.MouseEnter += (s, e) => label.Foreground = Theme.Brush(row.Danger ? Theme.Blend(Theme.Danger, Colors.White, .3) : Theme.Blend(Theme.Accent, Colors.White, .22));
                button.MouseLeave += (s, e) => label.Foreground = Theme.Brush(row.Danger ? Theme.Blend(Theme.Danger, Colors.White, .3) : row.Selected ? Theme.Accent : Theme.Text);
                button.Click += (s, e) => { if (!row.KeepOpen) Close(); row.Click?.Invoke(); };
                items.Children.Add(button);
            }
        }
        public void Show(IEnumerable<MenuRow> rows, UIElement anchor = null, bool above = false)
        {
            var entries = rows.ToArray();
            bool overlay = anchor != null && entries.Length > 0 && entries[0].Header;
            SetRows(entries);
            popup.PlacementTarget = anchor;
            popup.Placement = anchor == null ? PlacementMode.MousePoint : overlay ? PlacementMode.Relative : above ? PlacementMode.Top : PlacementMode.Bottom;
            popup.HorizontalOffset = anchor == null ? -border.Width : 0;
            popup.VerticalOffset = anchor == null ? -border.DesiredSize.Height : overlay ? 0 : 4;
            // Measure before opening so the tray popup has a real size on the first show.
            border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (anchor == null) popup.VerticalOffset = -border.DesiredSize.Height;
            popup.IsOpen = true;
        }
        public void Close() => popup.IsOpen = false;
        public void Dispose() { Close(); popup.Child = null; }
    }
}
