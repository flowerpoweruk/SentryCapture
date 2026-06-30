using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using SentryCapture.Models;
using SentryCapture.Services;

namespace SentryCapture.ViewModels;

public class MainViewModel : ObservableObject
{
    private const string FeedbackEmail = "purplepenguin.apps@gmail.com";
    private const int MaxLogDisplayEntries = 3000;

    private readonly CaptureManager _manager;
    private readonly Logger _log = Logger.Instance;
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, CameraViewModel> _vmById = new();
    private readonly DispatcherTimer _uptimeTimer;

    public ObservableCollection<CameraViewModel> Cameras { get; } = new();
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public RelayCommand StartAllCommand { get; }
    public RelayCommand StopAllCommand { get; }
    public RelayCommand CopyLogCommand { get; }
    public RelayCommand ClearLogViewCommand { get; }
    public RelayCommand OpenDataFolderCommand { get; }
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand SendFeedbackCommand { get; }

    public MainViewModel(CaptureManager manager)
    {
        _manager = manager;
        _dispatcher = Application.Current.Dispatcher;

        StartAllCommand = new RelayCommand(_ => _manager.StartAll());
        StopAllCommand = new RelayCommand(_ => _manager.StopAll());
        CopyLogCommand = new RelayCommand(_ => CopyLog());
        ClearLogViewCommand = new RelayCommand(_ => LogEntries.Clear());
        OpenDataFolderCommand = new RelayCommand(_ => OpenFolder(AppPaths.DataRoot));
        OpenLogFolderCommand = new RelayCommand(_ => OpenFolder(AppPaths.LogDirectory));
        SendFeedbackCommand = new RelayCommand(_ => SendFeedback());

        BuildCameraList();

        _manager.WorkerStateChanged += OnWorkerStateChanged;
        _manager.CamerasChanged += () => _dispatcher.Invoke(BuildCameraList);
        _log.EntryLogged += OnEntryLogged;

        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uptimeTimer.Tick += (_, _) =>
        {
            foreach (var cam in Cameras) cam.RefreshUptime();
            OnPropertyChanged(nameof(StatusSummary));
        };
        _uptimeTimer.Start();
    }

    public bool CanAddCamera => _manager.CanAddCamera;

    public string StatusSummary
    {
        get
        {
            int total = Cameras.Count;
            int running = Cameras.Count(c => c.IsRunning);
            int healthy = Cameras.Count(c => c.Status == CameraStatus.Ok);
            int errored = Cameras.Count(c => c.Status == CameraStatus.Error);
            return $"{total} camera(s) · {running} running · {healthy} healthy · {errored} error";
        }
    }

    private void BuildCameraList()
    {
        Cameras.Clear();
        _vmById.Clear();
        foreach (var worker in _manager.Workers)
        {
            var vm = new CameraViewModel(worker);
            Cameras.Add(vm);
            _vmById[worker.Id] = vm;
        }
        OnPropertyChanged(nameof(CanAddCamera));
        OnPropertyChanged(nameof(StatusSummary));
    }

    private void OnWorkerStateChanged(CameraWorker worker)
    {
        _dispatcher.Invoke(() =>
        {
            if (_vmById.TryGetValue(worker.Id, out var vm))
                vm.Refresh();
            OnPropertyChanged(nameof(StatusSummary));
        });
    }

    private void OnEntryLogged(LogEntry entry)
    {
        _dispatcher.Invoke(() =>
        {
            LogEntries.Add(entry);
            // Trim from the front to bound memory; full history remains on disk.
            while (LogEntries.Count > MaxLogDisplayEntries)
                LogEntries.RemoveAt(0);
        });
    }

    // ---- Camera CRUD (called from the view after showing dialogs) ----

    public CameraViewModel? AddCamera(CameraConfig config)
    {
        var worker = _manager.AddCamera(config);
        return worker == null ? null : _vmById.GetValueOrDefault(worker.Id);
    }

    public void EditCamera(string id, CameraConfig config) => _manager.EditCamera(id, config);

    public void RemoveCamera(string id) => _manager.RemoveCamera(id);

    // ---- Actions ----

    private void CopyLog()
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var entry in LogEntries)
                sb.AppendLine(entry.ToString());

            if (sb.Length == 0)
                sb.AppendLine("(log is empty)");

            sb.AppendLine();
            sb.AppendLine($"--- Full history on disk: {_log.CurrentLogFilePath} ---");

            Clipboard.SetText(sb.ToString());
            _log.Info("Debug log copied to clipboard.");
        }
        catch (Exception ex)
        {
            _log.Error("Failed to copy log to clipboard.", ex);
        }
    }

    private void SendFeedback()
    {
        try
        {
            string subject = Uri.EscapeDataString("Sentry Capture feedback");
            string body = Uri.EscapeDataString(
                "Describe your feedback or issue here.\r\n\r\n" +
                "(Tip: you can paste relevant lines from the in-app debug log below.)\r\n");
            string mailto = $"mailto:{FeedbackEmail}?subject={subject}&body={body}";

            Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
            _log.Info("Opened default email client for feedback.");
        }
        catch (Exception ex)
        {
            _log.Error("Failed to open email client for feedback.", ex);
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            System.IO.Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to open folder: {path}", ex);
        }
    }
}
