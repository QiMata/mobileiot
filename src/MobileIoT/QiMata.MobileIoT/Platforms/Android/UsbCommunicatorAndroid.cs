#if ANDROID
using Android.Content;
using Android.Hardware.Usb;
using Microsoft.Maui.ApplicationModel;
using QiMata.MobileIoT.Shared.Models;
using QiMata.MobileIoT.Shared.Usb;
using System.Collections.Generic;
using System.Linq;

namespace QiMata.MobileIoT.Platforms.Android;

public sealed class UsbCommunicatorAndroid : IUsbCommunicator
{
    readonly UsbManager _mgr;
    UsbDeviceConnection? _conn;
    UsbEndpoint? _in;
    UsbEndpoint? _out;

    public UsbCommunicatorAndroid() =>
        _mgr = (UsbManager)(Platform.CurrentActivity?.GetSystemService(Context.UsbService) ??
                            global::Android.App.Application.Context.GetSystemService(Context.UsbService))!;

    public IEnumerable<UsbDeviceDescriptor> ListDevices() =>
        _mgr.DeviceList.Values.Select(d => new UsbDeviceDescriptor(
            d.DeviceName,
            (ushort)d.VendorId,
            (ushort)d.ProductId,
            d.ProductName));

    public bool OpenDevice(string id)
    {
        var dev = _mgr.DeviceList.Values.FirstOrDefault(d => d.DeviceName == id);
        if (dev is null || !_mgr.HasPermission(dev)) return false;

        var intf = dev.GetInterface(0);
        for (int i = 0; i < intf.EndpointCount; i++)
        {
            var ep = intf.GetEndpoint(i);
            if (ep.Type == UsbAddressing.XferBulk)
            {
                if (ep.Direction == UsbAddressing.In)
                    _in = ep;
                else
                    _out = ep;
            }
        }

        if (_in is null || _out is null) return false;

        _conn = _mgr.OpenDevice(dev);
        return _conn?.ClaimInterface(intf, true) ?? false;
    }

    public int Write(byte[] data) =>
        _conn?.BulkTransfer(_out!, data, data.Length, 1000) ?? -1;

    public int Read(byte[] buff) =>
        _conn?.BulkTransfer(_in!, buff, buff.Length, 1000) ?? -1;
}
#endif
