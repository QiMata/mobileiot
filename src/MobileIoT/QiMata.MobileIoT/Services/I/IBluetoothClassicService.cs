using System.Collections.Generic;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Services.I
{
    public interface IBluetoothClassicService
    {
        Task<IEnumerable<string>> GetPairedDevicesAsync();
        Task ConnectToDeviceAsync(string deviceName);
        Task DisconnectAsync();
        Task SendDataAsync(string data);
        Task<string> ReceiveDataAsync();
    }
}
