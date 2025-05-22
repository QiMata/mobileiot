namespace QiMata.MobileIoT.Services.I;

public interface IBleDemoService
{
    Task<(double temp, double humidity)> ReadDht22Async();
    Task<bool> ToggleLedAsync();
}
