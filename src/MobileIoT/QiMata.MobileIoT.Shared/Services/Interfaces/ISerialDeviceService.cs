namespace QiMata.MobileIoT.Shared.Services;

/// <summary>Manages USB-serial peripheral connections, providing port enumeration, open/close lifecycle, and byte-level read/write.</summary>
public interface ISerialDeviceService : IAsyncDisposable
{
    /// <summary>Indicates whether a serial port is currently open.</summary>
    bool IsOpen { get; }

    /// <summary>Enumerate attached USB-serial peripherals.</summary>
    Task<IReadOnlyList<SerialDeviceInfo>> ListAsync(CancellationToken ct = default);

    /// <summary>Open the first port matching VID/PID at the supplied baud rate.</summary>
    Task<bool> OpenAsync(ushort vid, ushort pid, int baudRate = 9600, CancellationToken ct = default);

    /// <summary>Write a byte payload.</summary>
    Task<int> WriteAsync(byte[] data, CancellationToken ct = default);

    /// <summary>Raised when bytes arrive from the peripheral.</summary>
    event EventHandler<ReadOnlyMemory<byte>> DataReceived;
}
