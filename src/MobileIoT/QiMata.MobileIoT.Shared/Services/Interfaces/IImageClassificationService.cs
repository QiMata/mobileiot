namespace QiMata.MobileIoT.Services.Interfaces;

public interface IImageClassificationService
{
    string ClassifyImage(Stream imageStream);
}
