using CommunityToolkit.Mvvm.ComponentModel;
using QiMata.MobileIoT.Services.Interfaces;
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
    private const string KnownPiUuid = "12345678-1234-1234-1234-1234567890AB";

    public string DeviceId   { get; }
    public string? Name      { get; private set; }
    public int    Rssi       { get; private set; }
    public string DataPreview => BitConverter.ToString(Data.Take(16).ToArray());
    byte[] Data { get; set; }

    // Telemetry fields
    public string? BeaconUuid { get; private set; }
    public double? DecodedTemp { get; private set; }
    public double? DecodedHumidity { get; private set; }
    public bool IsTelemetryBeacon { get; private set; }

    public BeaconItemViewModel(BeaconAdvertisement adv)
    {
        DeviceId = adv.DeviceId;
        Data = Array.Empty<byte>();
        Update(adv);
    }

    public void Update(BeaconAdvertisement adv)
    {
        Name = adv.Name;
        Rssi = adv.Rssi;
        Data = adv.Data;
        TryParseIBeacon(adv.Data);
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Rssi));
        OnPropertyChanged(nameof(DataPreview));
        OnPropertyChanged(nameof(BeaconUuid));
        OnPropertyChanged(nameof(DecodedTemp));
        OnPropertyChanged(nameof(DecodedHumidity));
        OnPropertyChanged(nameof(IsTelemetryBeacon));
    }

    private void TryParseIBeacon(byte[] scanRecord)
    {
        IsTelemetryBeacon = false;
        BeaconUuid = null;
        DecodedTemp = null;
        DecodedHumidity = null;

        if (scanRecord is null || scanRecord.Length < 2)
            return;

        // Iterate AD structures in the scan record
        int i = 0;
        while (i < scanRecord.Length - 1)
        {
            int length = scanRecord[i];
            if (length == 0 || i + length >= scanRecord.Length)
                break;

            int type = scanRecord[i + 1];

            // Type 0xFF = Manufacturer Specific Data
            if (type == 0xFF && length >= 26)
            {
                int dataStart = i + 2;
                int dataLen = length - 1;

                // Check Apple company ID (0x4C 0x00, little-endian)
                if (dataLen >= 25 &&
                    scanRecord[dataStart] == 0x4C &&
                    scanRecord[dataStart + 1] == 0x00)
                {
                    // Check iBeacon prefix (0x02 0x15)
                    if (scanRecord[dataStart + 2] == 0x02 &&
                        scanRecord[dataStart + 3] == 0x15)
                    {
                        // Extract UUID (16 bytes at offset +4)
                        var uuidBytes = new byte[16];
                        Array.Copy(scanRecord, dataStart + 4, uuidBytes, 0, 16);
                        var uuid = new Guid(
                            (uuidBytes[0] << 24) | (uuidBytes[1] << 16) | (uuidBytes[2] << 8) | uuidBytes[3],
                            (short)((uuidBytes[4] << 8) | uuidBytes[5]),
                            (short)((uuidBytes[6] << 8) | uuidBytes[7]),
                            uuidBytes[8], uuidBytes[9], uuidBytes[10], uuidBytes[11],
                            uuidBytes[12], uuidBytes[13], uuidBytes[14], uuidBytes[15]);

                        BeaconUuid = uuid.ToString().ToUpperInvariant();

                        // Extract major (2 bytes big-endian at offset +20)
                        short major = (short)((scanRecord[dataStart + 20] << 8) | scanRecord[dataStart + 21]);
                        // Extract minor (2 bytes big-endian at offset +22)
                        ushort minor = (ushort)((scanRecord[dataStart + 22] << 8) | scanRecord[dataStart + 23]);

                        // Check if this is our known Pi telemetry beacon
                        if (string.Equals(BeaconUuid, KnownPiUuid, StringComparison.OrdinalIgnoreCase))
                        {
                            IsTelemetryBeacon = true;
                            DecodedTemp = major / 100.0;
                            DecodedHumidity = minor / 100.0;
                        }
                    }
                }
            }

            i += length + 1;
        }
    }
}
