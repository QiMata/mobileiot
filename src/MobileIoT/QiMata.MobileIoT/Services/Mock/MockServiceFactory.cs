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

            mock.Setup(m => m.ReadDht22Async()).ReturnsAsync((25.0, 60.0));
            mock.Setup(m => m.ToggleLedAsync()).ReturnsAsync(true);

            return mock.Object;
        }
    }
}
