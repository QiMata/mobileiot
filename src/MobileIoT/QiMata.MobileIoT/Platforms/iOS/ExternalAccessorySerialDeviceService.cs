using ExternalAccessory;
using Foundation;
using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT.Platforms.iOS;

public sealed class ExternalAccessorySerialDeviceService : NSObject, ISerialDeviceService, INSStreamDelegate
{
    private const int READ_BUFF_SZ = 4096;
    private EAAccessory? _acc;
    private EASession? _session;

    public bool IsOpen => _session != null;

    public Task<IReadOnlyList<SerialDeviceInfo>> ListAsync(CancellationToken ct = default)
    {
        var list = EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories
                     .Select(a => new SerialDeviceInfo(0, 0, a.Name))
                     .ToList()
                     .AsReadOnly();
        return Task.FromResult<IReadOnlyList<SerialDeviceInfo>>(list);
    }

    public Task<bool> OpenAsync(ushort vid, ushort pid, int baudRate = 0, CancellationToken ct = default)
    {
        _acc = EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories.FirstOrDefault();
        if (_acc == null) return Task.FromResult(false);

        var protocol = _acc.ProtocolStrings.First();
        _session = new EASession(_acc, protocol);
        _session.InputStream.Delegate  = this;
        _session.OutputStream.Schedule(NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
        _session.InputStream.Schedule(NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
        _session.OutputStream.Open();
        _session.InputStream.Open();
        return Task.FromResult(true);
    }

    public Task<int> WriteAsync(byte[] data, CancellationToken ct = default)
    {
        if (_session == null) throw new InvalidOperationException("Not open");
        var nsdata = NSData.FromArray(data);
        _session.OutputStream.Write(nsdata.Bytes, (nuint)nsdata.Length);
        return Task.FromResult(data.Length);
    }

    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    [Export("stream:handleEvent:")]
    public void HandleEvent(NSStream theStream, NSStreamEvent streamEvent)
    {
        if (streamEvent.HasFlag(NSStreamEvent.HasBytesAvailable))
        {
            var buffer = new byte[READ_BUFF_SZ];
            nuint len = _session!.InputStream.Read(buffer, (nuint)buffer.Length);
            if (len > 0)
                DataReceived?.Invoke(this, new ReadOnlyMemory<byte>(buffer, 0, (int)len));
        }
    }

    public ValueTask DisposeAsync()
    {
        _session?.InputStream.Close();
        _session?.OutputStream.Close();
        _session?.InputStream.RemoveFromRunLoop(NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
        _session?.OutputStream.RemoveFromRunLoop(NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
        _session = null;
        return ValueTask.CompletedTask;
    }
}
