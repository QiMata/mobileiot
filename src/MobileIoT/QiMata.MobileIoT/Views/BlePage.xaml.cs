namespace QiMata.MobileIoT.Views;

public partial class BlePage : ContentPage
{
    public BlePage(ViewModels.BleViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
