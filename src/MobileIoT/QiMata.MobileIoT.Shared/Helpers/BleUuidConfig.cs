using System;
using System.Reflection;
using System.Text.Json;

namespace QiMata.MobileIoT.Shared.Helpers;

public static class BleUuidConfig
{
    public static BleUuidValues Values { get; } = Load();

    private static BleUuidValues Load()
    {
        var assembly = typeof(BleUuidConfig).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream("BleConstants.json")
            ?? throw new InvalidOperationException("BLE constants resource missing");

        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        return new BleUuidValues(
            Guid.Parse(root.GetProperty("serviceUuid").GetString() ?? throw new InvalidOperationException("Missing service UUID")),
            Guid.Parse(root.GetProperty("temperatureCharacteristicUuid").GetString() ?? throw new InvalidOperationException("Missing temperature characteristic UUID")),
            Guid.Parse(root.GetProperty("humidityCharacteristicUuid").GetString() ?? throw new InvalidOperationException("Missing humidity characteristic UUID")),
            Guid.Parse(root.GetProperty("ledCharacteristicUuid").GetString() ?? throw new InvalidOperationException("Missing LED characteristic UUID"))
        );
    }

    public sealed record BleUuidValues(Guid ServiceUuid, Guid TemperatureCharacteristicUuid, Guid HumidityCharacteristicUuid, Guid LedCharacteristicUuid);
}
