namespace QiMata.MobileIoT.Services;

public interface IUsbDeviceService : IAsyncDisposable
{
    Task<IReadOnlyList<UsbDeviceInfo>> ListAsync(CancellationToken ct = default);
    Task<bool> OpenAsync(ushort vid, ushort pid, CancellationToken ct = default);
    Task<int> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default);
    bool IsOpen { get; }
}
