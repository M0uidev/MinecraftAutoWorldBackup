using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using MinecraftWorldBackup.Models;
using MinecraftWorldBackup.Services;
using MessageBox = System.Windows.MessageBox;
using Color = System.Windows.Media.Color;

namespace MinecraftWorldBackup;

/// <summary>
/// Settings configuration window
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppConfiguration _config;
    private GoogleDriveService? _driveService;
    private bool _isDriveConnected;

    public SettingsWindow(AppConfiguration config)
    {
        InitializeComponent();
        _config = config;
        LoadSettings();
    }

    private void LoadSettings()
    {
        InstancesPathTextBox.Text = _config.ATLauncherInstancesPath;
        BackupPathTextBox.Text = _config.BackupPath;
        IntervalTextBox.Text = _config.PollingIntervalMinutes.ToString();
        MaxBackupsTextBox.Text = _config.MaxBackupsPerWorld.ToString();
        StartMinimizedCheckBox.IsChecked = _config.StartMinimized;
        EnableGoogleDriveCheckBox.IsChecked = _config.EnableGoogleDrive;
        AlsoBackupLocallyCheckBox.IsChecked = _config.AlsoBackupLocally;
        
        // Check if credentials exist
        UpdateDriveConnectionStatus();
    }

    private void UpdateDriveConnectionStatus()
    {
        var credPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "credentials.json");
        var tokenPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MinecraftWorldBackup",
            "token"
        );
        
        if (!System.IO.File.Exists(credPath))
        {
            DriveStatusIcon.Text = "⚠️";
            DriveStatusText.Text = "credentials.json missing";
            DriveStatusText.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)); // Warning yellow
            ConnectDriveButton.Content = "Setup";
        }
        else if (System.IO.Directory.Exists(tokenPath))
        {
            DriveStatusIcon.Text = "●";
            DriveStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128)); // Green
            DriveStatusText.Text = "Connected";
            DriveStatusText.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128));
            ConnectDriveButton.Content = "Disconnect";
            _isDriveConnected = true;
        }
        else
        {
            DriveStatusIcon.Text = "○";
            DriveStatusText.Text = "Not connected";
            DriveStatusText.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)); // Gray
            ConnectDriveButton.Content = "Connect";
            _isDriveConnected = false;
        }
    }

    private async void ConnectDrive_Click(object sender, RoutedEventArgs e)
    {
        var credPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "credentials.json");
        
        if (!System.IO.File.Exists(credPath))
        {
            MessageBox.Show(
                "To use Google Drive sync:\n\n" +
                "1. Go to console.cloud.google.com\n" +
                "2. Create a project and enable 'Google Drive API'\n" +
                "3. Create OAuth 2.0 credentials (Desktop app)\n" +
                "4. Download and save as 'credentials.json'\n" +
                "5. Place in the app folder:\n" +
                $"   {AppDomain.CurrentDomain.BaseDirectory}",
                "Google Drive Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        if (_isDriveConnected)
        {
            // Disconnect
            var result = MessageBox.Show(
                "Are you sure you want to disconnect from Google Drive?",
                "Disconnect",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                _driveService?.Dispose();
                _driveService = new GoogleDriveService(_config);
                await _driveService.DisconnectAsync();
                UpdateDriveConnectionStatus();
            }
        }
        else
        {
            // Connect
            ConnectDriveButton.IsEnabled = false;
            ConnectDriveButton.Content = "Connecting...";

            _driveService = new GoogleDriveService(_config);
            var success = await _driveService.AuthenticateAsync();

            if (success)
            {
                MessageBox.Show("Successfully connected to Google Drive!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Failed to connect to Google Drive. Check the credentials.json file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateDriveConnectionStatus();
            ConnectDriveButton.IsEnabled = true;
        }
    }

    private void GoogleDriveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Just for visual feedback, actual save happens on Save button
    }

    private void BrowseInstances_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select ATLauncher Instances Folder",
            SelectedPath = InstancesPathTextBox.Text,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            InstancesPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void BrowseBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Backup Storage Folder",
            SelectedPath = BackupPathTextBox.Text,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            BackupPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Validate inputs
        if (!int.TryParse(IntervalTextBox.Text, out var interval) || interval < 1 || interval > 1440)
        {
            MessageBox.Show("Scan interval must be between 1 and 1440 minutes.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(MaxBackupsTextBox.Text, out var maxBackups) || maxBackups < 1 || maxBackups > 100)
        {
            MessageBox.Show("Maximum backups must be between 1 and 100.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(InstancesPathTextBox.Text))
        {
            MessageBox.Show("Please specify the ATLauncher instances path.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(BackupPathTextBox.Text))
        {
            MessageBox.Show("Please specify the backup storage path.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Save configuration
        _config.ATLauncherInstancesPath = InstancesPathTextBox.Text;
        _config.BackupPath = BackupPathTextBox.Text;
        _config.PollingIntervalMinutes = interval;
        _config.MaxBackupsPerWorld = maxBackups;
        _config.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;
        _config.EnableGoogleDrive = EnableGoogleDriveCheckBox.IsChecked ?? false;
        _config.AlsoBackupLocally = AlsoBackupLocallyCheckBox.IsChecked ?? true;
        _config.Save();

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
