using System.Runtime.CompilerServices;

namespace QiMata.MobileIoT.Services.Interfaces;

public interface IAppLogger
{
    void Debug(string message, [CallerMemberName] string? caller = null);
    void Info(string message, [CallerMemberName] string? caller = null);
    void Warn(string message, Exception? ex = null, [CallerMemberName] string? caller = null);
    void Error(string message, Exception? ex = null, [CallerMemberName] string? caller = null);
}

public interface IObservableLog : IAppLogger
{
    string Text { get; }
    event EventHandler? TextChanged;
    void Clear();
}
