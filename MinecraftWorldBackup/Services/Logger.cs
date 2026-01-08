using System.IO;

namespace MinecraftWorldBackup.Services;

/// <summary>
/// Simple file-based logger for debugging
/// </summary>
public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MinecraftWorldBackup",
        "app.log"
    );

    private static readonly object _lock = new();

    static Logger()
    {
        var dir = Path.GetDirectoryName(LogPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Error(string message) => Log("ERROR", message);
    public static void Error(string message, Exception ex) => Log("ERROR", $"{message}: {ex.Message}\n{ex.StackTrace}");
    public static void Debug(string message) => Log("DEBUG", message);

    private static void Log(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                File.AppendAllText(LogPath, logLine + Environment.NewLine);
                System.Diagnostics.Debug.WriteLine(logLine);
            }
        }
        catch
        {
            // Ignore logging errors
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(LogPath))
            {
                File.Delete(LogPath);
            }
        }
        catch { }
    }

    public static string GetLogPath() => LogPath;
}
