namespace QiMata.MobileIoT;

public class BleService : IBleService
{
    public Task<(double temp, double humidity)> ReadDht22Async()
        => Task.FromResult<(double temp, double humidity)>((23.4, 56.7)); // TODO real BLE read

    private bool _ledState;
    public Task<bool> ToggleLedAsync()
    {
        _ledState = !_ledState;
        // TODO write value over BLE
        return Task.FromResult(_ledState);
    }
}
