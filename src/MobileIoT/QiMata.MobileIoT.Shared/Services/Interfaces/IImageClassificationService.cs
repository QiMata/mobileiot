namespace QiMata.MobileIoT.Shared.Services.Interfaces;

public interface IImageClassificationService
{
    string ClassifyImage(Stream imageStream);
}
