using ExternalAccessory;
using Foundation;
using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT.Platforms.iOS;

public sealed class UsbDeviceServiceIos : IUsbDeviceService
{
    EAAccessory? _acc;
    EASession? _session;
    NSInputStream? _in;
    NSOutputStream? _out;

    public bool IsOpen => _session is not null;

    public Task<IReadOnlyList<UsbDeviceInfo>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UsbDeviceInfo>>(
               EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories
                   .Select(a => new UsbDeviceInfo(0, 0, a.Name)).ToList());

    public Task<bool> OpenAsync(ushort vid, ushort pid, CancellationToken ct = default)
    {
        _acc = EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories
                  .FirstOrDefault(a => a.ProtocolStrings.Contains("com.yourcompany.yourprotocol"));
        if (_acc is null) return Task.FromResult(false);
        _session = new EASession(_acc, _acc.ProtocolStrings.First());
        _in  = _session.InputStream;  _in?.Open();
        _out = _session.OutputStream; _out?.Open();
        return Task.FromResult(true);
    }

    public Task<int> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_out is null) return Task.FromResult(0);
        var bytes = data.ToArray();
        return Task.FromResult((int)_out.Write(bytes, 0, bytes.Length));
    }

    public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_in is null || !_in.HasBytesAvailable) return Task.FromResult(0);
        return Task.FromResult((int)_in.Read(buffer.Span));
    }

    public ValueTask DisposeAsync()
    {
        _in?.Close(); _out?.Close(); _session = null;
        return ValueTask.CompletedTask;
    }
}
