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
    }
}
