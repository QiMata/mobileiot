namespace QiMata.MobileIoT.Services.Interfaces;

public interface IHceService
{
    bool IsAvailable { get; }
    byte[]? LastSelectedAid { get; }
    bool WasSelected { get; }
    Task StartAsync(byte[] aid, byte[] payload, CancellationToken cancellationToken);
    Task StopAsync();
    event EventHandler<byte[]>? PayloadServed;
}
