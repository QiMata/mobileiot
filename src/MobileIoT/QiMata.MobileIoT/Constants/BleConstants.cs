namespace QiMata.MobileIoT.Constants;

public static class BleConstants
{
    public static readonly Guid ServiceUuid = new("12345678-1234-1234-1234-1234567890AB");

    public static readonly Guid TemperatureCharacteristic = new("00002A6E-0000-1000-8000-00805F9B34FB");
    public static readonly Guid HumidityCharacteristic = new("00002A6F-0000-1000-8000-00805F9B34FB");
    public static readonly Guid LedCharacteristic = new("12345679-1234-1234-1234-1234567890AB");

    public static readonly TimeSpan DefaultScanTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan AdvertiseStartTimeoutAndroid = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan AdvertiseStartTimeoutIos = TimeSpan.FromSeconds(8);

    public static readonly byte[] Ping = "PING"u8.ToArray();
}
