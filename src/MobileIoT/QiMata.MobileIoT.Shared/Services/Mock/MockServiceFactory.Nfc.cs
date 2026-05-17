using Moq;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Shared.Services.Mock
{
    public static partial class MockServiceFactory
    {
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
                .Callback<string>(text => { mock.Raise(s => s.MessageReceived += null, mock.Object, text); })
                .Returns(Task.CompletedTask);

            return mock.Object;
        }
    }
}
