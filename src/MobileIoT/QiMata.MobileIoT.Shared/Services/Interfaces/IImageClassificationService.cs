namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>Runs on-device ML image classification against a supplied image stream.</summary>
public interface IImageClassificationService
{
    /// <summary>Classifies the image in the given stream and returns the top predicted label.</summary>
    string ClassifyImage(Stream imageStream);
}
