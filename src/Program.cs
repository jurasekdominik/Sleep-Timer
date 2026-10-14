using System;
using System.Linq;
using System.Reflection;
using System.Windows;

[assembly: AssemblyTitle("Sleep Timer")]
[assembly: AssemblyProduct("Sleep Timer")]
[assembly: AssemblyDescription("Sleep Timer - WPF edition")]
[assembly: AssemblyCopyright("Dominik Jurásek")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace SleepTimer
{
    static class Program
    {
        public const string TrayArg = "--tray";
        public static readonly string Version = typeof(Program).Assembly.GetName().Version.ToString(2);
        [STAThread] static void Main(string[] args)
        {
            try { Native.SetDllDirectory(""); } catch { }
            if (!SingleInstance.Claim()) return;
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Theme.Init(app);
            using (var window = new MainWindow())
            {
                app.MainWindow = window;
                app.SessionEnding += (s, e) => window.EndSession();
                if (args.Contains("--verify-startup"))
                {
                    window.TestMode = true;
                    window.Show(); window.UpdateLayout();
                    return;
                }
                SingleInstance.Listen(() => window.Dispatcher.BeginInvoke((Action)window.ShowWindow));
                if (!args.Contains(TrayArg)) window.Show();
                app.Run();
            }
        }
    }
}
