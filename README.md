# Minecraft AT Launcher World Backup

A Windows desktop application for automatically backing up Minecraft singleplayer worlds from ATLauncher instances.

## Features

- 🔍 **Auto-Discovery** - Scans ATLauncher instances for Minecraft worlds
- ✅ **Opt-in Selection** - Choose which worlds to protect
- ⏰ **Scheduled Backups** - Automatic 10-minute polling (configurable)
- 📦 **ZIP Compression** - Efficient storage with progress tracking
- 🔄 **Retention Policy** - Keeps last N backups, auto-deletes older ones
- 📥 **System Tray** - Runs quietly in background
- 🎨 **Dark Theme** - Modern, clean UI

## Screenshots

*Coming soon*

## Requirements

- Windows 10/11
- .NET 8.0 Runtime
- ATLauncher (or similar launcher with standard folder structure)

## Installation

### From Release
1. Download the latest release
2. Extract to a folder
3. Run `MinecraftWorldBackup.exe`

### From Source
```bash
git clone https://github.com/M0uidev/MinecraftWorldBackup.git
cd MinecraftWorldBackup
dotnet run
```

## Configuration

Settings are stored in `%APPDATA%\MinecraftWorldBackup\config.json`:

| Setting | Default | Description |
|---------|---------|-------------|
| ATLauncherInstancesPath | `D:\Games\ATLauncher\instances` | Path to ATLauncher instances folder |
| BackupPath | `Documents\MinecraftBackups` | Where backups are stored |
| PollingIntervalMinutes | 10 | How often to check for changes |
| MaxBackupsPerWorld | 10 | Retention limit per world |
| StartMinimized | false | Launch to system tray |

## How It Works

1. Parses `level.dat` NBT files to read `LastPlayed` timestamp
2. Compares with last known timestamp to detect changes
3. Creates timestamped ZIP backup when changes detected
4. Applies retention policy to remove old backups

## Tech Stack

- **Framework**: .NET 8 / WPF
- **NBT Parsing**: fNbt library
- **Storage**: ZIP compression + JSON config

## License

MIT License - See [LICENSE](LICENSE) for details
