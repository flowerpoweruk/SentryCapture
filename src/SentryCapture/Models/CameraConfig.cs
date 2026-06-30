using System.Collections.Generic;

namespace SentryCapture.Models;

/// <summary>
/// Persisted configuration for a single camera feed.
/// </summary>
public class CameraConfig
{
    /// <summary>Stable unique id so renames don't lose identity/history.</summary>
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N");

    /// <summary>User-defined display name, e.g. "M6 J19 Northbound". Used to build folder/file names.</summary>
    public string Name { get; set; } = "";

    /// <summary>Direct JPEG URL to poll every 30 seconds.</summary>
    public string Url { get; set; } = "";

    /// <summary>
    /// Optional custom request headers (e.g. Referer, User-Agent) for feeds that reject plain requests.
    /// A sensible default User-Agent is always sent unless overridden here.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    public CameraConfig Clone()
    {
        return new CameraConfig
        {
            Id = Id,
            Name = Name,
            Url = Url,
            Headers = new Dictionary<string, string>(Headers)
        };
    }
}
