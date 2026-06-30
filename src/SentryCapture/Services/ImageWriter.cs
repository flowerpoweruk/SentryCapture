using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SentryCapture.Services;

/// <summary>
/// Builds the per-camera / per-day folder hierarchy and timestamped file names,
/// and writes downloaded image bytes to disk.
///
/// Layout:
///   SentryCapture_Data/&lt;SanitisedCameraName&gt;/&lt;yyyy-MM-dd&gt;/&lt;Name&gt;_&lt;yyyy-MM-dd_HH-mm-ss&gt;.jpg
/// </summary>
public static class ImageWriter
{
    /// <summary>
    /// Makes a string safe to use as a Windows file/folder name. Invalid characters and
    /// spaces become underscores; the result is trimmed and collapsed so it stays readable.
    /// </summary>
    public static string Sanitise(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Camera";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);

        foreach (char c in name.Trim())
        {
            if (char.IsWhiteSpace(c) || invalid.Contains(c))
                sb.Append('_');
            else
                sb.Append(c);
        }

        // Collapse runs of underscores and trim leading/trailing ones.
        string result = sb.ToString();
        while (result.Contains("__"))
            result = result.Replace("__", "_");
        result = result.Trim('_', '.');

        // Avoid reserved Windows device names.
        var reserved = new[] { "CON", "PRN", "AUX", "NUL",
            "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
            "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9" };
        if (reserved.Contains(result.ToUpperInvariant()))
            result = "_" + result;

        return string.IsNullOrEmpty(result) ? "Camera" : result;
    }

    /// <summary>
    /// Ensures the camera/day folder exists for <paramref name="now"/> and returns the full
    /// path the image should be written to. Does not write any bytes.
    /// </summary>
    public static string BuildImagePath(string cameraName, DateTime now)
    {
        string safeName = Sanitise(cameraName);
        string day = now.ToString("yyyy-MM-dd");
        string time = now.ToString("yyyy-MM-dd_HH-mm-ss");

        string dayFolder = Path.Combine(AppPaths.DataRoot, safeName, day);
        Directory.CreateDirectory(dayFolder);

        string fileName = $"{safeName}_{time}.jpg";
        return Path.Combine(dayFolder, fileName);
    }

    /// <summary>
    /// Writes image bytes for a camera at the given time, returning the path written.
    /// If a file with the same second-precision name already exists (two captures within the
    /// same second), a millisecond suffix is appended to keep both.
    /// </summary>
    public static string Write(string cameraName, byte[] data, DateTime now)
    {
        string path = BuildImagePath(cameraName, now);

        if (File.Exists(path))
        {
            string dir = Path.GetDirectoryName(path)!;
            string baseName = Path.GetFileNameWithoutExtension(path);
            path = Path.Combine(dir, $"{baseName}_{now:fff}.jpg");
        }

        File.WriteAllBytes(path, data);
        return path;
    }
}
