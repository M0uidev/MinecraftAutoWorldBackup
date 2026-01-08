using System.Windows;
using Microsoft.Win32;
using MinecraftWorldBackup.Models;
using MessageBox = System.Windows.MessageBox;

namespace MinecraftWorldBackup;

/// <summary>
/// Settings configuration window
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppConfiguration _config;

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
