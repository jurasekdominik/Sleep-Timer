using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Sleep Timer")]
[assembly: AssemblyProduct("Sleep Timer")]
[assembly: AssemblyDescription("Sleep Timer - shut down, sleep or restart on a countdown")]
[assembly: AssemblyCopyright("Dominik Jurásek")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace SleepTimer
{
    static class Program
    {
        public const string TrayArg = "--tray";
        public static readonly string Version = typeof(Program).Assembly.GetName().Version.ToString(2);

        [STAThread]
        static void Main(string[] args)
        {
            // Load DLLs only from the app folder and System32, never from the current directory.
            try { Native.SetDllDirectory(""); } catch { }
            if (!SingleInstance.Claim()) return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Theme.Init();
            string accent = Settings.Get("accent", Theme.DefaultAccent);  // only "#RRGGBB" is accepted
            if (System.Text.RegularExpressions.Regex.IsMatch(accent, "^#[0-9A-Fa-f]{6}$")) Theme.SetAccent(System.Drawing.ColorTranslator.FromHtml(accent));

            var window = new MainForm();
            SingleInstance.Listen(() => window.BeginInvoke((Action)window.ShowWindow));
            if (!args.Contains(TrayArg)) window.Show();
            try { Application.Run(); }
            finally { window.Dispose(); Theme.DisposeFonts(); }
        }
    }
}
