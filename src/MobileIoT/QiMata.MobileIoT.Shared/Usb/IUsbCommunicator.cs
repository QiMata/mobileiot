using QiMata.MobileIoT.Shared.Models;

namespace QiMata.MobileIoT.Shared.Usb;

public interface IUsbCommunicator
{
    IEnumerable<UsbDeviceDescriptor> ListDevices();
    bool OpenDevice(string idOrProtocol);
    int  Write(byte[] data);
    int  Read(byte[] buffer);
}
