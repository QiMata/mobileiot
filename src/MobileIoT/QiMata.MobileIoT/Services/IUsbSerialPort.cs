namespace QiMata.MobileIoT.Services;

public interface IUsbSerialPort : IAsyncDisposable
{
    Task<IReadOnlyList<UsbDeviceInfo>> ListDevicesAsync(CancellationToken ct = default);
    Task<bool> OpenAsync(int vendorId, int productId, int baudRate = 115200, CancellationToken ct = default);
    Task<int> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default);
    bool IsOpen { get; }
}
