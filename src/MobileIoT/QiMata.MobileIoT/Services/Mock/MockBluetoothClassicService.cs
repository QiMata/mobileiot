using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT.Services.Mock
{
    public class MockBluetoothClassicService : IBluetoothClassicService
    {
        private readonly Queue<string> _receivedData = new();
        private readonly List<string> _pairedDevices = new() { "Mock Device" };
        public Task<IEnumerable<string>> GetPairedDevicesAsync()
        {
            return Task.FromResult<IEnumerable<string>>(_pairedDevices);
        }

        public Task ConnectToDeviceAsync(string deviceName)
        {
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            return Task.CompletedTask;
        }

        public Task SendDataAsync(string data)
        {
            _receivedData.Enqueue(data);
            return Task.CompletedTask;
        }

        public Task<string> ReceiveDataAsync()
        {
            if (_receivedData.Count == 0)
            {
                return Task.FromResult(string.Empty);
            }
            return Task.FromResult(_receivedData.Dequeue());
        }
    }
}
