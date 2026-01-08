using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MinecraftWorldBackup.Models;

/// <summary>
/// Application configuration persisted to JSON
/// </summary>
public class AppConfiguration
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MinecraftWorldBackup",
        "config.json"
    );

    /// <summary>
    /// Path to ATLauncher instances folder
    /// </summary>
    public string ATLauncherInstancesPath { get; set; } = @"D:\Games\ATLauncher\instances";

    /// <summary>
    /// Path where backups will be stored
    /// </summary>
    public string BackupPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "MinecraftBackups"
    );

    /// <summary>
    /// Polling interval in minutes
    /// </summary>
    public int PollingIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// Maximum number of backups to keep per world
    /// </summary>
    public int MaxBackupsPerWorld { get; set; } = 10;

    /// <summary>
    /// List of world unique IDs that are selected for backup
    /// </summary>
    public List<string> SelectedWorldIds { get; set; } = [];

    /// <summary>
    /// Last known timestamps for each world (for change detection across restarts)
    /// </summary>
    public Dictionary<string, long> LastKnownTimestamps { get; set; } = [];

    /// <summary>
    /// Whether to start the application minimized to tray
    /// </summary>
    public bool StartMinimized { get; set; } = false;

    /// <summary>
    /// Whether Google Drive sync is enabled
    /// </summary>
    public bool EnableGoogleDrive { get; set; } = false;

    /// <summary>
    /// Whether to also create local ZIP backups when using Google Drive
    /// </summary>
    public bool AlsoBackupLocally { get; set; } = true;

    /// <summary>
    /// Loads configuration from disk, or creates default if not exists
    /// </summary>
    public static AppConfiguration Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfiguration>(json) ?? new AppConfiguration();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
        }

        return new AppConfiguration();
    }

    /// <summary>
    /// Saves configuration to disk
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a world is selected for backup
    /// </summary>
    public bool IsWorldSelected(string worldUniqueId) => SelectedWorldIds.Contains(worldUniqueId);

    /// <summary>
    /// Toggles a world's selection state
    /// </summary>
    public void ToggleWorldSelection(string worldUniqueId)
    {
        if (SelectedWorldIds.Contains(worldUniqueId))
        {
            SelectedWorldIds.Remove(worldUniqueId);
        }
        else
        {
            SelectedWorldIds.Add(worldUniqueId);
        }
        Save();
    }

    /// <summary>
    /// Updates the last known timestamp for a world
    /// </summary>
    public void UpdateLastKnownTimestamp(string worldUniqueId, long timestamp)
    {
        LastKnownTimestamps[worldUniqueId] = timestamp;
        Save();
    }

    /// <summary>
    /// Gets the last known timestamp for a world
    /// </summary>
    public long GetLastKnownTimestamp(string worldUniqueId)
    {
        return LastKnownTimestamps.TryGetValue(worldUniqueId, out var timestamp) ? timestamp : 0;
    }
}
