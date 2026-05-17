using Moq;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Shared.Services.Mock
{
    public static partial class MockServiceFactory
    {
        public static IBleDemoService CreateBleDemoService()
        {
            var mock = new Moq.Mock<IBleDemoService>();
            var random = new Random();
            var ledState = false;

            mock.Setup(m => m.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            mock.Setup(m => m.DisconnectAsync()).Returns(Task.CompletedTask);

            mock.Setup(m => m.ReadDht22Async(It.IsAny<CancellationToken>()))
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

            mock.Setup(m => m.StartStreamingAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mock.Setup(m => m.StopStreamingAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            return mock.Object;
        }
    }
}
