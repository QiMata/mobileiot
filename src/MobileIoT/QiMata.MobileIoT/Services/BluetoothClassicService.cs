using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using System.Text;
using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT.Services
{
    public class BluetoothClassicService : IBluetoothClassicService
    {
        private BluetoothClient? _bluetoothClient;
        private BluetoothDeviceInfo? _connectedDevice;

        public async Task<IEnumerable<string>> GetPairedDevicesAsync()
        {
            return await Task.Run(() =>
            {
                var client = new BluetoothClient();
                var devices = client.DiscoverDevices();
                return devices.Select(d => d.DeviceName);
            });
        }

        public async Task ConnectToDeviceAsync(string deviceName)
        {
            await Task.Run(() =>
            {
                var client = new BluetoothClient();
                var devices = client.DiscoverDevices();
                var device = devices.FirstOrDefault(d => d.DeviceName == deviceName);

                if (device == null)
                {
                    throw new Exception("Device not found.");
                }

                _connectedDevice = device;
                _bluetoothClient = new BluetoothClient();
                _bluetoothClient.Connect(device.DeviceAddress, BluetoothService.SerialPort);
            });
        }

        public async Task DisconnectAsync()
        {
            if (_bluetoothClient != null)
            {
                await Task.Run(() =>
                {
                    _bluetoothClient.Close();
                    _bluetoothClient = null;
                    _connectedDevice = null;
                });
            }
        }

        public async Task SendDataAsync(string data)
        {
            if (_bluetoothClient == null || !_bluetoothClient.Connected)
            {
                throw new InvalidOperationException("No device connected.");
            }

            await Task.Run(() =>
            {
                var stream = _bluetoothClient!.GetStream();
                var bytes = Encoding.UTF8.GetBytes(data);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            });
        }

        public async Task<string> ReceiveDataAsync()
        {
            if (_bluetoothClient == null || !_bluetoothClient.Connected)
            {
                throw new InvalidOperationException("No device connected.");
            }

            return await Task.Run(() =>
            {
                var stream = _bluetoothClient!.GetStream();
                var buffer = new byte[1024];
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                return Encoding.UTF8.GetString(buffer, 0, bytesRead);
            });
        }
    }
}
