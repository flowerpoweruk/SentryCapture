using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace SentryCapture.Services;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}

public class LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string Camera { get; init; } = "";
    public string Message { get; init; } = "";

    /// <summary>Full single-line representation used for the in-app panel and the log file.</summary>
    public override string ToString()
    {
        string cam = string.IsNullOrEmpty(Camera) ? "-" : Camera;
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level.ToString().ToUpperInvariant()}] [{cam}] {Message}";
    }
}

/// <summary>
/// Central logger. Writes every entry to a daily-rolling file on disk (history survives
/// restarts/crashes) and raises <see cref="EntryLogged"/> so the UI can append in real time.
///
/// Failures intentionally capture the full error detail (exception type, message, HTTP status,
/// stack trace) because the user pastes this directly into an AI assistant to diagnose issues.
/// </summary>
public sealed class Logger
{
    public static Logger Instance { get; } = new Logger();

    private readonly object _fileGate = new();
    private readonly BlockingCollection<LogEntry> _writeQueue = new();

    /// <summary>Raised on a background thread whenever an entry is logged.</summary>
    public event Action<LogEntry>? EntryLogged;

    private Logger()
    {
        AppPaths.EnsureDirectories();
        var thread = new System.Threading.Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "SentryCapture.LogWriter"
        };
        thread.Start();
    }

    public void Info(string message, string camera = "") => Log(LogLevel.Info, message, camera);
    public void Success(string message, string camera = "") => Log(LogLevel.Success, message, camera);
    public void Warning(string message, string camera = "") => Log(LogLevel.Warning, message, camera);
    public void Error(string message, string camera = "") => Log(LogLevel.Error, message, camera);

    /// <summary>Logs an error with full exception detail (type, message, stack trace).</summary>
    public void Error(string message, Exception ex, string camera = "")
    {
        var sb = new StringBuilder();
        sb.Append(message);
        sb.Append(" | ").Append(ex.GetType().FullName).Append(": ").Append(ex.Message);

        var inner = ex.InnerException;
        int depth = 0;
        while (inner != null && depth < 5)
        {
            sb.Append(" | Inner: ").Append(inner.GetType().FullName).Append(": ").Append(inner.Message);
            inner = inner.InnerException;
            depth++;
        }

        if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            sb.Append(" | StackTrace: ").Append(ex.StackTrace.Replace(Environment.NewLine, " "));

        Log(LogLevel.Error, sb.ToString(), camera);
    }

    public void Log(LogLevel level, string message, string camera = "")
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Camera = camera,
            Message = message
        };

        try { _writeQueue.Add(entry); } catch { /* queue completed during shutdown */ }
        EntryLogged?.Invoke(entry);
    }

    private void ProcessQueue()
    {
        foreach (var entry in _writeQueue.GetConsumingEnumerable())
        {
            try
            {
                string file = Path.Combine(AppPaths.LogDirectory, $"sentry-capture_{entry.Timestamp:yyyy-MM-dd}.log");
                lock (_fileGate)
                {
                    File.AppendAllText(file, entry + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Never let logging failures crash the app.
            }
        }
    }

    public string CurrentLogFilePath =>
        Path.Combine(AppPaths.LogDirectory, $"sentry-capture_{DateTime.Now:yyyy-MM-dd}.log");
}
