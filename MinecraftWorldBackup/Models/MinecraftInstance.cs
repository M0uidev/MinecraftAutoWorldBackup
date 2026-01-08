using System.IO;

namespace MinecraftWorldBackup.Models;

/// <summary>
/// Represents a Minecraft instance (e.g., from ATLauncher)
/// </summary>
public class MinecraftInstance
{
    /// <summary>
    /// Name of the instance (folder name)
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Full path to the instance's .minecraft directory
    /// </summary>
    public required string MinecraftPath { get; set; }

    /// <summary>
    /// Full path to the saves folder
    /// </summary>
    public string SavesPath => System.IO.Path.Combine(MinecraftPath, "saves");

    /// <summary>
    /// List of worlds in this instance
    /// </summary>
    public List<MinecraftWorld> Worlds { get; set; } = [];

    /// <summary>
    /// Whether this instance folder exists and is valid
    /// </summary>
    public bool IsValid => Directory.Exists(SavesPath);
}
