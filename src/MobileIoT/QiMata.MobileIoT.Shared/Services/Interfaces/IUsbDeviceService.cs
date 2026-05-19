using QiMata.MobileIoT.Shared.Models;

namespace QiMata.MobileIoT.Shared.Services;

/// <summary>Manages a USB device connection, providing enumeration, open/close lifecycle, and bulk read/write operations.</summary>
public interface IUsbDeviceService : IAsyncDisposable
{
    /// <summary>Returns the list of currently attached USB devices.</summary>
    Task<IReadOnlyList<UsbDeviceDescriptor>> ListAsync(CancellationToken ct = default);

    /// <summary>Opens the USB device matching the given vendor and product ID.</summary>
    Task<bool> OpenAsync(ushort vid, ushort pid, CancellationToken ct = default);

    /// <summary>Writes a byte buffer to the open USB device and returns the number of bytes written.</summary>
    Task<int> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    /// <summary>Reads bytes from the open USB device into the supplied buffer and returns the number of bytes read.</summary>
    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default);

    /// <summary>Indicates whether a USB device is currently open.</summary>
    bool IsOpen { get; }
}
