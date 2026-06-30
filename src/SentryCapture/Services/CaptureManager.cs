using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using SentryCapture.Models;

namespace SentryCapture.Services;

/// <summary>
/// Owns all <see cref="CameraWorker"/> instances and the shared <see cref="HttpClient"/>.
/// Coordinates global Start All / Stop All and live add/edit/remove of cameras, persisting
/// every configuration change to disk.
/// </summary>
public sealed class CaptureManager : IDisposable
{
    private readonly HttpClient _http;
    private readonly Logger _log = Logger.Instance;
    private readonly List<CameraWorker> _workers = new();
    private readonly object _gate = new();

    private AppConfig _config;

    /// <summary>Raised when a worker's state changes (status/count/uptime).</summary>
    public event Action<CameraWorker>? WorkerStateChanged;

    /// <summary>Raised when the set of cameras changes (add/remove) so the UI can rebuild its list.</summary>
    public event Action? CamerasChanged;

    public CaptureManager(AppConfig config)
    {
        _config = config;

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };
        _http = new HttpClient(handler);
        // Per-request timeouts are enforced in CameraWorker; keep the client timeout generous.
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(_config.RequestTimeoutSeconds * 2, 60));

        foreach (var cam in _config.Cameras)
            _workers.Add(CreateWorker(cam));
    }

    public IReadOnlyList<CameraWorker> Workers
    {
        get { lock (_gate) return _workers.ToList(); }
    }

    public int CameraCount
    {
        get { lock (_gate) return _workers.Count; }
    }

    public bool CanAddCamera => CameraCount < AppConfig.MaxCameras;

    private CameraWorker CreateWorker(CameraConfig cam)
    {
        var worker = new CameraWorker(cam, _http, _config.PollIntervalSeconds, _config.RequestTimeoutSeconds);
        worker.StateChanged += w => WorkerStateChanged?.Invoke(w);
        return worker;
    }

    public void StartAll()
    {
        List<CameraWorker> snapshot;
        lock (_gate) snapshot = _workers.ToList();

        if (snapshot.Count == 0)
        {
            _log.Warning("Start All requested but no cameras are configured.");
            return;
        }

        _log.Info($"Start All ({snapshot.Count} camera(s)).");
        foreach (var w in snapshot) w.Start();
    }

    public void StopAll()
    {
        List<CameraWorker> snapshot;
        lock (_gate) snapshot = _workers.ToList();

        _log.Info("Stop All.");
        foreach (var w in snapshot) w.Stop();
    }

    /// <summary>Adds a new camera. Returns the created worker, or null if at the max.</summary>
    public CameraWorker? AddCamera(CameraConfig cam)
    {
        CameraWorker worker;
        lock (_gate)
        {
            if (_workers.Count >= AppConfig.MaxCameras)
            {
                _log.Warning($"Cannot add camera \"{cam.Name}\": maximum of {AppConfig.MaxCameras} reached.");
                return null;
            }
            worker = CreateWorker(cam);
            _workers.Add(worker);
            _config.Cameras.Add(cam);
        }

        Persist();
        _log.Info($"Camera added: \"{cam.Name}\" ({cam.Url}).", cam.Name);
        CamerasChanged?.Invoke();
        return worker;
    }

    /// <summary>Edits an existing camera live (other cameras are unaffected).</summary>
    public void EditCamera(string id, CameraConfig updated)
    {
        CameraWorker? worker;
        lock (_gate)
        {
            worker = _workers.FirstOrDefault(w => w.Id == id);
            if (worker == null) return;

            updated.Id = id; // preserve identity
            worker.UpdateConfig(updated);

            int idx = _config.Cameras.FindIndex(c => c.Id == id);
            if (idx >= 0) _config.Cameras[idx] = updated;
        }

        Persist();
        CamerasChanged?.Invoke();
    }

    /// <summary>Removes a camera live, stopping its polling first.</summary>
    public void RemoveCamera(string id)
    {
        CameraWorker? worker;
        lock (_gate)
        {
            worker = _workers.FirstOrDefault(w => w.Id == id);
            if (worker == null) return;

            worker.Stop();
            _workers.Remove(worker);
            _config.Cameras.RemoveAll(c => c.Id == id);
        }

        Persist();
        _log.Info($"Camera removed: \"{worker.Config.Name}\".", worker.Config.Name);
        CamerasChanged?.Invoke();
    }

    public bool AnyRunning
    {
        get { lock (_gate) return _workers.Any(w => w.IsRunning); }
    }

    private void Persist()
    {
        try
        {
            AppConfig snapshot;
            lock (_gate)
            {
                snapshot = new AppConfig
                {
                    PollIntervalSeconds = _config.PollIntervalSeconds,
                    RequestTimeoutSeconds = _config.RequestTimeoutSeconds,
                    Cameras = _config.Cameras.Select(c => c.Clone()).ToList()
                };
            }
            ConfigStore.Save(snapshot);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to save configuration.", ex);
        }
    }

    public void Dispose()
    {
        StopAll();
        _http.Dispose();
    }
}
