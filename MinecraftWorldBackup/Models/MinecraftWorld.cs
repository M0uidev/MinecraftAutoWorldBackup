namespace MinecraftWorldBackup.Models;

/// <summary>
/// Represents a Minecraft world within an instance.
/// </summary>
public class MinecraftWorld
{
    /// <summary>
    /// Full path to the world folder (e.g., .minecraft/saves/MyWorld)
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Display name of the world (from level.dat or folder name)
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The parent instance this world belongs to
    /// </summary>
    public required MinecraftInstance Instance { get; set; }

    /// <summary>
    /// Last played timestamp from level.dat (Unix timestamp in milliseconds)
    /// </summary>
    public long LastPlayed { get; set; }

    /// <summary>
    /// Whether this world is selected for backup
    /// </summary>
    public bool IsSelectedForBackup { get; set; }

    /// <summary>
    /// Last known backup timestamp (for change detection)
    /// </summary>
    public long LastBackupTimestamp { get; set; }

    /// <summary>
    /// Size of the world folder in bytes
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets a unique identifier for this world (Instance + World name)
    /// </summary>
    public string UniqueId => $"{Instance.Name}::{Name}";

    /// <summary>
    /// Returns true if the world has been played since the last backup
    /// </summary>
    public bool NeedsBackup => LastPlayed > LastBackupTimestamp;

    /// <summary>
    /// Human-readable size string
    /// </summary>
    public string SizeDisplay
    {
        get
        {
            if (SizeBytes < 1024) return $"{SizeBytes} B";
            if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
            if (SizeBytes < 1024 * 1024 * 1024) return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
            return $"{SizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    /// <summary>
    /// Human-readable last played time
    /// </summary>
    public DateTime LastPlayedDateTime => DateTimeOffset.FromUnixTimeMilliseconds(LastPlayed).LocalDateTime;
}
