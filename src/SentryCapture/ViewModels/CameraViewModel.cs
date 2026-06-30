using System;
using SentryCapture.Models;
using SentryCapture.Services;

namespace SentryCapture.ViewModels;

/// <summary>UI-facing wrapper around a single <see cref="CameraWorker"/>.</summary>
public class CameraViewModel : ObservableObject
{
    public CameraWorker Worker { get; }

    public CameraViewModel(CameraWorker worker)
    {
        Worker = worker;
        Refresh();
    }

    public string Id => Worker.Id;
    public string Name => Worker.Config.Name;
    public string Url => Worker.Config.Url;

    public CameraStatus Status => Worker.Status;

    public string StatusText => Worker.Status switch
    {
        CameraStatus.Ok => "Healthy",
        CameraStatus.Error => "Error",
        CameraStatus.Starting => "Starting…",
        _ => "Stopped"
    };

    /// <summary>Brush key resolved in XAML via a converter/trigger; exposed as a simple state string.</summary>
    public string StatusKey => Worker.Status switch
    {
        CameraStatus.Ok => "Ok",
        CameraStatus.Error => "Error",
        CameraStatus.Starting => "Starting",
        _ => "Stopped"
    };

    public int ImageCount => Worker.ImageCount;

    public bool IsRunning => Worker.IsRunning;

    public string LastError => Worker.LastError;

    public string Uptime
    {
        get
        {
            if (!Worker.IsRunning || Worker.StartedAtUtc is null)
                return "—";

            TimeSpan span = DateTime.UtcNow - Worker.StartedAtUtc.Value;
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;

            if (span.TotalDays >= 1)
                return $"{(int)span.TotalDays}d {span.Hours:D2}h {span.Minutes:D2}m";
            if (span.TotalHours >= 1)
                return $"{span.Hours:D2}h {span.Minutes:D2}m {span.Seconds:D2}s";
            return $"{span.Minutes:D2}m {span.Seconds:D2}s";
        }
    }

    /// <summary>Re-reads all derived state from the worker and notifies the UI.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Url));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusKey));
        OnPropertyChanged(nameof(ImageCount));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(LastError));
        OnPropertyChanged(nameof(Uptime));
    }

    /// <summary>Cheap per-second refresh for the uptime ticker.</summary>
    public void RefreshUptime() => OnPropertyChanged(nameof(Uptime));
}
