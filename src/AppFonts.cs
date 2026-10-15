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
        public readonly Typeface Typeface, BoldTypeface;
        public AppFont(string key, string name, string family, FontWeight weight)
        {
            Key = key; Name = name; Family = family;
            var fallback = new FontFamily(family == "Segoe UI" ? family : family + ", Segoe UI");
            Typeface = new Typeface(fallback, FontStyles.Normal, weight, FontStretches.Normal);
            BoldTypeface = new Typeface(fallback, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        }
    }

    static class AppFonts
    {
        static readonly HashSet<string> installed = new HashSet<string>(
            Fonts.SystemFontFamilies.Select(font => font.Source), StringComparer.OrdinalIgnoreCase);
        public static readonly AppFont[] All = new[] {
            new AppFont("segoe", "Segoe UI · Regular", "Segoe UI", FontWeights.Normal),
            new AppFont("light", "Segoe UI · Light", "Segoe UI", FontWeights.Light),
            new AppFont("semilight", "Segoe UI · Semilight", "Segoe UI", FontWeight.FromOpenTypeWeight(350)),
            new AppFont("semibold", "Segoe UI · Semibold", "Segoe UI", FontWeights.SemiBold),
            new AppFont("segoe-variable", "Segoe UI Variable", "Segoe UI Variable", FontWeights.Normal),
            new AppFont("segoe-variable-text", "Segoe UI Variable Text", "Segoe UI Variable Text", FontWeights.Normal),
            new AppFont("segoe-variable-display", "Segoe UI Variable Display", "Segoe UI Variable Display", FontWeights.Normal),
            new AppFont("aptos", "Aptos", "Aptos", FontWeights.Normal),
            new AppFont("aptos-display", "Aptos Display", "Aptos Display", FontWeights.Normal),
            new AppFont("aptos-narrow", "Aptos Narrow", "Aptos Narrow", FontWeights.Normal),
            new AppFont("aptos-mono", "Aptos Mono", "Aptos Mono", FontWeights.Normal),
            new AppFont("aptos-serif", "Aptos Serif", "Aptos Serif", FontWeights.Normal),
            new AppFont("bahnschrift", "Bahnschrift", "Bahnschrift", FontWeights.Normal),
            new AppFont("calibri", "Calibri", "Calibri", FontWeights.Normal),
            new AppFont("corbel", "Corbel", "Corbel", FontWeights.Normal),
            new AppFont("candara", "Candara", "Candara", FontWeights.Normal),
            new AppFont("tahoma", "Tahoma", "Tahoma", FontWeights.Normal),
            new AppFont("arial", "Arial", "Arial", FontWeights.Normal),
            new AppFont("verdana", "Verdana", "Verdana", FontWeights.Normal),
            new AppFont("trebuchet", "Trebuchet MS", "Trebuchet MS", FontWeights.Normal),
            new AppFont("century-gothic", "Century Gothic", "Century Gothic", FontWeights.Normal),
            new AppFont("georgia", "Georgia", "Georgia", FontWeights.Normal),
            new AppFont("times", "Times New Roman", "Times New Roman", FontWeights.Normal),
            new AppFont("cambria", "Cambria", "Cambria", FontWeights.Normal),
            new AppFont("constantia", "Constantia", "Constantia", FontWeights.Normal),
            new AppFont("consolas", "Consolas", "Consolas", FontWeights.Normal),
            new AppFont("cascadia-mono", "Cascadia Mono", "Cascadia Mono", FontWeights.Normal),
            new AppFont("cascadia-code", "Cascadia Code", "Cascadia Code", FontWeights.Normal),
            new AppFont("lucida-console", "Lucida Console", "Lucida Console", FontWeights.Normal)
        }.Where(font => font.Key == "segoe" || installed.Contains(font.Family)).ToArray();
        public static AppFont Timer = All[0], Ui = All[0];
        public static AppFont Find(string key) => All.FirstOrDefault(font => font.Key == key) ?? All[0];
    }
}
