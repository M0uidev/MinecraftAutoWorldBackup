using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.IO;
using MinecraftWorldBackup.Models;

namespace MinecraftWorldBackup.Services;

/// <summary>
/// Handles Google Drive authentication and file sync
/// </summary>
public class GoogleDriveService : IDisposable
{
    private static readonly string[] Scopes = { DriveService.Scope.DriveFile };
    private const string ApplicationName = "Minecraft World Backup";
    private const string CredentialsPath = "credentials.json";
    private const string TokenPath = "token";
    
    private DriveService? _driveService;
    private readonly AppConfiguration _config;
    private string? _backupFolderId;

    public bool IsAuthenticated => _driveService != null;
    public event EventHandler<string>? StatusChanged;

    public GoogleDriveService(AppConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Authenticates with Google Drive using OAuth2
    /// </summary>
    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            var credPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CredentialsPath);
            Logger.Info($"Looking for credentials at: {credPath}");
            
            if (!File.Exists(credPath))
            {
                Logger.Error("credentials.json not found");
                StatusChanged?.Invoke(this, "credentials.json not found. Please add it to the app folder.");
                return false;
            }

            Logger.Info("credentials.json found, starting OAuth flow...");
            using var stream = new FileStream(credPath, FileMode.Open, FileAccess.Read);
            
            var tokenPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MinecraftWorldBackup",
                TokenPath
            );
            Logger.Info($"Token path: {tokenPath}");

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(tokenPath, true)
            );

            Logger.Info("OAuth successful, creating DriveService...");
            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            });

            // Get or create the backup folder
            Logger.Info("Getting/creating MinecraftBackups folder...");
            _backupFolderId = await GetOrCreateFolderAsync("MinecraftBackups", null);
            
            Logger.Info($"Connected to Google Drive. Backup folder ID: {_backupFolderId}");
            StatusChanged?.Invoke(this, "Connected to Google Drive");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Google Drive auth failed", ex);
            StatusChanged?.Invoke(this, $"Auth failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Syncs a world folder to Google Drive
    /// </summary>
    public async Task<bool> SyncWorldAsync(MinecraftWorld world, IProgress<int>? progress = null)
    {
        if (_driveService == null || _backupFolderId == null)
        {
            return false;
        }

        try
        {
            StatusChanged?.Invoke(this, $"Syncing {world.Name} to Google Drive...");

            // Create folder structure: MinecraftBackups/InstanceName/WorldName
            var instanceFolderId = await GetOrCreateFolderAsync(
                SanitizeName(world.Instance.Name), 
                _backupFolderId
            );
            
            var worldFolderId = await GetOrCreateFolderAsync(
                SanitizeName(world.Name), 
                instanceFolderId
            );

            // Get all files in the world folder
            var allFiles = Directory.GetFiles(world.Path, "*", SearchOption.AllDirectories);
            var totalFiles = allFiles.Length;
            var processedFiles = 0;

            foreach (var localFile in allFiles)
            {
                var relativePath = Path.GetRelativePath(world.Path, localFile);
                
                try
                {
                    await UploadOrUpdateFileAsync(localFile, relativePath, worldFolderId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to upload {relativePath}: {ex.Message}");
                }

                processedFiles++;
                var percent = (int)((double)processedFiles / totalFiles * 100);
                progress?.Report(percent);
            }

            StatusChanged?.Invoke(this, $"Synced {world.Name} ({processedFiles} files)");
            return true;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Sync failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets or creates a folder in Google Drive
    /// </summary>
    private async Task<string> GetOrCreateFolderAsync(string folderName, string? parentId)
    {
        if (_driveService == null) throw new InvalidOperationException("Not authenticated");

        // Search for existing folder
        var query = $"name = '{folderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        if (parentId != null)
        {
            query += $" and '{parentId}' in parents";
        }

        var listRequest = _driveService.Files.List();
        listRequest.Q = query;
        listRequest.Fields = "files(id, name)";
        
        var result = await listRequest.ExecuteAsync();
        
        if (result.Files.Count > 0)
        {
            return result.Files[0].Id;
        }

        // Create new folder
        var folderMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder"
        };

        if (parentId != null)
        {
            folderMetadata.Parents = new List<string> { parentId };
        }

        var createRequest = _driveService.Files.Create(folderMetadata);
        createRequest.Fields = "id";
        
        var folder = await createRequest.ExecuteAsync();
        return folder.Id;
    }

    /// <summary>
    /// Uploads or updates a file in Google Drive
    /// </summary>
    private async Task UploadOrUpdateFileAsync(string localPath, string relativePath, string parentFolderId)
    {
        if (_driveService == null) throw new InvalidOperationException("Not authenticated");

        var fileName = Path.GetFileName(localPath);
        var folderPath = Path.GetDirectoryName(relativePath);
        
        // Navigate/create subdirectory structure
        var currentFolderId = parentFolderId;
        if (!string.IsNullOrEmpty(folderPath))
        {
            foreach (var part in folderPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (!string.IsNullOrEmpty(part))
                {
                    currentFolderId = await GetOrCreateFolderAsync(part, currentFolderId);
                }
            }
        }

        // Check if file already exists
        var query = $"name = '{fileName}' and '{currentFolderId}' in parents and trashed = false";
        var listRequest = _driveService.Files.List();
        listRequest.Q = query;
        listRequest.Fields = "files(id, name, modifiedTime)";
        
        var existing = await listRequest.ExecuteAsync();

        using var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        
        if (existing.Files.Count > 0)
        {
            // Update existing file
            var fileMetadata = new Google.Apis.Drive.v3.Data.File { Name = fileName };
            var updateRequest = _driveService.Files.Update(fileMetadata, existing.Files[0].Id, stream, GetMimeType(localPath));
            await updateRequest.UploadAsync();
        }
        else
        {
            // Create new file
            var fileMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName,
                Parents = new List<string> { currentFolderId }
            };
            
            var createRequest = _driveService.Files.Create(fileMetadata, stream, GetMimeType(localPath));
            await createRequest.UploadAsync();
        }
    }

    /// <summary>
    /// Disconnects from Google Drive
    /// </summary>
    public Task DisconnectAsync()
    {
        var tokenPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MinecraftWorldBackup",
            TokenPath
        );

        if (Directory.Exists(tokenPath))
        {
            Directory.Delete(tokenPath, true);
        }

        _driveService?.Dispose();
        _driveService = null;
        _backupFolderId = null;
        
        StatusChanged?.Invoke(this, "Disconnected from Google Drive");
        return Task.CompletedTask;
    }

    private static string SanitizeName(string name)
    {
        var invalids = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalids, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".dat" => "application/octet-stream",
            ".mca" => "application/octet-stream",
            ".json" => "application/json",
            ".png" => "image/png",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    public void Dispose()
    {
        _driveService?.Dispose();
    }
}
