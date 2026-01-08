using System.IO;
using System.IO.Compression;
using MinecraftWorldBackup.Models;

namespace MinecraftWorldBackup.Services;

/// <summary>
/// Handles backup creation and retention management
/// </summary>
public class BackupEngine
{
    private readonly AppConfiguration _config;

    public BackupEngine(AppConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Creates a backup of the specified world
    /// </summary>
    public async Task<BackupResult> CreateBackupAsync(MinecraftWorld world, IProgress<int>? progress = null)
    {
        try
        {
            // Ensure backup directory exists
            var worldBackupDir = GetWorldBackupDirectory(world);
            Directory.CreateDirectory(worldBackupDir);

            // Generate backup filename with timestamp
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupFileName = $"{SanitizeFileName(world.Name)}_{timestamp}.zip";
            var backupPath = Path.Combine(worldBackupDir, backupFileName);

            // Create the ZIP backup
            await Task.Run(() =>
            {
                CreateZipBackup(world.Path, backupPath, progress);
            });

            // Get backup size
            var backupSize = new FileInfo(backupPath).Length;

            // Update the last known timestamp
            _config.UpdateLastKnownTimestamp(world.UniqueId, world.LastPlayed);
            world.LastBackupTimestamp = world.LastPlayed;

            // Apply retention policy
            ApplyRetentionPolicy(world);

            return new BackupResult
            {
                Success = true,
                BackupPath = backupPath,
                BackupSizeBytes = backupSize,
                World = world
            };
        }
        catch (Exception ex)
        {
            return new BackupResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                World = world
            };
        }
    }

    /// <summary>
    /// Creates a ZIP archive of the world folder
    /// </summary>
    private void CreateZipBackup(string sourceDir, string destinationPath, IProgress<int>? progress)
    {
        // Delete existing partial file if it exists
        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        // Count files for progress reporting
        var allFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        var totalFiles = allFiles.Length;
        var processedFiles = 0;
        var skippedFiles = new List<string>();

        // Use explicit FileStream for better control
        using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var zipArchive = new System.IO.Compression.ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false))
        {
            foreach (var file in allFiles)
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                
                try
                {
                    // Read file with sharing enabled (in case Minecraft has it open)
                    using var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var entry = zipArchive.CreateEntry(relativePath, CompressionLevel.Optimal);
                    
                    using var entryStream = entry.Open();
                    sourceStream.CopyTo(entryStream);
                }
                catch (IOException ex)
                {
                    // File might be locked by Minecraft - skip it but log
                    skippedFiles.Add(relativePath);
                    System.Diagnostics.Debug.WriteLine($"Skipping locked file: {relativePath} - {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    skippedFiles.Add(relativePath);
                    System.Diagnostics.Debug.WriteLine($"Access denied: {relativePath} - {ex.Message}");
                }

                processedFiles++;
                var percent = (int)((double)processedFiles / totalFiles * 100);
                progress?.Report(percent);
            }
        } // ZIP is finalized here when disposed

        if (skippedFiles.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"Backup completed with {skippedFiles.Count} skipped files");
        }
    }

    /// <summary>
    /// Applies retention policy - keeps only the configured number of backups
    /// </summary>
    public void ApplyRetentionPolicy(MinecraftWorld world)
    {
        var worldBackupDir = GetWorldBackupDirectory(world);
        
        if (!Directory.Exists(worldBackupDir))
        {
            return;
        }

        var backups = Directory.GetFiles(worldBackupDir, "*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        // Keep only the configured number of backups
        var toDelete = backups.Skip(_config.MaxBackupsPerWorld);

        foreach (var backup in toDelete)
        {
            try
            {
                backup.Delete();
                System.Diagnostics.Debug.WriteLine($"Deleted old backup: {backup.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete backup {backup.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets the backup directory path for a world
    /// </summary>
    public string GetWorldBackupDirectory(MinecraftWorld world)
    {
        return Path.Combine(
            _config.BackupPath,
            "backups",
            SanitizeFileName(world.Instance.Name),
            SanitizeFileName(world.Name)
        );
    }

    /// <summary>
    /// Gets existing backups for a world
    /// </summary>
    public List<BackupInfo> GetBackupsForWorld(MinecraftWorld world)
    {
        var worldBackupDir = GetWorldBackupDirectory(world);
        
        if (!Directory.Exists(worldBackupDir))
        {
            return [];
        }

        return Directory.GetFiles(worldBackupDir, "*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .Select(f => new BackupInfo
            {
                Path = f.FullName,
                FileName = f.Name,
                CreatedAt = f.CreationTime,
                SizeBytes = f.Length
            })
            .ToList();
    }

    /// <summary>
    /// Sanitizes a string for use as a filename
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalids = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalids, StringSplitOptions.RemoveEmptyEntries));
    }
}

/// <summary>
/// Result of a backup operation
/// </summary>
public class BackupResult
{
    public bool Success { get; set; }
    public string? BackupPath { get; set; }
    public long BackupSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public required MinecraftWorld World { get; set; }
}

/// <summary>
/// Information about an existing backup
/// </summary>
public class BackupInfo
{
    public required string Path { get; set; }
    public required string FileName { get; set; }
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }

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
}
