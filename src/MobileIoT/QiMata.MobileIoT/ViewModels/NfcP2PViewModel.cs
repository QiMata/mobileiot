using QiMata.MobileIoT.Shared.Services;
using QiMata.MobileIoT.Shared.ViewModels;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Shared.Services.Interfaces;

namespace QiMata.MobileIoT.ViewModels;

public partial class NfcP2PViewModel : BaseViewModel
{
    readonly INfcP2PService _svc;

    public NfcP2PViewModel(INfcP2PService svc, IAppLogger logger) : base(logger)
    {
        _svc = svc;
    }

    [RelayCommand]
    void StartP2P() => _svc.StartP2P("Hello World");

    [RelayCommand]
    void NavigateBack() => Shell.Current.GoToAsync("..");
}
