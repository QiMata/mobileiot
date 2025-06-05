namespace QiMata.MobileIoT.Views;

public partial class VisionPage : ContentPage
{
    public VisionPage(ViewModels.VisionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
