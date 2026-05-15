using QiMata.MobileIoT.ViewModels;

namespace QiMata.MobileIoT.Views;

public partial class AudioPage : ContentPage
{
    public AudioPage(AudioDemoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
