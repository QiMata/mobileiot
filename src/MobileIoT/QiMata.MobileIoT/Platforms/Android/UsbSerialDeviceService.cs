using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Hoho.Android.UsbSerial.Driver;
using QiMata.MobileIoT.Constants;
using QiMata.MobileIoT.Platforms.Android.Services;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Services.Interfaces;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application = Android.App.Application;
using IOException = System.IO.IOException;

namespace QiMata.MobileIoT.Platforms.Android;

public sealed class UsbSerialDeviceService : ISerialDeviceService
{
    private readonly UsbManager _usb;
    private readonly UsbPermissionManager _permissions;
    private readonly IAppLogger _logger;
    private UsbDeviceConnection? _conn;
    private IUsbSerialPort? _port;
    private CancellationTokenSource? _rxCts;
    private bool _isOpen = false;

    public UsbSerialDeviceService(UsbPermissionManager permissions, IAppLogger logger)
    {
        _usb = (UsbManager)Application.Context.GetSystemService(Context.UsbService)!;
        _permissions = permissions;
        _logger = logger;
    }

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

    public async Task<bool> OpenAsync(ushort vid, ushort pid, int baudRate = TransportConstants.UsbDefaultBaud, CancellationToken ct = default)
    {
        var dev = _usb.DeviceList.Values.FirstOrDefault(d => d.VendorId == vid && d.ProductId == pid);
        if (dev == null) return false;

        if (!await _permissions.EnsurePermissionAsync(_usb, dev, ct).ConfigureAwait(false))
        {
            _logger.Warn($"USB permission denied for VID={vid:X4} PID={pid:X4}");
            return false;
        }

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
        var buffer = new byte[TransportConstants.UsbReadBufferSize];
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

    public ValueTask DisposeAsync()
    {
        _isOpen = false;
        _rxCts?.Cancel();
        _port?.Close();
        _conn?.Close();
        _rxCts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
