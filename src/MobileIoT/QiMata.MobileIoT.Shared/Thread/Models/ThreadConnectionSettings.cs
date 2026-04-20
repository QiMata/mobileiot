namespace QiMata.MobileIoT.ThreadDemoCore.Models;

public sealed class ThreadConnectionSettings
{
    public bool UseLiveBridge { get; set; }
    public string BridgeBaseUrl { get; set; } = "http://raspberrypi.local:8080";
    public string TargetNode { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 3000;
}
