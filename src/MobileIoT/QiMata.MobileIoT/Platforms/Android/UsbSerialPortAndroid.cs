using Android.Content;
using UsbSerialForAndroid;
using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT.Platforms.Android;

public sealed class UsbSerialPortAndroid : IUsbSerialPort
{
    readonly Context _ctx = Android.App.Application.Context;
    UsbSerialDevice? _device;
    ISerialInputOutputManager? _ioManager;

    public bool IsOpen => _device?.IsOpened ?? false;

    public Task<IReadOnlyList<UsbDeviceInfo>> ListDevicesAsync(CancellationToken ct = default)
    {
        var mgr = UsbSerialProber.DefaultProber;
        var drivers = mgr.FindAllDrivers(_ctx);
        var list = drivers
            .SelectMany(d => d.Ports)
            .Select(p => new UsbDeviceInfo(
                p.Driver.Device.VendorId,
                p.Driver.Device.ProductId,
                p.Driver.Device.DeviceName))
            .ToList();
        return Task.FromResult<IReadOnlyList<UsbDeviceInfo>>(list);
    }

    public Task<bool> OpenAsync(int vendorId, int productId, int baudRate = 115200, CancellationToken ct = default)
    {
        var prober = UsbSerialProber.DefaultProber;
        var driver = prober.FindAllDrivers(_ctx)
            .FirstOrDefault(d => d.Device.VendorId == vendorId && d.Device.ProductId == productId);
        if (driver == null)
            return Task.FromResult(false);

        var connection = UsbManager.OpenDevice(driver.Device);
        if (connection == null)
            return Task.FromResult(false);

        _device = driver.Ports.First();
        _device.Open(connection);
        _device.SetParameters(baudRate, 8, StopBits.One, Parity.None);

        _ioManager = new SerialInputOutputManager(_device);
        _ioManager.Start();

        return Task.FromResult(true);
    }

    public Task<int> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => Task.FromResult(_device?.Write(data.ToArray()) ?? 0);

    public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        => Task.FromResult(_device?.Read(buffer.Span) ?? 0);

    public ValueTask DisposeAsync()
    {
        _ioManager?.Close();
        _device?.Close();
        _ioManager = null;
        _device = null;
        return ValueTask.CompletedTask;
    }
}
