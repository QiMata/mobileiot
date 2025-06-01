using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.ViewModels;

namespace QiMata.MobileIoT.Views;

public partial class UsbPage : ContentPage
{
    public UsbPage(UsbViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}