using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SentryCapture.Models;

namespace SentryCapture.Services;

public enum CameraStatus
{
    /// <summary>Not polling (stopped).</summary>
    Stopped,
    /// <summary>Running but no result yet this session.</summary>
    Starting,
    /// <summary>Last poll succeeded.</summary>
    Ok,
    /// <summary>Last poll failed.</summary>
    Error
}

/// <summary>
/// Owns the independent polling loop for a single camera. Each worker runs its own
/// <see cref="PeriodicTimer"/>-driven async loop so one camera failing or being slow never
/// blocks another. Config (name/url/headers) can be swapped live without stopping the loop.
/// </summary>
public sealed class CameraWorker
{
    private readonly HttpClient _http;
    private readonly Logger _log = Logger.Instance;
    private readonly int _intervalSeconds;
    private readonly int _timeoutSeconds;

    private CameraConfig _config; // swapped atomically on live edit
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public string Id => _config.Id;
    public CameraConfig Config => _config;
    public CameraStatus Status { get; private set; } = CameraStatus.Stopped;
    public int ImageCount { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public string LastError { get; private set; } = "";

    /// <summary>Raised (possibly off the UI thread) whenever status/count/uptime state changes.</summary>
    public event Action<CameraWorker>? StateChanged;

    public CameraWorker(CameraConfig config, HttpClient http, int intervalSeconds, int timeoutSeconds)
    {
        _config = config;
        _http = http;
        _intervalSeconds = Math.Max(1, intervalSeconds);
        _timeoutSeconds = Math.Max(1, timeoutSeconds);
    }

    public bool IsRunning => _loopTask is { IsCompleted: false };

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        StartedAtUtc = DateTime.UtcNow;
        ImageCount = 0;
        Status = CameraStatus.Starting;
        LastError = "";
        _log.Info("Started polling.", _config.Name);
        Notify();

        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        if (_cts == null) return;
        try { _cts.Cancel(); } catch { /* ignore */ }
        Status = CameraStatus.Stopped;
        _log.Info("Stopped polling.", _config.Name);
        Notify();
    }

    /// <summary>
    /// Replaces this worker's configuration live. New name/url/headers take effect on the next
    /// poll cycle without disturbing other cameras. Image count and uptime are preserved.
    /// </summary>
    public void UpdateConfig(CameraConfig newConfig)
    {
        string oldName = _config.Name;
        _config = newConfig;
        if (oldName != newConfig.Name)
            _log.Info($"Camera reconfigured (was \"{oldName}\").", newConfig.Name);
        else
            _log.Info("Camera settings updated.", newConfig.Name);
        Notify();
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        // Poll once immediately, then every interval.
        await PollOnceAsync(token).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                await PollOnceAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task PollOnceAsync(CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        var cfg = _config; // capture current config for this cycle
        string url = cfg.Url;
        string name = cfg.Name;

        if (string.IsNullOrWhiteSpace(url))
        {
            Fail(name, "No URL configured for this camera.");
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Default browser-like User-Agent unless the camera overrides it.
            bool hasCustomUa = false;
            foreach (var header in cfg.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key)) continue;
                if (header.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                    hasCustomUa = true;
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            if (!hasCustomUa)
            {
                request.Headers.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) SentryCapture/1.0");
            }

            using var response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Fail(name, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} from {url}");
                return;
            }

            byte[] data = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token).ConfigureAwait(false);

            if (data.Length == 0)
            {
                Fail(name, $"Empty response body (0 bytes) from {url}");
                return;
            }

            string? contentType = response.Content.Headers.ContentType?.MediaType;
            if (!LooksLikeImage(data, contentType))
            {
                string ct = contentType ?? "unknown";
                Fail(name, $"Response is not a valid image (Content-Type: {ct}, {data.Length} bytes, " +
                           $"first bytes: {FirstBytesHex(data)}) from {url}");
                return;
            }

            var now = DateTime.Now;
            string path = ImageWriter.Write(name, data, now);

            ImageCount++;
            Status = CameraStatus.Ok;
            LastError = "";
            _log.Success($"Captured {data.Length:N0} bytes -> {path}", name);
            Notify();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // App is stopping; not an error.
        }
        catch (OperationCanceledException)
        {
            Fail(name, $"Request timed out after {_timeoutSeconds}s for {url}");
        }
        catch (HttpRequestException ex)
        {
            FailWithException(name, $"Network error fetching {url}", ex);
        }
        catch (Exception ex)
        {
            FailWithException(name, $"Unexpected error fetching {url}", ex);
        }
    }

    private void Fail(string name, string detail)
    {
        Status = CameraStatus.Error;
        LastError = detail;
        _log.Error(detail, name);
        Notify();
    }

    private void FailWithException(string name, string message, Exception ex)
    {
        Status = CameraStatus.Error;
        LastError = $"{message} | {ex.GetType().Name}: {ex.Message}";
        _log.Error(message, ex, name);
        Notify();
    }

    /// <summary>Heuristic: trust an image content-type, or fall back to common image magic bytes.</summary>
    private static bool LooksLikeImage(byte[] data, string? contentType)
    {
        if (!string.IsNullOrEmpty(contentType) &&
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (data.Length < 4) return false;

        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return true;
        // PNG: 89 50 4E 47
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return true;
        // GIF: 47 49 46 38
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38) return true;
        // BMP: 42 4D
        if (data[0] == 0x42 && data[1] == 0x4D) return true;

        return false;
    }

    private static string FirstBytesHex(byte[] data)
    {
        int n = Math.Min(8, data.Length);
        var parts = new string[n];
        for (int i = 0; i < n; i++) parts[i] = data[i].ToString("X2");
        return string.Join(" ", parts);
    }

    private void Notify() => StateChanged?.Invoke(this);
}
