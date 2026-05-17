using Moq;
using QiMata.MobileIoT.Shared.Models;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using System;
using System.Collections.Generic;

namespace QiMata.MobileIoT.Shared.Services.Mock
{
    public static partial class MockServiceFactory
    {
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
    }
}
