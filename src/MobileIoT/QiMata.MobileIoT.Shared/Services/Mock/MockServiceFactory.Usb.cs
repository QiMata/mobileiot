using Moq;
using QiMata.MobileIoT.Shared.Models;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using QiMata.MobileIoT.Shared.Usb;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace QiMata.MobileIoT.Shared.Services.Mock
{
    public static partial class MockServiceFactory
    {
        public static IUsbCommunicator CreateUsbCommunicator(Action<Mock<IUsbCommunicator>>? configure = null)
        {
            var mock = new Mock<IUsbCommunicator>(MockBehavior.Strict);

            var devices = new List<UsbDeviceDescriptor>
            {
                new("Device1", 0x1234, 0x5678, "Demo Device 1"),
                new("Device2", 0x8765, 0x4321, "Demo Device 2"),
                new("Device3", 0x1111, 0x2222, "Demo Device 3")
            };

            string? currentDevice = null;
            var ioQueue = new ConcurrentQueue<byte[]>();

            mock.Setup(c => c.ListDevices())
                .Returns(devices.AsEnumerable());

            mock.Setup(c => c.OpenDevice(It.IsAny<string>()))
                .Returns((string idOrProtocol) =>
                {
                    if (devices.Exists(d => d.Identifier == idOrProtocol))
                    {
                        currentDevice = idOrProtocol;
                        return true;
                    }

                    currentDevice = null;
                    return false;
                });

            mock.Setup(c => c.Write(It.IsAny<byte[]>()))
                .Returns((byte[] data) =>
                {
                    if (currentDevice is null) return 0;

                    ioQueue.Enqueue(data);
                    return data.Length;
                });

            mock.Setup(c => c.Read(It.IsAny<byte[]>()))
                .Returns((byte[] buffer) =>
                {
                    if (currentDevice is null) return 0;

                    if (!ioQueue.TryDequeue(out var data)) return 0;

                    var count = Math.Min(buffer.Length, data.Length);
                    Array.Copy(data, 0, buffer, 0, count);
                    return count;
                });

            configure?.Invoke(mock);
            return mock.Object;
        }
    }
}
