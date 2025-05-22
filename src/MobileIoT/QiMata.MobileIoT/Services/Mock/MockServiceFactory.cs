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
                    var temperature = random.NextDouble() * 20 + 15;   // 15-35 °C
                    var humidity = random.NextDouble() * 60 + 20;      // 20-80 %
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

    }
}
