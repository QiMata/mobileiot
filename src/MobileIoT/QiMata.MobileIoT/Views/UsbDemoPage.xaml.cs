using QiMata.MobileIoT.ViewModels;

namespace QiMata.MobileIoT.Views;

public partial class UsbDemoPage : ContentPage
{
    public UsbDemoPage(UsbDemoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
