using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SleepTimer
{
    sealed class FontWindow : Window
    {
        readonly Action<string, bool> chooseFamily;
        readonly Action<int, bool> chooseWeight;
        readonly PopupMenu weights = new PopupMenu(180);
        readonly StackPanel families = new StackPanel();
        readonly List<Button> choices = new List<Button>();
        readonly Button timerTab = new Button { Content = "TIMER" };
        readonly Button uiTab = new Button { Content = "UI" };
        readonly Border tabUnderline = new Border { Height = 2.5 };
        readonly TranslateTransform tabSlide = new TranslateTransform();
        readonly Button weightButton = new Button { Margin = new Thickness(0, 12, 0, 4), HorizontalAlignment = HorizontalAlignment.Left };
        readonly TextBlock note = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
        public bool IsUi { get; private set; }

        public FontWindow(Window owner, bool ui, Action<string, bool> chooseFamily, Action<int, bool> chooseWeight)
        {
            this.chooseFamily = chooseFamily; this.chooseWeight = chooseWeight;
            Owner = owner; Title = "Fonts"; Width = 420; SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Theme.Brush(Theme.Bg); ShowInTaskbar = false; UseLayoutRounding = SnapsToDevicePixels = true;
            var panel = new StackPanel { Margin = new Thickness(16) };
            var tabs = new Canvas { Height = 24 };
            foreach (var tab in new[] { timerTab, uiTab })
            {
                tab.FontSize = 12; tab.FontWeight = FontWeights.SemiBold; tab.Height = 17;
                tab.Style = (Style)FindResource("TextButton");
                tab.HorizontalContentAlignment = HorizontalAlignment.Left;
                tabs.Children.Add(tab);
            }
            tabUnderline.RenderTransform = tabSlide;
            tabUnderline.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
            Canvas.SetTop(tabUnderline, 18); tabs.Children.Add(tabUnderline); panel.Children.Add(tabs);
            timerTab.Click += (s, e) => SelectScope(false); uiTab.Click += (s, e) => SelectScope(true);
            var available = new TextBlock { Text = "Installed fonts only · no downloads", FontSize = 12, Margin = new Thickness(0, 10, 0, 0) };
            available.SetResourceReference(TextBlock.ForegroundProperty, "FaintBrush"); panel.Children.Add(available);
            panel.Children.Add(weightButton); panel.Children.Add(families);
            weightButton.Click += (s, e) =>
            {
                if (weights.Visible || weights.JustClosed) return;
                var current = IsUi ? AppFonts.Ui : AppFonts.Timer;
                weights.Show(current.Weights.Select(weight => new MenuRow {
                    Text = AppFont.WeightName(weight), Selected = weight == current.Typeface.Weight,
                    Click = () => { chooseWeight(weight.ToOpenTypeWeight(), IsUi); RefreshChoices(); }
                }), weightButton);
            };
            note.SetResourceReference(TextBlock.ForegroundProperty, "FaintBrush"); panel.Children.Add(note);
            var done = new Button { Content = "Done", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            done.Click += (s, e) => Close(); panel.Children.Add(done);
            Content = new ThemedScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height - 80) };
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
            SourceInitialized += (s, e) => Native.DarkTitlebar(new WindowInteropHelper(this).Handle);
            Closed += (s, e) => weights.Dispose();
            SelectScope(ui);
        }

        public void SelectScope(bool ui)
        {
            IsUi = ui; weights.Close(); families.Children.Clear(); choices.Clear();
            foreach (var font in AppFonts.Choices(ui))
            {
                var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });
                row.Children.Add(new TextBlock { Text = font.Name, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
                var sample = new TextBlock { Text = ui ? "Sleep Timer" : "01:30:00", FontSize = ui ? 22 : 26,
                    FontFamily = font.Typeface.FontFamily, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(sample, 1); row.Children.Add(sample);
                var choice = new Button { Content = row, Height = 56, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(10, 4, 10, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch, Tag = font, ToolTip = font.Name };
                choice.Click += (s, e) => { chooseFamily(font.Key, IsUi); RefreshChoices(); };
                choice.MouseDoubleClick += (s, e) => { chooseFamily(font.Key, IsUi); e.Handled = true; Close(); };
                choices.Add(choice); families.Children.Add(choice);
            }
            note.Text = ui ? "UI family and weight. Timer digits keep their own font. Double-click to select and close."
                : "Timer family and weight. The UI keeps its own font. Double-click to select and close.";
            RefreshChoices();
        }
        void RefreshChoices()
        {
            double timerWidth = VisualText.Make(this, "TIMER", 12, Theme.Text, true).WidthIncludingTrailingWhitespace;
            double uiWidth = VisualText.Make(this, "UI", 12, Theme.Text, true).WidthIncludingTrailingWhitespace;
            timerTab.Width = timerWidth + 4; uiTab.Width = uiWidth + 4;
            Canvas.SetLeft(uiTab, timerWidth + 15);
            timerTab.Foreground = Theme.Brush(IsUi ? Theme.Faint : Theme.Text);
            uiTab.Foreground = Theme.Brush(IsUi ? Theme.Text : Theme.Faint);
            tabSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(tabSlide.X, IsUi ? timerWidth + 15 : 0,
                TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            tabUnderline.BeginAnimation(WidthProperty, new DoubleAnimation(double.IsNaN(tabUnderline.Width) ? timerWidth : tabUnderline.Width,
                IsUi ? uiWidth : timerWidth, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            var current = IsUi ? AppFonts.Ui : AppFonts.Timer;
            weightButton.Content = "Weight: " + AppFont.WeightName(current.Typeface.Weight) + " ▾";
            foreach (var button in choices)
            {
                var family = (AppFont)button.Tag;
                button.BorderBrush = Theme.Brush(family.Key == current.Key ? Theme.Accent : Theme.Border);
                ((Grid)button.Content).Children.OfType<TextBlock>().Last().FontWeight = family.WithWeight(current.Typeface.Weight.ToOpenTypeWeight()).Typeface.Weight;
            }
        }
    }
}
