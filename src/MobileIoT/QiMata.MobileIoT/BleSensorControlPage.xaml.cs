namespace QiMata.MobileIoT;

public partial class BleSensorControlPage : ContentPage
{
    public BleSensorControlPage(BleSensorControlViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
