namespace QiMata.MobileIoT.Uno;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
    }

    private async void OnNavigate(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string route } && App.NavigationService is not null)
        {
            await App.NavigationService.NavigateAsync(route);
        }
    }
}
