using System.Runtime.CompilerServices;

namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>Provides structured logging at debug, info, warn, and error severity levels.</summary>
public interface IAppLogger
{
    /// <summary>Logs a diagnostic message at debug severity.</summary>
    void Debug(string message, [CallerMemberName] string? caller = null);

    /// <summary>Logs an informational message.</summary>
    void Info(string message, [CallerMemberName] string? caller = null);

    /// <summary>Logs a warning, optionally attaching an exception.</summary>
    void Warn(string message, Exception? ex = null, [CallerMemberName] string? caller = null);

    /// <summary>Logs an error, optionally attaching an exception.</summary>
    void Error(string message, Exception? ex = null, [CallerMemberName] string? caller = null);
}

/// <summary>Extends <see cref="IAppLogger"/> with a bindable text property so log output can be displayed in the UI.</summary>
public interface IObservableLog : IAppLogger
{
    /// <summary>Accumulated log text suitable for display in a scrollable UI control.</summary>
    string Text { get; }

    /// <summary>Raised whenever the <see cref="Text"/> property changes.</summary>
    event EventHandler? TextChanged;

    /// <summary>Clears the accumulated log text.</summary>
    void Clear();
}
