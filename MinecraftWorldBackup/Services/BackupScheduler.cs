using MinecraftWorldBackup.Models;

namespace MinecraftWorldBackup.Services;

/// <summary>
/// Manages the backup scheduling and change detection
/// </summary>
public class BackupScheduler : IDisposable
{
    private readonly AppConfiguration _config;
    private readonly InstanceScanner _scanner;
    private readonly BackupEngine _backupEngine;
    private readonly System.Timers.Timer _timer;
    private bool _isRunning;
    private bool _disposed;

    public event EventHandler<BackupEventArgs>? BackupStarted;
    public event EventHandler<BackupEventArgs>? BackupCompleted;
    public event EventHandler<ScanCompletedEventArgs>? ScanCompleted;

    public BackupScheduler(AppConfiguration config, InstanceScanner scanner, BackupEngine backupEngine)
    {
        _config = config;
        _scanner = scanner;
        _backupEngine = backupEngine;
        
        _timer = new System.Timers.Timer();
        _timer.Elapsed += async (s, e) => await PerformScheduledScanAsync();
        UpdateTimerInterval();
    }

    /// <summary>
    /// Starts the scheduler
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;
        
        _isRunning = true;
        _timer.Start();
        System.Diagnostics.Debug.WriteLine($"Scheduler started with {_config.PollingIntervalMinutes} minute interval");
    }

    /// <summary>
    /// Stops the scheduler
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _timer.Stop();
        System.Diagnostics.Debug.WriteLine("Scheduler stopped");
    }

    /// <summary>
    /// Updates the timer interval from configuration
    /// </summary>
    public void UpdateTimerInterval()
    {
        _timer.Interval = _config.PollingIntervalMinutes * 60 * 1000; // Convert to milliseconds
    }

    /// <summary>
    /// Performs a scheduled scan and backup of changed worlds
    /// </summary>
    public async Task PerformScheduledScanAsync()
    {
        System.Diagnostics.Debug.WriteLine($"Performing scheduled scan at {DateTime.Now}");
        
        var instances = _scanner.ScanInstances();
        var allWorlds = instances.SelectMany(i => i.Worlds).ToList();
        var selectedWorlds = allWorlds.Where(w => w.IsSelectedForBackup).ToList();
        
        ScanCompleted?.Invoke(this, new ScanCompletedEventArgs
        {
            TotalWorlds = allWorlds.Count,
            SelectedWorlds = selectedWorlds.Count
        });

        foreach (var world in selectedWorlds)
        {
            // Refresh the timestamp from level.dat
            _scanner.RefreshWorldTimestamp(world);
            
            // Check if the world needs backup
            if (world.NeedsBackup)
            {
                await BackupWorldAsync(world);
            }
        }
    }

    /// <summary>
    /// Manually triggers a backup for a specific world
    /// </summary>
    public async Task<BackupResult> BackupWorldAsync(MinecraftWorld world)
    {
        BackupStarted?.Invoke(this, new BackupEventArgs { World = world });
        
        var result = await _backupEngine.CreateBackupAsync(world);
        
        BackupCompleted?.Invoke(this, new BackupEventArgs
        {
            World = world,
            Result = result
        });

        return result;
    }

    /// <summary>
    /// Manually triggers backup for all selected worlds
    /// </summary>
    public async Task BackupAllSelectedAsync(IEnumerable<MinecraftWorld> worlds)
    {
        foreach (var world in worlds.Where(w => w.IsSelectedForBackup))
        {
            await BackupWorldAsync(world);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _timer.Stop();
        _timer.Dispose();
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }
}

public class BackupEventArgs : EventArgs
{
    public required MinecraftWorld World { get; set; }
    public BackupResult? Result { get; set; }
}

public class ScanCompletedEventArgs : EventArgs
{
    public int TotalWorlds { get; set; }
    public int SelectedWorlds { get; set; }
}
