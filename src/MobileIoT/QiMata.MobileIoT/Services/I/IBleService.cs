namespace QiMata.MobileIoT.Services.I;

public interface IBleService
{
    Task<(double temp, double humidity)> ReadDht22Async();
    Task<bool> ToggleLedAsync();
}
