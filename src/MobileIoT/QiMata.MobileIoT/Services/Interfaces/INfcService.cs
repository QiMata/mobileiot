namespace QiMata.MobileIoT.Services.Interfaces;

public interface INfcService
{
    bool IsAvailable { get; }
    bool IsEnabled { get; }
    Task StartListeningAsync();
    Task StopListeningAsync();
    Task WriteTextAsync(string text);
    event EventHandler<string> MessageReceived;
}
