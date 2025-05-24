using CommunityToolkit.Mvvm.ComponentModel;
using QiMata.MobileIoT.Services.I;
using System.Collections.ObjectModel;

namespace QiMata.MobileIoT.ViewModels;

public partial class BeaconScanViewModel : ObservableObject
{
    readonly IBeaconScanner _scanner;

    public ObservableCollection<BeaconItemViewModel> Devices { get; } = new();

    public BeaconScanViewModel(IBeaconScanner scanner)
    {
        _scanner = scanner;
        _scanner.AdvertisementReceived += OnAdv;
        _scanner.StartScanning();
    }

    void OnAdv(object? s, BeaconAdvertisement adv)
    {
        var existing = Devices.FirstOrDefault(d => d.DeviceId == adv.DeviceId);
        if (existing is null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                Devices.Add(new BeaconItemViewModel(adv)));
        }
        else
        {
            existing.Update(adv);
        }
    }
}

public class BeaconItemViewModel : ObservableObject
{
    public string DeviceId   { get; }
    public string? Name      { get; private set; }
    public int    Rssi       { get; private set; }
    public string DataPreview => BitConverter.ToString(Data.Take(16).ToArray());
    byte[] Data { get; set; }

    public BeaconItemViewModel(BeaconAdvertisement adv)
    {
        DeviceId = adv.DeviceId;
        Update(adv);
    }

    public void Update(BeaconAdvertisement adv)
    {
        Name = adv.Name;
        Rssi = adv.Rssi;
        Data = adv.Data;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Rssi));
        OnPropertyChanged(nameof(DataPreview));
    }
}
