using System.Diagnostics;
using Avalonia;

namespace Hypricing.Desktop;

class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (Array.Exists(args, a => a is "--version" or "-V"))
        {
            var v = typeof(Program).Assembly.GetName().Version;
            Console.WriteLine($"hypricing {v?.ToString(3) ?? "dev"}");
            return 0;
        }

        if (Array.Exists(args, a => a is "--verbose" or "-v"))
            Trace.Listeners.Add(new ConsoleTraceListener(useErrorStream: true));

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
