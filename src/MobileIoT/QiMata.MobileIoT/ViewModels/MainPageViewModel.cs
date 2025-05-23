using CommunityToolkit.Mvvm.ComponentModel;
using System;

using Microsoft.Maui;
using Microsoft.Maui.Dispatching;
using System.Collections.ObjectModel;
using System.Linq;
using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT.ViewModels;

public class MainPageViewModel : ObservableObject
{
    readonly IBeaconScanner _scanner;

    public ObservableCollection<string> BeaconLogs { get; } = new();

    public MainPageViewModel(IBeaconScanner scanner)
    {
        _scanner = scanner;
        _scanner.AdvertisementReceived += OnAd;
        _scanner.StartScanning();
    }

    void OnAd(object? s, BeaconAdvertisement e)
    {
        var preview = BitConverter.ToString(e.Data.Take(4).ToArray());
        MainThread.BeginInvokeOnMainThread(() =>
            BeaconLogs.Add($"{DateTime.Now:T}  RSSI {e.Rssi}  {preview}"));
    }
}
