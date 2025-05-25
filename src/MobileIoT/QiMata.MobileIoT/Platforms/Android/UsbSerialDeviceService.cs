using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Hoho.Android.UsbSerial.Driver;
using QiMata.MobileIoT.Services;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application = Android.App.Application;
using IOException = System.IO.IOException;

namespace QiMata.MobileIoT.Platforms.Android;

public sealed class UsbSerialDeviceService : ISerialDeviceService
{
    private const int READ_BUFF_SZ = 4096;

    private readonly UsbManager _usb;
    private UsbDeviceConnection? _conn;
    private IUsbSerialPort? _port;
    private CancellationTokenSource? _rxCts;

    private static readonly Dictionary<int, TaskCompletionSource<bool>> _permBlocks = new();

    public UsbSerialDeviceService()
        => _usb = (UsbManager)Application.Context.GetSystemService(Context.UsbService)!;

    private bool _isOpen = false;
    public bool IsOpen => _port != null && _isOpen;

    public Task<IReadOnlyList<SerialDeviceInfo>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SerialDeviceInfo>>(
               _usb.DeviceList.Values
                   .Select(d => new SerialDeviceInfo(
                                    (ushort)d.VendorId,
                                    (ushort)d.ProductId,
                                    d.DeviceName))
                   .ToList()
                   .AsReadOnly());

    public async Task<bool> OpenAsync(ushort vid, ushort pid, int baudRate = 9600, CancellationToken ct = default)
    {
        var dev = _usb.DeviceList.Values.FirstOrDefault(d => d.VendorId == vid && d.ProductId == pid);
        if (dev == null) return false;

        //-- Request runtime permission if we don't have it ––––––––––––––––––––––
        if (!_usb.HasPermission(dev))
        {
            var tcs = new TaskCompletionSource<bool>();
            _permBlocks[dev.DeviceId] = tcs;

            var pi = PendingIntent.GetBroadcast(
                         Application.Context, 0,
                         new Intent(UsbPermissionBroadcastReceiver.ACTION_USB_PERMISSION),
                         PendingIntentFlags.Immutable);

            _usb.RequestPermission(dev, pi);
            await tcs.Task.WaitAsync(ct);
        }
        //-- Open connection / configure port –––––––––––––––––––––––––––––––––––
        _conn = _usb.OpenDevice(dev);
        if (_conn == null) return false;

        var driver = UsbSerialProber.DefaultProber
                                    .FindAllDrivers(_usb)
                                    .First(d => d.Device.DeviceId == dev.DeviceId);

        _port = driver.Ports[0];
        _port.Open(_conn);
        _isOpen = true;
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
                break; // Device unplugged
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _isOpen = false;
        _rxCts?.Cancel();
        _port?.Close();
        _conn?.Close();
        _rxCts?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Called by the broadcast receiver once permission is granted.</summary>
    internal static void UnblockPermission(int deviceId)
    {
        if (_permBlocks.TryGetValue(deviceId, out var tcs))
            tcs.TrySetResult(true);
    }
}