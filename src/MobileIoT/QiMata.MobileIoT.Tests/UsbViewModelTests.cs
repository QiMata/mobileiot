using QiMata.MobileIoT.Shared.Services;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using Moq;
using QiMata.MobileIoT.Shared.Models;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QiMata.MobileIoT.Tests;

public class UsbViewModelTests
{
    [Fact]
    public async Task ConnectAsync_UsesIdentifierWhenNameMissing()
    {
        var service = new Mock<IUsbDeviceService>();
        var logger = new Mock<IAppLogger>();
        var devices = new ReadOnlyCollection<UsbDeviceDescriptor>(new[]
        {
            new UsbDeviceDescriptor("dev1", 0x1234, 0x5678, null)
        });

        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(devices);
        service.Setup(s => s.OpenAsync(It.IsAny<ushort>(), It.IsAny<ushort>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);

        var vm = new UsbViewModel(service.Object, logger.Object);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.Contains("Connected to dev1", vm.Log);
    }

    [Fact]
    public async Task ConnectAsync_LogsFailureWhenNoDevices()
    {
        var service = new Mock<IUsbDeviceService>();
        var logger = new Mock<IAppLogger>();
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<UsbDeviceDescriptor>());

        var vm = new UsbViewModel(service.Object, logger.Object);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.Contains("No device or failed to open", vm.Log);
    }

    [Fact]
    public async Task ConnectAsync_LogsFailureWhenOpenFails()
    {
        var service = new Mock<IUsbDeviceService>();
        var logger = new Mock<IAppLogger>();
        var devices = new ReadOnlyCollection<UsbDeviceDescriptor>(new[]
        {
            new UsbDeviceDescriptor("dev2", 0x1234, 0x5678, "Display")
        });

        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(devices);
        service.Setup(s => s.OpenAsync(It.IsAny<ushort>(), It.IsAny<ushort>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(false);

        var vm = new UsbViewModel(service.Object, logger.Object);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.Contains("No device or failed to open", vm.Log);
    }

    [Fact]
    public async Task SendPingAsync_DoesNothingWhenPortClosed()
    {
        var service = new Mock<IUsbDeviceService>();
        var logger = new Mock<IAppLogger>();
        service.SetupGet(s => s.IsOpen).Returns(false);

        var vm = new UsbViewModel(service.Object, logger.Object);

        await vm.SendPingCommand.ExecuteAsync(null);

        service.Verify(s => s.WriteAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(string.Empty, vm.Log);
    }

    [Fact]
    public async Task SendPingAsync_WritesAndLogsWhenOpen()
    {
        var service = new Mock<IUsbDeviceService>();
        var logger = new Mock<IAppLogger>();
        service.SetupGet(s => s.IsOpen).Returns(true);
        service.Setup(s => s.WriteAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(4);
        service.Setup(s => s.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(3)
               .Callback<Memory<byte>, CancellationToken>((buffer, _) =>
               {
                   var span = buffer.Span;
                   span[0] = 0x41;
                   span[1] = 0x42;
                   span[2] = 0x43;
               });

        var vm = new UsbViewModel(service.Object, logger.Object);

        await vm.SendPingCommand.ExecuteAsync(null);

        service.Verify(s => s.WriteAsync(It.Is<ReadOnlyMemory<byte>>(data => data.ToArray().SequenceEqual(new byte[] { 0x50, 0x49, 0x4E, 0x47 })), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("RX 3 bytes", vm.Log);
    }
}
