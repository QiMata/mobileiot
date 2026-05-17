using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using System.Runtime.CompilerServices;

namespace QiMata.MobileIoT.Services;

public sealed class AppLogger : IObservableLog
{
    private const int MaxLines = 500;

    private readonly ILogger<AppLogger> _logger;
    private readonly LinkedList<string> _lines = new();
    private readonly object _gate = new();

    public AppLogger(ILogger<AppLogger> logger)
    {
        _logger = logger;
    }

    public string Text
    {
        get
        {
            lock (_gate)
            {
                return string.Join('\n', _lines);
            }
        }
    }

    public event EventHandler? TextChanged;

    public void Clear()
    {
        lock (_gate)
        {
            _lines.Clear();
        }
        RaiseChanged();
    }

    public void Debug(string message, [CallerMemberName] string? caller = null)
    {
        _logger.LogDebug("{Caller}: {Message}", caller, message);
        Append("DBG", caller, message, ex: null);
    }

    public void Info(string message, [CallerMemberName] string? caller = null)
    {
        _logger.LogInformation("{Caller}: {Message}", caller, message);
        Append("INF", caller, message, ex: null);
    }

    public void Warn(string message, Exception? ex = null, [CallerMemberName] string? caller = null)
    {
        _logger.LogWarning(ex, "{Caller}: {Message}", caller, message);
        Append("WRN", caller, message, ex);
    }

    public void Error(string message, Exception? ex = null, [CallerMemberName] string? caller = null)
    {
        _logger.LogError(ex, "{Caller}: {Message}", caller, message);
        Append("ERR", caller, message, ex);
    }

    private void Append(string level, string? caller, string message, Exception? ex)
    {
        var line = ex is null
            ? $"[{DateTime.Now:HH:mm:ss}] {level} {caller}: {message}"
            : $"[{DateTime.Now:HH:mm:ss}] {level} {caller}: {message} -- {ex.GetType().Name}: {ex.Message}";

        lock (_gate)
        {
            _lines.AddLast(line);
            while (_lines.Count > MaxLines)
            {
                _lines.RemoveFirst();
            }
        }
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        var handler = TextChanged;
        if (handler is null) return;

        if (MainThread.IsMainThread)
        {
            handler(this, EventArgs.Empty);
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() => handler(this, EventArgs.Empty));
        }
    }
}
