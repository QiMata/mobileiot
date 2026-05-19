namespace QiMata.MobileIoT.Shared.Services;

/// <summary>Records microphone audio and raises decoded text messages as they are recognized from the audio stream.</summary>
public interface IAudioModemService
{
    /// <summary>Requests microphone permission and starts recording audio for decoding.</summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Stops recording and finalises decoding of any buffered audio.</summary>
    Task StopAsync();

    /// <summary>Raised when a decoded text message is successfully extracted from the audio stream.</summary>
    event EventHandler<string> DataReceived;
}
