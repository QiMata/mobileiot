using Moq;
using QiMata.MobileIoT.Services.I;
using QiMata.MobileIoT.ViewModels;
using Xunit;

public class BluetoothServiceTests
{
    [Fact]
    public async Task ViewModel_Updates_When_Temp_Raises()
    {
        var mockBle = new Mock<IBluetoothService>();
        var vm = new MainViewModel(mockBle.Object);

        mockBle.Raise(m => m.TemperatureUpdatedC += null!, 25.6);

        Assert.Equal(25.6, vm.TempC);
    }
}
