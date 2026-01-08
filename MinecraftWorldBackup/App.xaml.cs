using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace MinecraftWorldBackup;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly string LogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "crash.log"
    );

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Global exception handlers - save to file
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            LogException("FATAL", ex);
            System.Windows.MessageBox.Show($"Crash logged to:\n{LogPath}", "Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        
        DispatcherUnhandledException += (s, args) =>
        {
            LogException("UI ERROR", args.Exception);
            System.Windows.MessageBox.Show($"Error logged to:\n{LogPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }

    private static void LogException(string type, Exception? ex)
    {
        try
        {
            var log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {type}\n" +
                      $"Message: {ex?.Message}\n" +
                      $"Stack Trace:\n{ex?.StackTrace}\n" +
                      $"Inner Exception: {ex?.InnerException?.Message}\n" +
                      new string('-', 80) + "\n\n";
            File.AppendAllText(LogPath, log);
        }
        catch { }
    }
}
