using QiMata.MobileIoT.Shared.Services;
using QiMata.MobileIoT.Shared.ViewModels;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using QiMata.MobileIoT.Shared.Thread.Services;
using System.Collections.ObjectModel;

namespace QiMata.MobileIoT.ViewModels;

public partial class MainPageViewModel : BaseViewModel
{
    readonly IBeaconScanner _scanner;
    readonly INavigationService _navigation;

    public ObservableCollection<string> BeaconLogs { get; } = new();

    public MainPageViewModel(IBeaconScanner scanner, INavigationService navigation, IAppLogger logger) : base(logger)
    {
        _scanner = scanner;
        _navigation = navigation;
        Subscribe<BeaconAdvertisement>(
            h => _scanner.AdvertisementReceived += h,
            h => _scanner.AdvertisementReceived -= h,
            OnAd);
        _scanner.StartScanning();
    }

    void OnAd(object? s, BeaconAdvertisement e)
    {
        var preview = BitConverter.ToString(e.Data.Take(4).ToArray());
        OnMain(() => BeaconLogs.Add($"{DateTime.Now:T}  RSSI {e.Rssi}  {preview}"));
    }

    [RelayCommand]
    private Task NavigateAsync(string route) => _navigation.NavigateAsync(route);
}
