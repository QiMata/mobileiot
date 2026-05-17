namespace QiMata.MobileIoT.Shared.Services;

public interface IQrScanningService
{
    Task<string?> ScanAsync();
}
