using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace QiMata.MobileIoT;

public partial class MainPageViewModel : ObservableObject
{
    [RelayCommand]
    private async Task Navigate(string target)
    {
        await OnNavigate(target);
    }

    private async Task OnNavigate(string target)
    {
        switch (target)
        {
            case "BleSensorControlPage":
                await Shell.Current.GoToAsync(nameof(BleSensorControlPage));
                break;
            default:
                await Shell.Current.GoToAsync(target);
                break;
        }
    }
}
