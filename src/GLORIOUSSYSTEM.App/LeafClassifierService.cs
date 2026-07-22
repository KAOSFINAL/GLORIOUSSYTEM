using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace GLORIOUSSYSTEM.App;

public class LeafPrediction
{
    public string Label { get; set; } = "";
    public float Confidence { get; set; }
    public Dictionary<string, float> AllScores { get; set; } = new();
}

public class LeafClassifierService
{
    static readonly string[] Classes = { "deficient", "diseased", "healthy" };
    static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
    static readonly float[] Std = { 0.229f, 0.224f, 0.225f };

    readonly InferenceSession _session;

    public LeafClassifierService()
    {
        var modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "leaf_model.onnx");
        _session = new InferenceSession(modelPath);
    }

    public LeafPrediction Classify(string imagePath)
    {
        using var original = SKBitmap.Decode(imagePath);
        using var resized = original.Resize(new SKImageInfo(224, 224), SKFilterQuality.Medium);

        var input = new DenseTensor<float>(new[] { 1, 3, 224, 224 });

        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 224; x++)
            {
                var pixel = resized.GetPixel(x, y);
                input[0, 0, y, x] = (pixel.Red / 255f - Mean[0]) / Std[0];
                input[0, 1, y, x] = (pixel.Green / 255f - Mean[1]) / Std[1];
                input[0, 2, y, x] = (pixel.Blue / 255f - Mean[2]) / Std[2];
            }
        }

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", input) };
        using var results = _session.Run(inputs);
        var scores = results.First().AsEnumerable<float>().ToArray();

        var expScores = scores.Select(MathF.Exp).ToArray();
        var sumExp = expScores.Sum();
        var probabilities = expScores.Select(e => e / sumExp).ToArray();

        var maxIndex = Array.IndexOf(probabilities, probabilities.Max());

        return new LeafPrediction
        {
            Label = Classes[maxIndex],
            Confidence = probabilities[maxIndex],
            AllScores = Classes.Zip(probabilities, (c, p) => (c, p)).ToDictionary(x => x.c, x => x.p)
        };
    }
}