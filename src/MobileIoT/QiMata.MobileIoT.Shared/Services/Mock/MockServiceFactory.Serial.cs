using Moq;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using QiMata.MobileIoT.Shared.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Shared.Services.Mock
{
    public static partial class MockServiceFactory
    {
        public static ISerialDeviceService CreateSerialDeviceService(Action<Mock<ISerialDeviceService>>? configure = null)
        {
            var mock = new Mock<ISerialDeviceService>(MockBehavior.Strict);

            var devices = new List<SerialDeviceInfo>
        {
            new(0x2341, 0x0043, "Arduino Uno"),
            new(0x10C4, 0xEA60, "CP2102 USB-UART")
        };

            var isOpen = false;
            EventHandler<ReadOnlyMemory<byte>>? dataReceived = null;

            mock.SetupGet(s => s.IsOpen)
                .Returns(() => isOpen);

            mock.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices.AsReadOnly());

            mock.Setup(s => s.OpenAsync(It.IsAny<ushort>(),
                                        It.IsAny<ushort>(),
                                        It.IsAny<int>(),
                                        It.IsAny<CancellationToken>()))
                .Returns((ushort vendorId, ushort productId, int _, CancellationToken __) =>
                {
                    isOpen = devices.Exists(d => d.VendorId == vendorId && d.ProductId == productId);
                    return Task.FromResult(isOpen);
                });

            mock.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns((byte[] data, Task _) =>
                {
                    if (!isOpen) return Task.FromResult(0);

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(10);
                        dataReceived?.Invoke(mock.Object, data);
                    });

                    return Task.FromResult(data.Length);
                });

            mock.SetupAdd(m => m.DataReceived += It.IsAny<EventHandler<ReadOnlyMemory<byte>>>())
                .Callback<EventHandler<ReadOnlyMemory<byte>>>(h => dataReceived += h);

            mock.SetupRemove(m => m.DataReceived -= It.IsAny<EventHandler<ReadOnlyMemory<byte>>>())
                .Callback<EventHandler<ReadOnlyMemory<byte>>>(h => dataReceived -= h);

            mock.Setup(s => s.DisposeAsync())
                .Returns(() =>
                {
                    isOpen = false;
                    return ValueTask.CompletedTask;
                });

            configure?.Invoke(mock);
            return mock.Object;
        }
    }
}
