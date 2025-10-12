using Moq;
using QiMata.MobileIoT.Models;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.ViewModels;
using System.Collections.ObjectModel;
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
        var devices = new ReadOnlyCollection<UsbDeviceDescriptor>(new[]
        {
            new UsbDeviceDescriptor("dev1", 0x1234, 0x5678, null)
        });

        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(devices);
        service.Setup(s => s.OpenAsync(It.IsAny<ushort>(), It.IsAny<ushort>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);

        var vm = new UsbViewModel(service.Object);

        await vm.ConnectAsyncCommand.ExecuteAsync(null);

        Assert.Contains("Connected to dev1", vm.Log);
    }
}
