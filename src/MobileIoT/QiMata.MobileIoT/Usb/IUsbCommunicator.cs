namespace QiMata.MobileIoT.Usb;

public interface IUsbCommunicator
{
    IEnumerable<UsbDeviceInfo> ListDevices();
    bool OpenDevice(string idOrProtocol);
    int  Write(byte[] data);
    int  Read(byte[] buffer);
}
