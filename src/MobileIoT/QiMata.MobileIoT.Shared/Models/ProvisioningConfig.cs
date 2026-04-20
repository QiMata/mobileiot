using System.Text.Json.Serialization;

namespace QiMata.MobileIoT.Models;

public class ProvisioningConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "provision";

    [JsonPropertyName("wifi_ssid")]
    public string WifiSsid { get; set; } = string.Empty;

    [JsonPropertyName("wifi_pass")]
    public string WifiPass { get; set; } = string.Empty;

    [JsonPropertyName("wifi_security")]
    public string WifiSecurity { get; set; } = "WPA2";

    [JsonPropertyName("device_name")]
    public string DeviceName { get; set; } = string.Empty;

    [JsonPropertyName("mqtt_broker")]
    public string MqttBroker { get; set; } = string.Empty;

    [JsonPropertyName("mqtt_port")]
    public int MqttPort { get; set; } = 1883;
}
