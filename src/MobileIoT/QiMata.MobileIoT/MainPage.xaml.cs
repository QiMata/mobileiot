using QiMata.MobileIoT.ViewModels;

namespace QiMata.MobileIoT;

public partial class MainPage : ContentPage
{
    public MainPage(MainPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
