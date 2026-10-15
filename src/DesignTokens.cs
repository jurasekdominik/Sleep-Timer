using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SleepTimer
{
    // Shared desktop roles and geometry. Segoe UI and framework components require no bundled assets.
    static class Design
    {
        public const double Space = 4, Gap = 8, BlockPadding = 12, PageEdge = 16, DialogEdge = 24, ColumnGap = 32;
        public const double Caption = 11, Label = 12, Body = 13, Section = 14, Title = 18, Mono = 12;
        public const double Field = 28, Chip = 24, SettingsRow = 32, DescribedRow = 44;
        public const double Radius = 4, CardRadius = 8, Line = 1, Sidebar = 180, CollapsedSidebar = 54, ActiveMarker = 3;
        public const double Icon = 16, NavIcon = 18, IconStroke = 1.8, IconGap = 6;
        public const double SwitchWidth = 32, SwitchHeight = 18, ThumbWidth = 20, ThumbHeight = 14;
        public const double InputWidth = 80, TimeWidth = 80, FieldPairGap = 16, ButtonMin = 72, DialogButtonMin = 96;
        public const double PageWidth = 368, PageHeight = 532, StatusHeight = 28, SettingsContent = 336;
        public const double DialWidth = 320, DialHeight = 328, DialTop = 56, DialLeft = 24, DialType = 45.3;
        public const double ModeTop = 400, AwakeTop = 444, HintTop = 492, ModeWidth = 224, ClockLeft = 240;
        public const double MainActionWidth = 144, MainActionTop = 226, MainActionLeft = 112;
        public const double DefaultMenu = 224, PopupMaxHeight = 520, Motion = 180, TabMotion = 220;
        public static readonly Thickness PageMargin = new Thickness(PageEdge);
        public static readonly Thickness FieldPadding = new Thickness(Gap, 0, Gap, 0);
        public static readonly CornerRadius ControlCorner = new CornerRadius(Radius);
        public static readonly CornerRadius CardCorner = new CornerRadius(CardRadius);
        public static readonly Color Background = Theme.Hex("#151519"), Raised = Theme.Hex("#1B1B20"), Fill = Theme.Hex("#29292F"),
            Separator = Theme.Hex("#35353D"), Outline = Theme.Hex("#606069"), Text = Theme.Hex("#EDEDF0"),
            Text2 = Theme.Hex("#B1B1BC"), Text3 = Theme.Hex("#9999A7"), Success = Theme.Hex("#83CEA5"),
            Warning = Theme.Hex("#FFB142"), Danger = Theme.Hex("#E0685C"), Info = Theme.Hex("#6FA8DC");

        public static TextBlock TextLabel(string text, double size = Body, bool secondary = false) =>
            new TextBlock { Text = text, FontSize = size, Foreground = Theme.Brush(secondary ? Text2 : Text), VerticalAlignment = VerticalAlignment.Center };
        public static Border Divider() => new Border { Height = Line, Background = Theme.Brush(Separator), Opacity = .6, Margin = new Thickness(0, BlockPadding, 0, BlockPadding) };
        public static StackPanel Block(string heading, string help = null)
        {
            var block = new StackPanel { Margin = new Thickness(0, 0, 0, PageEdge) };
            block.Children.Add(Divider());
            block.Children.Add(new TextBlock { Text = heading, FontSize = Section, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, Gap) });
            if (!string.IsNullOrEmpty(help)) block.Children.Add(new TextBlock { Text = help, FontSize = Caption, Foreground = Theme.Brush(Text3), Margin = new Thickness(0, 0, 0, Gap), TextWrapping = TextWrapping.Wrap });
            return block;
        }
    }

    // Inline Lucide paths (ISC, https://lucide.dev). No icon font or runtime package.
    static class Icons
    {
        public const string Timer = "M10 2h4 M12 14v-4 M4.93 4.93l1.41 1.41 M19.07 4.93l-1.41 1.41 M20 14a8 8 0 1 1-16 0a8 8 0 1 1 16 0";
        public const string Settings = "M9.671 4.136a2.34 2.34 0 0 1 4.659 0a2.34 2.34 0 0 0 3.319 1.915a2.34 2.34 0 0 1 2.33 4.033a2.34 2.34 0 0 0 0 3.831a2.34 2.34 0 0 1-2.33 4.033a2.34 2.34 0 0 0-3.319 1.915a2.34 2.34 0 0 1-4.659 0a2.34 2.34 0 0 0-3.32-1.915a2.34 2.34 0 0 1-2.33-4.033a2.34 2.34 0 0 0 0-3.831a2.34 2.34 0 0 1 2.33-4.033a2.34 2.34 0 0 0 3.32-1.915 M15 12a3 3 0 1 1-6 0a3 3 0 1 1 6 0";
        public const string Palette = "M12 3a9 9 0 1 0 0 18h1.5a2.5 2.5 0 0 0 0-5h-1a1.5 1.5 0 0 1 0-3H15a6 6 0 0 0 6-6c0-2.5-4-4-9-4 M7.5 8h.01 M12 6h.01 M16.5 8h.01 M6 12h.01";
        public const string List = "M8 6h13 M8 12h13 M8 18h13 M3 6h.01 M3 12h.01 M3 18h.01";
        public const string ChevronDown = "M6 9l6 6l6-6", ChevronLeft = "M15 18l-6-6l6-6", ChevronRight = "M9 18l6-6l-6-6";
        public const string Check = "M20 6L9 17l-5-5", X = "M18 6L6 18 M6 6l12 12";
        public static FrameworkElement View(string data, double size = Design.Icon, Color? color = null)
        {
            return new Viewbox { Width = size, Height = size, Child = new System.Windows.Shapes.Path {
                Data = Geometry.Parse(data), Stroke = Theme.Brush(color ?? Theme.Second), StrokeThickness = Design.IconStroke,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.None, Width = 24, Height = 24 } };
        }
    }
}
