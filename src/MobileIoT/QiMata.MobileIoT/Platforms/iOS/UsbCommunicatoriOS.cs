#if IOS
using ExternalAccessory;
using Foundation;
using QiMata.MobileIoT.Shared.Models;
using QiMata.MobileIoT.Shared.Usb;
using System.Collections.Generic;
using System.Linq;

namespace QiMata.MobileIoT.Platforms.iOS;

public sealed class UsbCommunicatoriOS : IUsbCommunicator
{
    EAAccessory? _acc;  EASession? _sess;
    NSInputStream? _in; NSOutputStream? _out;

    public IEnumerable<UsbDeviceDescriptor> ListDevices() =>
        EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories
            .Select(a => new UsbDeviceDescriptor(a.SerialNumber, 0, 0, a.Name));

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

    public int Write(byte[] d) { _out?.Write(d, 0, (UIntPtr)d.Length); return d.Length; }
    public int Read(byte[] b)  => (int)_in?.Read(b, 0, (UIntPtr)b.Length)!;
}
#endif
