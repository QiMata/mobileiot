using Android.Content;
using Android.Hardware.Usb;
using Java.IO;
using UsbSerialForAndroid; // NuGet/Xamarin binding
using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT.Platforms.Android;

public sealed class UsbSerialDeviceService : ISerialDeviceService
{
    private const int READ_BUFF_SZ = 4096;

    private readonly UsbManager _usb;
    private UsbDeviceConnection? _conn;
    private UsbSerialPort? _port;
    private CancellationTokenSource? _rxCts;
    private static readonly Dictionary<int, TaskCompletionSource> _permBlocks = new();

    public UsbSerialDeviceService()
    {
        _usb = (UsbManager)Android.App.Application.Context.GetSystemService(Context.UsbService)!;
    }

    public bool IsOpen => _port?.IsOpen == true;

    public async Task<IReadOnlyList<SerialDeviceInfo>> ListAsync(CancellationToken ct = default)
    {
        var list = new List<SerialDeviceInfo>();
        foreach (var dev in _usb.DeviceList.Values)
            list.Add(new SerialDeviceInfo((ushort)dev.VendorId, (ushort)dev.ProductId, dev.DeviceName));
        return list;
    }

    public async Task<bool> OpenAsync(ushort vid, ushort pid, int baudRate = 9600, CancellationToken ct = default)
    {
        var dev = _usb.DeviceList.Values.FirstOrDefault(d => d.VendorId == vid && d.ProductId == pid);
        if (dev == null) return false;

        if (!_usb.HasPermission(dev))
        {
            var tcs = new TaskCompletionSource();
            _permBlocks[dev.DeviceId] = tcs;

            var pi = PendingIntent.GetBroadcast(Android.App.Application.Context, 0,
                     new Intent(UsbPermissionBroadcastReceiver.ACTION_USB_PERMISSION),
                     PendingIntentFlags.Immutable);
            _usb.RequestPermission(dev, pi);
            await tcs.Task.WaitAsync(ct);
        }

        _conn = _usb.OpenDevice(dev);
        if (_conn == null) return false;

        var driver = UsbSerialProber.DefaultProber.FindAllDrivers(_usb)
                                                  .First(d => d.Device.DeviceId == dev.DeviceId);
        _port = driver.Ports[0];
        _port.Open(_conn);
        _port.SetParameters(baudRate, 8, StopBits.One, Parity.None);

        _rxCts = new CancellationTokenSource();
        _ = Task.Run(() => RxLoop(_rxCts.Token), _rxCts.Token);
        return true;
    }

    public Task<int> WriteAsync(byte[] data, CancellationToken ct = default)
    {
        if (_port == null) throw new InvalidOperationException("Port not open");
        int sent = _port.Write(data, 1000);
        return Task.FromResult(sent);
    }

    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    private void RxLoop(CancellationToken token)
    {
        var buffer = new byte[READ_BUFF_SZ];
        while (!token.IsCancellationRequested && _port != null)
        {
            try
            {
                int len = _port.Read(buffer, 100);
                if (len > 0)
                    DataReceived?.Invoke(this, new ReadOnlyMemory<byte>(buffer, 0, len));
            }
            catch (IOException)
            {
                break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _rxCts?.Cancel();
        _port?.Close();
        _conn?.Close();
        _rxCts?.Dispose();
    }

    internal static void UnblockPermission(int deviceId)
    {
        if (_permBlocks.TryGetValue(deviceId, out var tcs))
            tcs.TrySetResult();
    }
}
