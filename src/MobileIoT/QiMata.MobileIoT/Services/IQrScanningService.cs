namespace QiMata.MobileIoT.Services;

public interface IQrScanningService
{
    Task<string?> ScanAsync();
}
