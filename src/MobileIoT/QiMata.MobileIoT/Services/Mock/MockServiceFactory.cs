using Moq;
using QiMata.MobileIoT.Services.I;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using QiMata.MobileIoT.Usb;

namespace QiMata.MobileIoT.Services.Mock
{
    public interface INfcP2PTestHarness
    {
        IReadOnlyList<string> SentMessages { get; }
        void SimulateIncoming(string mimeType, string text, byte[]? rawPayload = null);
    }

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
                .Callback<string>(text => { mock.Raise(s => s.MessageReceived += null, mock.Object, text); })
                .Returns(Task.CompletedTask);

            return mock.Object;
        }

        public static INfcP2PService CreateNfcP2PService()
        {
            // public (or InternalsVisibleTo‐exposed) extra interface
            var mock = new Mock<INfcP2PService>();

            var sent = new List<string>();

            mock.Setup(m => m.StartP2P(It.IsAny<string>()))
                .Callback<string>(msg => sent.Add(msg));

            mock.Setup(m => m.StopP2P());

            // add test-only helpers
            mock.As<INfcP2PTestHarness>()
                .SetupGet(h => h.SentMessages)
                .Returns(sent);

            mock.As<INfcP2PTestHarness>()
                .Setup(h => h.SimulateIncoming(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>()))
                .Callback<string, string, byte[]>((mime, text, raw) =>
                {
                    raw ??= System.Text.Encoding.UTF8.GetBytes(text);
                    mock.Raise(s => s.MessageReceived += null,
                        new NfcMessageEventArgs(mime, text, raw));
                });

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

                var deviceList = new List<string>
                {
                    $"AA:BB:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}",
                    $"AA:BB:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}",
                    $"AA:BB:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}",
                    $"AA:BB:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}",
                    $"AA:BB:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}:{random.Next(0, 255):X2}"
                };


                timer = new Timer(_ =>
                {
                    var data = new byte[20];
                    random.NextBytes(data);
                    var rssi = random.Next(-100, -20);
                    var deviceId = deviceList[random.Next(deviceList.Count)];

                    mock.Raise(m => m.AdvertisementReceived += null,
                        mock.Object,
                        new BeaconAdvertisement(
                            deviceId,
                            "MockBeacon",
                            data,
                            rssi));
                }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500)); // every 500 ms
            });

            mock.Setup(s => s.StopScanning()).Callback(() =>
            {
                timer?.Dispose();
                timer = null;
            });

            return mock.Object;
        }

        public static IP2PService CreateP2PService(Action<Mock<IP2PService>>? configure = null)
        {
            var mock = new Mock<IP2PService>(MockBehavior.Strict);

            var connectedPeers = new HashSet<string>();
            var channel = Channel.CreateUnbounded<(string PeerId, ReadOnlyMemory<byte> Data)>();

            mock.Setup(s => s.StartDiscoveryAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(s => s.ConnectToPeerAsync(It.IsAny<string>(),
                                                 It.IsAny<CancellationToken>()))
                .Returns((string peerId, CancellationToken _) =>
                {
                    connectedPeers.Add(peerId);
                    return Task.FromResult(true);
                });

            mock.Setup(s => s.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(),
                                        It.IsAny<string?>(),
                                        It.IsAny<CancellationToken>()))
                .Returns((ReadOnlyMemory<byte> buffer, string? peerId, CancellationToken _) =>
                {
                    var target = peerId ?? "broadcast";
                    channel.Writer.TryWrite((target, buffer));
                    return Task.FromResult(true);
                });

            mock.Setup(s => s.ReceiveAsync(It.IsAny<CancellationToken>()))
                .Returns((CancellationToken ct) => ReadMessagesAsync(ct));

            mock.Setup(s => s.StopAsync())
                .Returns(() =>
                {
                    channel.Writer.TryComplete();
                    return Task.CompletedTask;
                });

            configure?.Invoke(mock);
            return mock.Object;

            async IAsyncEnumerable<(string PeerId, ReadOnlyMemory<byte> Data)> ReadMessagesAsync(
                [EnumeratorCancellation] CancellationToken ct)
            {
                while (await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (channel.Reader.TryRead(out var message))
                    {
                        yield return message;
                    }
                }
            }
        }

        public static IUsbCommunicator CreateUsbCommunicator(Action<Mock<IUsbCommunicator>>? configure = null)
        {
            var mock = new Mock<IUsbCommunicator>(MockBehavior.Strict);

            var devices = new List<Usb.UsbDeviceInfo>
            {
                new Usb.UsbDeviceInfo("Device1", 0x1234, 0x5678),
                new Usb.UsbDeviceInfo("Device2", 0x8765, 0x4321),
                new Usb.UsbDeviceInfo("Device3", 0x1111, 0x2222)
            };

            string? currentDevice = null;
            var ioQueue = new ConcurrentQueue<byte[]>();

            mock.Setup(c => c.ListDevices())
                .Returns(devices.AsEnumerable());

            mock.Setup(c => c.OpenDevice(It.IsAny<string>()))
                .Returns((string idOrProtocol) =>
                {
                    if (devices.Exists(d => d.Id == idOrProtocol))
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
