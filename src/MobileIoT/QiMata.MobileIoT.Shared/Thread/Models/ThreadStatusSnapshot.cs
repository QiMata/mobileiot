namespace QiMata.MobileIoT.Shared.Thread.Models;

public sealed class ThreadStatusSnapshot
{
    public string Role { get; set; } = string.Empty;
    public string DatasetHex { get; set; } = string.Empty;
    public List<string> MeshLocalAddresses { get; set; } = [];
    public string Rloc16 { get; set; } = string.Empty;
    public DateTime LastUpdatedUtc { get; set; }
    public string SourceMode { get; set; } = string.Empty;
}
