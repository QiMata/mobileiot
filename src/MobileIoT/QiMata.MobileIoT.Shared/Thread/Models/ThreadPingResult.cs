namespace QiMata.MobileIoT.Shared.Thread.Models;

public sealed class ThreadPingResult
{
    public bool Success { get; set; }
    public string RequestPayload { get; set; } = string.Empty;
    public string ResponsePayload { get; set; } = string.Empty;
    public double RoundTripMs { get; set; }
    public string? Error { get; set; }
}
