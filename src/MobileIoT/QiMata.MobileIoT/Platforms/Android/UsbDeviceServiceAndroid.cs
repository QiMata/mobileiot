using Device.Net;
using Usb.Net.Android;
using Microsoft.Extensions.Logging;
using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT.Platforms.Android;

public sealed class UsbDeviceServiceAndroid(ILoggerFactory loggerFactory) : IUsbDeviceService
{
    IDevice? _device;
    IDeviceFactory? _factory;

    public bool IsOpen => _device?.IsInitialized ?? false;

    public async Task<IReadOnlyList<UsbDeviceInfo>> ListAsync(CancellationToken ct = default)
    {
        var defs = await AndroidDeviceFactory.GetAndroidDeviceDefinitionsAsync(ct);
        return defs.Select(d => new UsbDeviceInfo(
                                 (ushort)(d.VendorId ?? 0),
                                 (ushort)(d.ProductId ?? 0),
                                 d.ProductName ?? "Unknown"))
                   .ToList();
    }

    public async Task<bool> OpenAsync(ushort vid, ushort pid, CancellationToken ct = default)
    {
        _factory = new FilterDeviceDefinition(vid, pid)
                       .CreateDeviceFactory(loggerFactory);
        var defs = await _factory.GetConnectedDeviceDefinitionsAsync(ct);
        if (!defs.Any()) return false;

        _device = await _factory.GetDeviceAsync(defs.First(), ct);
        await _device.InitializeAsync(ct);
        return _device.IsInitialized;
    }

    public Task<int> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => _device?.WriteAsync(data, ct) ?? Task.FromResult(0);

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_device is null) return 0;
        var result = await _device.ReadAsync(ct);
        result.CopyTo(buffer.Span);
        return result.Length;
    }

    public async ValueTask DisposeAsync()
    {
        if (_device is { } dev) await dev.CloseAsync();
    }
}
