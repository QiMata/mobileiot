using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT.ViewModels;

public partial class NfcP2PViewModel : ObservableObject
{
    readonly INfcP2PService _svc;

    public NfcP2PViewModel(INfcP2PService svc) => _svc = svc;

    [RelayCommand]
    void StartP2P() => _svc.StartP2P();

    [RelayCommand]
    void NavigateBack() => Shell.Current.GoToAsync("..");
}
