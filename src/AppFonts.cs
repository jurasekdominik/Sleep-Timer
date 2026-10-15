using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SleepTimer
{
    sealed class AppFont
    {
        public readonly string Key, Name, Family;
        public readonly bool ForUi;
        public readonly Typeface Typeface, BoldTypeface;
        public readonly FontWeight[] Weights;
        public AppFont(string key, string family, bool forUi = true)
            : this(key, family, forUi, new FontFamily(family).GetTypefaces()
                .Where(face => face.Style == FontStyles.Normal && face.Stretch == FontStretches.Normal)
                .Select(face => face.Weight).Distinct().OrderBy(weight => weight.ToOpenTypeWeight()).ToArray(), FontWeights.Normal) { }
        AppFont(string key, string family, bool forUi, FontWeight[] weights, FontWeight requested)
        {
            Key = key; Name = Family = family; ForUi = forUi;
            Weights = weights.Length == 0 ? new[] { FontWeights.Normal } : weights;
            var fallback = new FontFamily(family == "Segoe UI" ? family : family + ", Segoe UI");
            Typeface = new Typeface(fallback, FontStyles.Normal, Nearest(requested), FontStretches.Normal);
            BoldTypeface = new Typeface(fallback, FontStyles.Normal, Nearest(FontWeights.SemiBold), FontStretches.Normal);
        }
        FontWeight Nearest(FontWeight requested) => Weights.OrderBy(weight =>
            Math.Abs(weight.ToOpenTypeWeight() - requested.ToOpenTypeWeight())).First();
        public AppFont WithWeight(int weight) => new AppFont(Key, Family, ForUi, Weights,
            FontWeight.FromOpenTypeWeight(weight >= 1 && weight <= 999 ? weight : 400));
        public static string WeightName(FontWeight weight) => weight == FontWeights.Normal ? "Regular"
            : weight.ToOpenTypeWeight() == 350 ? "Semilight" : weight.ToString();
    }

    static class AppFonts
    {
        static readonly HashSet<string> installed = new HashSet<string>(
            Fonts.SystemFontFamilies.Select(font => font.Source), StringComparer.OrdinalIgnoreCase);
        public static readonly AppFont[] All = new[] {
            ("segoe", "Segoe UI", true), ("aptos", "Aptos", true),
            ("calibri", "Calibri", true), ("corbel", "Corbel", true),
            ("tahoma", "Tahoma", true), ("arial", "Arial", true), ("verdana", "Verdana", true),
            ("bahnschrift", "Bahnschrift", false), ("consolas", "Consolas", false),
            ("cascadia-mono", "Cascadia Mono", false)
        }.Where(font => font.Item1 == "segoe" || installed.Contains(font.Item2))
            .Select(font => new AppFont(font.Item1, font.Item2, font.Item3)).ToArray();
        public static IEnumerable<AppFont> Choices(bool ui) => All.Where(font => !ui || font.ForUi);
        public static AppFont Timer = All[0], Ui = All[0];
        public static AppFont Find(string key) => All.FirstOrDefault(font => font.Key == key) ?? All[0];
        public static AppFont Select(string key, bool ui, int weight)
        {
            // Migrate the old separate Segoe weight presets without losing the selection.
            if (weight == -1) weight = key == "light" ? 300 : key == "semilight" ? 350 : key == "semibold" ? 600 : 400;
            var font = Find(key);
            if (ui && !font.ForUi) font = All[0];
            return font.WithWeight(weight);
        }
    }
}
