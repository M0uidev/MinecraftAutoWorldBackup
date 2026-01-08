using System.IO;
using fNbt;
using MinecraftWorldBackup.Models;

namespace MinecraftWorldBackup.Services;

/// <summary>
/// Scans for Minecraft instances and worlds
/// </summary>
public class InstanceScanner
{
    private readonly AppConfiguration _config;

    public InstanceScanner(AppConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Discovers all instances in the ATLauncher directory
    /// </summary>
    public List<MinecraftInstance> ScanInstances()
    {
        var instances = new List<MinecraftInstance>();
        var instancesPath = _config.ATLauncherInstancesPath;

        if (!Directory.Exists(instancesPath))
        {
            System.Diagnostics.Debug.WriteLine($"ATLauncher instances path not found: {instancesPath}");
            return instances;
        }

        foreach (var instanceDir in Directory.GetDirectories(instancesPath))
        {
            var minecraftPath = Path.Combine(instanceDir, ".minecraft");
            
            // Some instances might have the saves directly in the instance folder
            if (!Directory.Exists(minecraftPath))
            {
                minecraftPath = instanceDir;
            }

            var savesPath = Path.Combine(minecraftPath, "saves");
            if (!Directory.Exists(savesPath))
            {
                continue;
            }

            var instance = new MinecraftInstance
            {
                Name = Path.GetFileName(instanceDir),
                MinecraftPath = minecraftPath
            };

            // Scan worlds in this instance
            instance.Worlds = ScanWorlds(instance);
            
            if (instance.Worlds.Count > 0)
            {
                instances.Add(instance);
            }
        }

        return instances;
    }

    /// <summary>
    /// Scans for worlds in an instance
    /// </summary>
    private List<MinecraftWorld> ScanWorlds(MinecraftInstance instance)
    {
        var worlds = new List<MinecraftWorld>();
        var savesPath = instance.SavesPath;

        if (!Directory.Exists(savesPath))
        {
            return worlds;
        }

        foreach (var worldDir in Directory.GetDirectories(savesPath))
        {
            var levelDatPath = Path.Combine(worldDir, "level.dat");
            if (!File.Exists(levelDatPath))
            {
                continue;
            }

            try
            {
                var world = ParseWorld(worldDir, instance);
                if (world != null)
                {
                    // Apply persisted selection state
                    world.IsSelectedForBackup = _config.IsWorldSelected(world.UniqueId);
                    world.LastBackupTimestamp = _config.GetLastKnownTimestamp(world.UniqueId);
                    worlds.Add(world);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse world {worldDir}: {ex.Message}");
            }
        }

        return worlds;
    }

    /// <summary>
    /// Parses a world directory and extracts metadata from level.dat
    /// </summary>
    private MinecraftWorld? ParseWorld(string worldPath, MinecraftInstance instance)
    {
        var levelDatPath = Path.Combine(worldPath, "level.dat");
        
        try
        {
            // Read and parse level.dat using fNbt
            var nbtFile = new NbtFile();
            nbtFile.LoadFromFile(levelDatPath);

            var rootTag = nbtFile.RootTag;
            var dataTag = rootTag.Get<NbtCompound>("Data");

            if (dataTag == null)
            {
                return null;
            }

            // Extract world name (LevelName) and last played timestamp
            var levelName = dataTag.Get<NbtString>("LevelName")?.Value ?? Path.GetFileName(worldPath);
            var lastPlayed = dataTag.Get<NbtLong>("LastPlayed")?.Value ?? 0;

            // Calculate world size
            var worldSize = CalculateDirectorySize(worldPath);

            return new MinecraftWorld
            {
                Path = worldPath,
                Name = levelName,
                Instance = instance,
                LastPlayed = lastPlayed,
                SizeBytes = worldSize
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing level.dat at {levelDatPath}: {ex.Message}");
            
            // Fallback: create world with folder name if level.dat is unreadable
            return new MinecraftWorld
            {
                Path = worldPath,
                Name = Path.GetFileName(worldPath),
                Instance = instance,
                LastPlayed = 0,
                SizeBytes = CalculateDirectorySize(worldPath)
            };
        }
    }

    /// <summary>
    /// Calculates the total size of a directory
    /// </summary>
    private static long CalculateDirectorySize(string path)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                         .Sum(file => file.Length);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Refreshes the LastPlayed timestamp for a specific world
    /// </summary>
    public void RefreshWorldTimestamp(MinecraftWorld world)
    {
        var levelDatPath = Path.Combine(world.Path, "level.dat");
        
        try
        {
            var nbtFile = new NbtFile();
            nbtFile.LoadFromFile(levelDatPath);
            
            var dataTag = nbtFile.RootTag.Get<NbtCompound>("Data");
            if (dataTag != null)
            {
                world.LastPlayed = dataTag.Get<NbtLong>("LastPlayed")?.Value ?? world.LastPlayed;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing timestamp for {world.Name}: {ex.Message}");
        }
    }
}
