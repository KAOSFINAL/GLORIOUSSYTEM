using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace GLORIOUSSYSTEM.App;

public class LeafPrediction
{
    // Lettuce detection
    public bool IsLettuce { get; set; }
    public float LettuceConfidence { get; set; }
    public Dictionary<string, float> LettuceScores { get; set; } = new();

    // Health classification (only valid if IsLettuce)
    public string HealthLabel { get; set; } = "";
    public float HealthConfidence { get; set; }
    public Dictionary<string, float> HealthScores { get; set; } = new();

    // Age classification (only valid if IsLettuce)
    public string AgeLabel { get; set; } = "";
    public float AgeConfidence { get; set; }
    public Dictionary<string, float> AgeScores { get; set; } = new();

    // Backward compatibility
    public string Label => IsLettuce ? HealthLabel : "non_lettuce";
    public float Confidence => IsLettuce ? HealthConfidence : LettuceConfidence;
    public Dictionary<string, float> AllScores => IsLettuce ? HealthScores : LettuceScores;
}

public class LeafClassifierService
{
    static readonly string[] LettuceClasses = { "non_lettuce", "lettuce" };
    static readonly string[] HealthClasses = { "deficient", "diseased", "healthy" };
    static readonly string[] AgeClasses = { "seedling", "vegetative", "mature", "harvest_ready" };
    static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
    static readonly float[] Std = { 0.229f, 0.224f, 0.225f };

    readonly InferenceSession _session;

    public LeafClassifierService()
    {
        var modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "leaf_model_multitask.onnx");
        if (!File.Exists(modelPath))
        {
            // Fallback to old model
            modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "leaf_model.onnx");
        }
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

        // Multi-task model has 3 outputs: lettuce_logits, health_logits, age_logits
        // Old model has 1 output
        if (results.Count >= 3)
        {
            return ProcessMultiTaskOutput(results);
        }
        else
        {
            return ProcessLegacyOutput(results);
        }
    }

    LeafPrediction ProcessMultiTaskOutput(IReadOnlyList<DisposableNamedOnnxValue> results)
    {
        var lettuceLogits = results[0].AsEnumerable<float>().ToArray();
        var healthLogits = results[1].AsEnumerable<float>().ToArray();
        var ageLogits = results[2].AsEnumerable<float>().ToArray();

        // Softmax for lettuce detection
        var lettuceExp = lettuceLogits.Select(MathF.Exp).ToArray();
        var lettuceSumExp = lettuceExp.Sum();
        var lettuceProbs = lettuceExp.Select(e => e / lettuceSumExp).ToArray();

        var lettuceMaxIndex = Array.IndexOf(lettuceProbs, lettuceProbs.Max());
        var isLettuce = lettuceMaxIndex == 1; // 1 = lettuce, 0 = non_lettuce

        var prediction = new LeafPrediction
        {
            IsLettuce = isLettuce,
            LettuceConfidence = lettuceProbs[lettuceMaxIndex],
            LettuceScores = LettuceClasses.Zip(lettuceProbs, (c, p) => (c, p)).ToDictionary(x => x.c, x => x.p)
        };

        if (isLettuce)
        {
            // Health classification
            var healthExp = healthLogits.Select(MathF.Exp).ToArray();
            var healthSumExp = healthExp.Sum();
            var healthProbs = healthExp.Select(e => e / healthSumExp).ToArray();

            var healthMaxIndex = Array.IndexOf(healthProbs, healthProbs.Max());
            prediction.HealthLabel = HealthClasses[healthMaxIndex];
            prediction.HealthConfidence = healthProbs[healthMaxIndex];
            prediction.HealthScores = HealthClasses.Zip(healthProbs, (c, p) => (c, p)).ToDictionary(x => x.c, x => x.p);

            // Age classification
            var ageExp = ageLogits.Select(MathF.Exp).ToArray();
            var ageSumExp = ageExp.Sum();
            var ageProbs = ageExp.Select(e => e / ageSumExp).ToArray();

            var ageMaxIndex = Array.IndexOf(ageProbs, ageProbs.Max());
            prediction.AgeLabel = AgeClasses[ageMaxIndex];
            prediction.AgeConfidence = ageProbs[ageMaxIndex];
            prediction.AgeScores = AgeClasses.Zip(ageProbs, (c, p) => (c, p)).ToDictionary(x => x.c, x => x.p);
        }
        else
        {
            // Not lettuce - set defaults for health/age
            prediction.HealthLabel = "N/A";
            prediction.HealthConfidence = 0;
            prediction.HealthScores = HealthClasses.ToDictionary(c => c, c => 0f);
            prediction.AgeLabel = "N/A";
            prediction.AgeConfidence = 0;
            prediction.AgeScores = AgeClasses.ToDictionary(c => c, c => 0f);
        }

        return prediction;
    }

    LeafPrediction ProcessLegacyOutput(IReadOnlyList<DisposableNamedOnnxValue> results)
    {
        var scores = results.First().AsEnumerable<float>().ToArray();

        var expScores = scores.Select(MathF.Exp).ToArray();
        var sumExp = expScores.Sum();
        var probabilities = expScores.Select(e => e / sumExp).ToArray();

        var maxIndex = Array.IndexOf(probabilities, probabilities.Max());

        // Legacy model only does health classification
        // Treat as lettuce with unknown age
        return new LeafPrediction
        {
            IsLettuce = true,
            LettuceConfidence = 1.0f,
            LettuceScores = LettuceClasses.ToDictionary(c => c, c => c == "lettuce" ? 1.0f : 0f),
            HealthLabel = HealthClasses[maxIndex],
            HealthConfidence = probabilities[maxIndex],
            HealthScores = HealthClasses.Zip(probabilities, (c, p) => (c, p)).ToDictionary(x => x.c, x => x.p),
            AgeLabel = "unknown",
            AgeConfidence = 0,
            AgeScores = AgeClasses.ToDictionary(c => c, c => 0f)
        };
    }
}