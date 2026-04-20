using Moq;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QiMata.MobileIoT.Tests;

public class SerialDemoViewModelTests
{
    [Fact]
    public async Task ConnectAsync_LogsWhenNoDevicesFound()
    {
        var service = new Mock<ISerialDeviceService>();
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<SerialDeviceInfo>());

        var vm = new SerialDemoViewModel(service.Object);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.Contains("No devices found", vm.Log);
    }

    [Fact]
    public async Task ConnectAsync_LogsConnectionResult()
    {
        var service = new Mock<ISerialDeviceService>();
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<SerialDeviceInfo>
               {
                   new SerialDeviceInfo(0x1234, 0x5678, "Demo")
               });
        service.Setup(s => s.OpenAsync(It.IsAny<ushort>(), It.IsAny<ushort>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);

        var vm = new SerialDemoViewModel(service.Object);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.Contains("Connected", vm.Log);
    }

    [Fact]
    public async Task SendAsync_AppendsWhenPortClosed()
    {
        var service = new Mock<ISerialDeviceService>();
        service.SetupGet(s => s.IsOpen).Returns(false);

        var vm = new SerialDemoViewModel(service.Object);

        await vm.SendLedOnCommand.ExecuteAsync(null);

        Assert.Contains("Not connected", vm.Log);
        service.Verify(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_TogglesLedCommands()
    {
        var service = new Mock<ISerialDeviceService>();
        service.SetupGet(s => s.IsOpen).Returns(true);
        service.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(8);

        var vm = new SerialDemoViewModel(service.Object);

        await vm.SendLedOnCommand.ExecuteAsync(null);
        await vm.SendLedOffCommand.ExecuteAsync(null);

        service.Verify(s => s.WriteAsync(It.Is<byte[]>(data => Encoding.ASCII.GetString(data) == "LED_ON\n"), It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(s => s.WriteAsync(It.Is<byte[]>(data => Encoding.ASCII.GetString(data) == "LED_OFF\n"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("TX: LED_ON", vm.Log);
        Assert.Contains("TX: LED_OFF", vm.Log);
    }

    [Fact]
    public void DataReceived_AppendsToLog()
    {
        var service = new Mock<ISerialDeviceService>();
        var vm = new SerialDemoViewModel(service.Object);

        service.Raise(s => s.DataReceived += null!, service.Object, new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("hello")));

        Assert.Contains("RX: hello", vm.Log);
    }
}
