using System;
using System.IO;

namespace SentryCapture.Services;

/// <summary>
/// Central resolver for all on-disk locations. Everything is kept *next to the executable*
/// so the app is fully portable (per the chosen "next to the .exe" data layout).
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// Folder containing the running executable. When running a single-file published exe,
    /// AppContext.BaseDirectory points at the exe's directory (the launch folder), which is
    /// what we want for portability.
    /// </summary>
    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string ConfigFile => Path.Combine(BaseDirectory, "config.json");

    /// <summary>Root folder for all captured images.</summary>
    public static string DataRoot => Path.Combine(BaseDirectory, "SentryCapture_Data");

    /// <summary>Folder for persisted log files.</summary>
    public static string LogDirectory => Path.Combine(BaseDirectory, "logs");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(LogDirectory);
    }
}
