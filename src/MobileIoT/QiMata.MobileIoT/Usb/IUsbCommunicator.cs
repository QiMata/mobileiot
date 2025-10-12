using QiMata.MobileIoT.Models;

namespace QiMata.MobileIoT.Usb;

public interface IUsbCommunicator
{
    IEnumerable<UsbDeviceDescriptor> ListDevices();
    bool OpenDevice(string idOrProtocol);
    int  Write(byte[] data);
    int  Read(byte[] buffer);
}
