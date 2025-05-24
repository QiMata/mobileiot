using QiMata.MobileIoT.Usb;

namespace QiMata.MobileIoT.ViewModels;

public class UsbViewModel(IUsbCommunicator usb)
{
    public IEnumerable<UsbDeviceInfo> Devices => usb.ListDevices();
    public Task<bool> Connect(string id) => Task.FromResult(usb.OpenDevice(id));
    public Task<int>  Send(byte[] d)    => Task.FromResult(usb.Write(d));
    public Task<int>  Receive(byte[] b) => Task.FromResult(usb.Read(b));
}
