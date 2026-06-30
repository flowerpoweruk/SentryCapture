using System.Collections.Generic;

namespace SentryCapture.Models;

/// <summary>
/// Root persisted application configuration (stored as config.json next to the exe).
/// </summary>
public class AppConfig
{
    /// <summary>Maximum number of cameras the app supports.</summary>
    public const int MaxCameras = 10;

    /// <summary>Polling interval in seconds for every camera.</summary>
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>HTTP request timeout in seconds for a single image download.</summary>
    public int RequestTimeoutSeconds { get; set; } = 20;

    /// <summary>Configured cameras (0..MaxCameras).</summary>
    public List<CameraConfig> Cameras { get; set; } = new();
}
