using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using System.Reflection;

namespace QiMata.MobileIoT.Services;

public class ImageClassificationService
{
    private InferenceSession _session;
    private readonly string[] _labels;

    public ImageClassificationService()
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var modelStream = assembly.GetManifestResourceStream("QiMata.MobileIoT.Resources.Models.mobilenetv2.onnx");
        using var ms = new MemoryStream();
        modelStream?.CopyTo(ms);
        _session = new InferenceSession(ms.ToArray());

        using var labelStream = assembly.GetManifestResourceStream("QiMata.MobileIoT.Resources.Models.imagenet_labels.txt");
        using var reader = new StreamReader(labelStream!);
        _labels = reader.ReadToEnd().Split('\n');
    }

    public string ClassifyImage(Stream imageStream)
    {
        using var bitmap = SKBitmap.Decode(imageStream);
        using var resized = bitmap.Resize(new SKImageInfo(224, 224), SKFilterQuality.Medium);

        float[] input = Preprocess(resized);

        var inputTensor = new DenseTensor<float>(input, new[] { 1, 3, 224, 224 });
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsEnumerable<float>().ToArray();

        int maxIdx = Array.IndexOf(output, output.Max());
        string label = _labels[maxIdx].Trim();

        return label;
    }

    private float[] Preprocess(SKBitmap bitmap)
    {
        var mean = new float[] { 0.485f, 0.456f, 0.406f };
        var std = new float[] { 0.229f, 0.224f, 0.225f };

        float[] result = new float[3 * 224 * 224];
        int idx = 0;

        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 224; x++)
            {
                var color = bitmap.GetPixel(x, y);
                result[idx++] = ((color.Red / 255f) - mean[0]) / std[0];
                result[idx++] = ((color.Green / 255f) - mean[1]) / std[1];
                result[idx++] = ((color.Blue / 255f) - mean[2]) / std[2];
            }
        }

        return result;
    }
}
