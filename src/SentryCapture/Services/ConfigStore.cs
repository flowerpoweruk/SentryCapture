using System;
using System.IO;
using System.Text.Json;
using SentryCapture.Models;

namespace SentryCapture.Services;

/// <summary>
/// Loads and saves <see cref="AppConfig"/> to config.json. Saves are written atomically
/// (temp file + replace) so a crash mid-write cannot corrupt the config.
/// </summary>
public static class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly object Gate = new();

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(AppPaths.ConfigFile))
                return new AppConfig();

            string json = File.ReadAllText(AppPaths.ConfigFile);
            if (string.IsNullOrWhiteSpace(json))
                return new AppConfig();

            var config = JsonSerializer.Deserialize<AppConfig>(json, Options);
            return config ?? new AppConfig();
        }
        catch
        {
            // A malformed config should never prevent the app from starting.
            // Back it up and start fresh so the user keeps their (broken) file for inspection.
            TryBackupCorruptConfig();
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        lock (Gate)
        {
            string json = JsonSerializer.Serialize(config, Options);
            string tempFile = AppPaths.ConfigFile + ".tmp";
            File.WriteAllText(tempFile, json);

            if (File.Exists(AppPaths.ConfigFile))
                File.Replace(tempFile, AppPaths.ConfigFile, null);
            else
                File.Move(tempFile, AppPaths.ConfigFile);
        }
    }

    private static void TryBackupCorruptConfig()
    {
        try
        {
            if (File.Exists(AppPaths.ConfigFile))
            {
                string backup = AppPaths.ConfigFile + ".corrupt";
                File.Copy(AppPaths.ConfigFile, backup, overwrite: true);
            }
        }
        catch
        {
            // Best-effort only.
        }
    }
}
