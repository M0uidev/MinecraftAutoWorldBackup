using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MinecraftWorldBackup.Models;
using MinecraftWorldBackup.Services;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;

namespace MinecraftWorldBackup;

/// <summary>
/// Main window for the Minecraft World Backup application
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppConfiguration _config;
    private readonly InstanceScanner _scanner;
    private readonly BackupEngine _backupEngine;
    private readonly BackupScheduler _scheduler;
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly System.Timers.Timer _uiUpdateTimer;
    private DateTime _nextScanTime;
    private List<MinecraftInstance> _instances = [];

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize services
        _config = AppConfiguration.Load();
        _scanner = new InstanceScanner(_config);
        _backupEngine = new BackupEngine(_config);
        _scheduler = new BackupScheduler(_config, _scanner, _backupEngine);

        // Setup event handlers
        _scheduler.BackupStarted += Scheduler_BackupStarted;
        _scheduler.BackupCompleted += Scheduler_BackupCompleted;
        _scheduler.ScanCompleted += Scheduler_ScanCompleted;

        // Setup system tray icon
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Shield,
            Text = "Minecraft World Backup",
            Visible = false
        };
        _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
        
        // Add context menu to tray icon
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("Open", null, (s, e) => ShowFromTray());
        contextMenu.Items.Add("Backup Now", null, async (s, e) => await BackupAllSelectedAsync());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());
        _notifyIcon.ContextMenuStrip = contextMenu;

        // Setup UI update timer for countdown
        _uiUpdateTimer = new System.Timers.Timer(1000);
        _uiUpdateTimer.Elapsed += (s, e) => UpdateCountdownDisplay();
        _uiUpdateTimer.Start();

        // Initial scan
        RefreshWorlds();
        
        // Start scheduler
        _scheduler.Start();
        _nextScanTime = DateTime.Now.AddMinutes(_config.PollingIntervalMinutes);

        // Check if should start minimized
        if (_config.StartMinimized)
        {
            WindowState = WindowState.Minimized;
            Hide();
            _notifyIcon.Visible = true;
        }
    }

    private void RefreshWorlds()
    {
        _instances = _scanner.ScanInstances();
        WorldsTreeView.ItemsSource = _instances;

        // Expand all instances
        foreach (var item in WorldsTreeView.Items)
        {
            var treeViewItem = WorldsTreeView.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (treeViewItem != null)
            {
                treeViewItem.IsExpanded = true;
            }
        }

        UpdateSelectionCount();
        UpdateStatus();
    }

    private void UpdateSelectionCount()
    {
        var selectedCount = _instances.SelectMany(i => i.Worlds).Count(w => w.IsSelectedForBackup);
        SelectedCountText.Text = $"{selectedCount} world{(selectedCount != 1 ? "s" : "")} selected for backup";
    }

    private void UpdateStatus()
    {
        var totalWorlds = _instances.Sum(i => i.Worlds.Count);
        StatusText.Text = totalWorlds > 0 
            ? $"Found {totalWorlds} worlds across {_instances.Count} instances" 
            : "No worlds found. Check your ATLauncher path in settings.";
    }

    private void UpdateCountdownDisplay()
    {
        var remaining = _nextScanTime - DateTime.Now;
        if (remaining.TotalSeconds < 0)
        {
            _nextScanTime = DateTime.Now.AddMinutes(_config.PollingIntervalMinutes);
            remaining = _nextScanTime - DateTime.Now;
        }

        Dispatcher.Invoke(() =>
        {
            NextScanText.Text = $"Next scan in: {remaining.Minutes:D2}:{remaining.Seconds:D2}";
        });
    }

    private void WorldCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is MinecraftWorld world)
        {
            _config.ToggleWorldSelection(world.UniqueId);
            UpdateSelectionCount();
        }
    }

    private async void BackupWorld_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MinecraftWorld world)
        {
            button.IsEnabled = false;
            button.Content = "...";

            // Show progress bar
            ProgressPanel.Visibility = Visibility.Visible;
            ProgressWorldName.Text = $"Backing up: {world.Name}";
            BackupProgressBar.Value = 0;
            ProgressPercent.Text = "0%";

            // Create progress reporter
            var progress = new Progress<int>(percent =>
            {
                BackupProgressBar.Value = percent;
                ProgressPercent.Text = $"{percent}%";
            });

            _scanner.RefreshWorldTimestamp(world);
            var result = await _backupEngine.CreateBackupAsync(world, progress);

            // Update last known timestamp if successful
            if (result.Success)
            {
                _config.UpdateLastKnownTimestamp(world.UniqueId, world.LastPlayed);
            }

            // Hide progress bar
            ProgressPanel.Visibility = Visibility.Collapsed;

            button.Content = result.Success ? "✓" : "✗";
            await Task.Delay(1500);
            button.Content = "Backup";
            button.IsEnabled = true;

            if (result.Success)
            {
                ShowNotification("Backup Complete", $"Backed up {world.Name}");
            }
            else
            {
                MessageBox.Show($"Backup failed: {result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BackupAll_Click(object sender, RoutedEventArgs e)
    {
        await BackupAllSelectedAsync();
    }

    private async Task BackupAllSelectedAsync()
    {
        var selectedWorlds = _instances.SelectMany(i => i.Worlds).Where(w => w.IsSelectedForBackup).ToList();
        
        if (selectedWorlds.Count == 0)
        {
            MessageBox.Show("No worlds selected for backup.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        BackupAllButton.IsEnabled = false;
        BackupAllButton.Content = "Backing up...";
        
        // Show progress panel
        ProgressPanel.Visibility = Visibility.Visible;

        var successCount = 0;
        for (int i = 0; i < selectedWorlds.Count; i++)
        {
            var world = selectedWorlds[i];
            
            // Update progress bar for current world
            ProgressWorldName.Text = $"Backing up ({i + 1}/{selectedWorlds.Count}): {world.Name}";
            BackupProgressBar.Value = 0;
            ProgressPercent.Text = "0%";

            // Create progress reporter
            var progress = new Progress<int>(percent =>
            {
                BackupProgressBar.Value = percent;
                ProgressPercent.Text = $"{percent}%";
            });

            _scanner.RefreshWorldTimestamp(world);
            var result = await _backupEngine.CreateBackupAsync(world, progress);
            
            if (result.Success)
            {
                _config.UpdateLastKnownTimestamp(world.UniqueId, world.LastPlayed);
                successCount++;
            }
        }

        // Hide progress panel
        ProgressPanel.Visibility = Visibility.Collapsed;

        BackupAllButton.Content = $"✓ {successCount}/{selectedWorlds.Count}";
        await Task.Delay(2000);
        BackupAllButton.Content = "🛡️ Backup All Selected";
        BackupAllButton.IsEnabled = true;

        ShowNotification("Backup Complete", $"Backed up {successCount} of {selectedWorlds.Count} worlds");
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshWorlds();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_config);
        settingsWindow.Owner = this;
        if (settingsWindow.ShowDialog() == true)
        {
            _scheduler.UpdateTimerInterval();
            RefreshWorlds();
        }
    }

    private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
    {
        MinimizeToTray();
    }

    private void MinimizeToTray()
    {
        Hide();
        _notifyIcon.Visible = true;
        ShowNotification("Minecraft Backup", "Running in background. Double-click to restore.");
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        _notifyIcon.Visible = false;
        Activate();
    }

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
    {
        ShowFromTray();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            MinimizeToTray();
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // Ask user if they want to exit or minimize
        var result = MessageBox.Show(
            "Do you want to close the application?\n\nClick 'No' to minimize to tray instead.",
            "Close Application",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == MessageBoxResult.No)
        {
            e.Cancel = true;
            MinimizeToTray();
            return;
        }

        ExitApplication();
    }

    private void ExitApplication()
    {
        _scheduler.Stop();
        _scheduler.Dispose();
        _uiUpdateTimer.Stop();
        _uiUpdateTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Current.Shutdown();
    }

    private void Scheduler_BackupStarted(object? sender, BackupEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = $"Backing up {e.World.Name}...";
        });
    }

    private void Scheduler_BackupCompleted(object? sender, BackupEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.Result?.Success == true)
            {
                StatusText.Text = $"Backed up {e.World.Name}";
                if (!IsVisible)
                {
                    ShowNotification("Backup Complete", $"Backed up {e.World.Name}");
                }
            }
            else
            {
                StatusText.Text = $"Failed to backup {e.World.Name}";
            }
        });
    }

    private void Scheduler_ScanCompleted(object? sender, ScanCompletedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshWorlds();
            _nextScanTime = DateTime.Now.AddMinutes(_config.PollingIntervalMinutes);
        });
    }

    private void ShowNotification(string title, string message)
    {
        if (_notifyIcon.Visible)
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, System.Windows.Forms.ToolTipIcon.Info);
        }
    }
}

/// <summary>
/// Converts boolean to Visibility
/// </summary>
public class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}