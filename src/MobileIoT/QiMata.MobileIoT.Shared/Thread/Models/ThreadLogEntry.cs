namespace QiMata.MobileIoT.Shared.Thread.Models;

public sealed class ThreadLogEntry
{
    public DateTime TimestampUtc { get; set; }
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;

    public override string ToString() =>
        $"[{TimestampUtc:HH:mm:ss}] {Level}: {Message}";
}
