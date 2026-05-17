using Moq;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Shared.Services.Mock
{
    public static partial class MockServiceFactory
    {
        public static IBluetoothService CreateBluetoothService()
        {
            var mock = new Mock<IBluetoothService>();

            // Standard async members
            mock.Setup(m => m.ConnectAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(true);
            mock.Setup(m => m.ToggleLedAsync(It.IsAny<bool>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);
            mock.Setup(m => m.DisconnectAsync()).Returns(Task.CompletedTask);
            mock.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);
            mock.Setup(m => m.ReadHumidityAsync(It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.FromResult(25.0f));
            mock.Setup(m => m.ReadTemperatureAsync(It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.FromResult(25.0f));
            mock.Setup(m => m.StartSensorNotificationsAsync(It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);
            mock.Setup(m => m.StopSensorNotificationsAsync(It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);

            return mock.Object;
        }
    }
}
