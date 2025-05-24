#if IOS
using ExternalAccessory;
using Foundation;
using QiMata.MobileIoT.Usb;
using System.Collections.Generic;
using System.Linq;

namespace QiMata.MobileIoT.Platforms.iOS;

public sealed class UsbCommunicatoriOS : IUsbCommunicator
{
    EAAccessory? _acc;  EASession? _sess;
    NSInputStream? _in; NSOutputStream? _out;

    public IEnumerable<UsbDeviceInfo> ListDevices() =>
        EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories
            .Select(a => new UsbDeviceInfo(a.SerialNumber, 0, 0));

    public bool OpenDevice(string protocol)
    {
        _acc = EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories
                  .FirstOrDefault(a => a.ProtocolStrings.Contains(protocol));
        if (_acc is null) return false;

        _sess = new EASession(_acc, protocol);
        _in = _sess.InputStream;  _out = _sess.OutputStream;
        _in?.Open();  _out?.Open();
        return _in is not null && _out is not null;
    }

    public int Write(byte[] d) { _out?.Write(d, 0, d.Length); return d.Length; }
    public int Read(byte[] b)  => _in?.Read(b, 0, b.Length) ?? -1;
}
#endif
