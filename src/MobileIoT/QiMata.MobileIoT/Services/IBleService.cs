using System.Threading.Tasks;

namespace QiMata.MobileIoT;

public interface IBleService
{
    Task<(double temp, double humidity)> ReadDht22Async();
    Task<bool> ToggleLedAsync();
}
