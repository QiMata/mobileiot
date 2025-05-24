using ExternalAccessory;
using Foundation;
using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT.Platforms.iOS;

public sealed class UsbSerialPortIos : IUsbSerialPort
{
    EAAccessory? _acc;
    EASession? _session;
    NSInputStream? _in;
    NSOutputStream? _out;

    public bool IsOpen => _session != null;

    public Task<IReadOnlyList<UsbDeviceInfo>> ListDevicesAsync(CancellationToken ct = default)
    {
        var list = EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories
            .Select(a => new UsbDeviceInfo(0, 0, a.Name))
            .ToList();
        return Task.FromResult<IReadOnlyList<UsbDeviceInfo>>(list);
    }

    public Task<bool> OpenAsync(int vid, int pid, int baud = 0, CancellationToken ct = default)
    {
        _acc = EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories
            .FirstOrDefault(a => a.ProtocolStrings.Contains("com.yourcompany.yourdevice"));
        if (_acc == null)
            return Task.FromResult(false);

        _session = new EASession(_acc, _acc.ProtocolStrings.First());
        _in = _session.InputStream;
        _out = _session.OutputStream;
        _in?.Open();
        _out?.Open();
        return Task.FromResult(true);
    }

    public async Task<int> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_out == null)
            return 0;
        var bytes = data.ToArray();
        return await Task.Run(() => (int)_out.Write(bytes, 0, bytes.Length), ct);
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_in == null || !_in.HasBytesAvailable)
            return 0;
        return await Task.Run(() => (int)_in.Read(buffer.Span), ct);
    }

    public ValueTask DisposeAsync()
    {
        _in?.Close();
        _out?.Close();
        _session = null;
        return ValueTask.CompletedTask;
    }
}
