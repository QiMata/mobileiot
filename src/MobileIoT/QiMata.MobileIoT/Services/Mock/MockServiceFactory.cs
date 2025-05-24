using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT.Services.Mock
{
    internal static class MockServiceFactory
    {
        public static IBleDemoService CreateBleDemoService()
        {
            var mock = new Moq.Mock<IBleDemoService>();
            var random = new Random();
            var ledState = false;

            mock.Setup(m => m.ReadDht22Async())
                .ReturnsAsync(() =>
                {
                    var temperature = random.NextDouble() * 20 + 15; // 15-35 °C
                    var humidity = random.NextDouble() * 60 + 20; // 20-80 %
                    return (temperature, humidity);
                });

            mock.Setup(m => m.ToggleLedAsync())
                .ReturnsAsync(() =>
                {
                    ledState = !ledState;
                    return ledState;
                });

            return mock.Object;
        }

        public static IBluetoothService CreateBluetoothService()
        {
            var mock = new Mock<IBluetoothService>();

            // Standard async members
            mock.Setup(m => m.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            mock.Setup(m => m.StartSensorNotificationsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mock.Setup(m => m.ToggleLedAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mock.Setup(m => m.DisconnectAsync()).Returns(Task.CompletedTask);
            mock.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

            // Capture event subscriptions so tests can invoke them later
            EventHandler<double>? tempHandler = null;
            EventHandler<double>? humidityHandler = null;

            mock.SetupAdd(m => m.TemperatureUpdatedC += It.IsAny<EventHandler<double>>())
                .Callback<EventHandler<double>>(h => tempHandler = h);
            mock.SetupRemove(m => m.TemperatureUpdatedC -= It.IsAny<EventHandler<double>>())
                .Callback<EventHandler<double>>(_ => tempHandler = null);

            mock.SetupAdd(m => m.HumidityUpdatedPercent += It.IsAny<EventHandler<double>>())
                .Callback<EventHandler<double>>(h => humidityHandler = h);
            mock.SetupRemove(m => m.HumidityUpdatedPercent -= It.IsAny<EventHandler<double>>())
                .Callback<EventHandler<double>>(_ => humidityHandler = null);

            // Optionally fire initial readings
            tempHandler?.Invoke(mock.Object, 25.0);
            humidityHandler?.Invoke(mock.Object, 50.0);

            return mock.Object;
        }

        public static INfcService CreateNfcService()
        {
            var mock = new Mock<INfcService>();

            mock.SetupGet(m => m.IsAvailable).Returns(true);
            mock.SetupGet(m => m.IsEnabled).Returns(true);

            // Simulate incoming NFC message shortly after listening starts
            mock.Setup(m => m.StartListeningAsync()).Returns(() =>
            {
                return Task.Run(async () =>
                {
                    await Task.Delay(500); // simulate wait time for a tag
                    mock.Raise(s => s.MessageReceived += null, mock.Object, "Mock NFC payload");
                });
            });

            mock.Setup(m => m.StopListeningAsync()).Returns(Task.CompletedTask);

            // Echo back written text as if it were immediately read
            mock.Setup(m => m.WriteTextAsync(It.IsAny<string>()))
                .Callback<string>(text =>
                {
                    mock.Raise(s => s.MessageReceived += null, mock.Object, text);
                })
                .Returns(Task.CompletedTask);

            return mock.Object;
        }

        public static INfcP2PService CreateNfcP2PService()
        {
            var mock = new Mock<INfcP2PService>();
            mock.Setup(m => m.StartP2P(It.IsAny<string>()));
            mock.Setup(m => m.StopP2P());
            return mock.Object;
        }

        public static IBeaconScanner CreateBeaconScanner()
        {
            var mock = new Mock<IBeaconScanner>();

            var random = new Random();
            Timer? timer = null;

            mock.SetupGet(s => s.IsScanning).Returns(() => timer != null);

            mock.Setup(s => s.StartScanning()).Callback(() =>
            {
                if (timer != null) return; // Already scanning

                timer = new Timer(_ =>
                {
                    var data = new byte[20];
                    random.NextBytes(data);
                    var rssi = random.Next(-100, -20);

                    mock.Raise(m => m.AdvertisementReceived += null,
                        mock.Object,
                        new BeaconAdvertisement(data, rssi));
                }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500)); // every 500 ms
            });

            mock.Setup(s => s.StopScanning()).Callback(() =>
            {
                timer?.Dispose();
                timer = null;
            });

            return mock.Object;
        }
    }
}
