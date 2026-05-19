namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>Provides basic NFC tag read/write capability and hardware availability information.</summary>
public interface INfcService
{
    /// <summary>Indicates whether NFC hardware is present on the device.</summary>
    bool IsAvailable { get; }

    /// <summary>Indicates whether NFC is currently enabled in device settings.</summary>
    bool IsEnabled { get; }

    /// <summary>Starts listening for incoming NFC tag events.</summary>
    Task StartListeningAsync();

    /// <summary>Stops listening for NFC tag events.</summary>
    Task StopListeningAsync();

    /// <summary>Writes a plain-text NDEF record to the next NFC tag that comes into range.</summary>
    Task WriteTextAsync(string text);

    /// <summary>Raised when an NFC tag is read and the message payload is extracted.</summary>
    event EventHandler<string> MessageReceived;
}
