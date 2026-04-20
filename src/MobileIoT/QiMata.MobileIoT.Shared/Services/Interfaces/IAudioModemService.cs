namespace QiMata.MobileIoT.Services;

public interface IAudioModemService
{
    /// <summary>Start listening for audio data.</summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Stop listening.</summary>
    Task StopAsync();

    /// <summary>Raised when a decoded message is received.</summary>
    event EventHandler<string> DataReceived;
}
