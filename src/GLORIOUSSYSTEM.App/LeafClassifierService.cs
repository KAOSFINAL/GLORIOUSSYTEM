using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Maui.Storage;
using SkiaSharp;

namespace GLORIOUSSYSTEM.App;

public class LeafPrediction
{
    public bool IsLettuce { get; set; }
    public float LettuceConfidence { get; set; }
    public Dictionary<string, float> LettuceScores { get; set; } = new();
    public string VarietyLabel { get; set; } = "unknown";
    public float VarietyConfidence { get; set; }
    public Dictionary<string, float> VarietyScores { get; set; } = new();
    public string HealthLabel { get; set; } = "";
    public float HealthConfidence { get; set; }
    public Dictionary<string, float> HealthScores { get; set; } = new();

    public string Label => IsLettuce ? HealthLabel : "non_lettuce";
    public float Confidence => IsLettuce ? HealthConfidence : LettuceConfidence;
    public Dictionary<string, float> AllScores => IsLettuce ? HealthScores : LettuceScores;
}

public sealed class LeafClassifierService : IDisposable
{
    const string MultiTaskModel = "leaf_model_multitask.onnx";
    const string GreenIceConfirmationModel = "leaf_model_green_ice_v2.onnx";
    const string LegacyModel = "leaf_model.onnx";

    static readonly string[] LettuceClasses = { "non_lettuce", "lettuce" };
    static readonly string[] VarietyClasses = { "green_ice", "not_green_ice" };
    static readonly string[] HealthClasses = { "deficient", "diseased", "healthy" };
    static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
    static readonly float[] Std = { 0.229f, 0.224f, 0.225f };

    readonly InferenceSession _session;
    readonly InferenceSession? _confirmationSession;
    readonly bool _isMultiTask;
    readonly bool _hasVariety;

    public string ModelKind => _confirmationSession != null
        ? "GREEN ICE CONSENSUS + HEALTH"
        : _hasVariety ? "VARIETY + HEALTH" : _isMultiTask ? "MULTITASK" : "HEALTH";

    LeafClassifierService(string modelPath, bool isMultiTask, string? confirmationModelPath)
    {
        _isMultiTask = isMultiTask;
        // MAUI hosts can be sensitive to ONNX Runtime's default parallel
        // memory arenas. Use a conservative sequential CPU session so a photo
        // scan cannot take down the desktop/mobile process.
        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_BASIC,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            EnableCpuMemArena = false,
            EnableMemoryPattern = false
        };
        _session = new InferenceSession(modelPath, sessionOptions);
        _hasVariety = _session.OutputMetadata.ContainsKey("variety_logits");

        if (confirmationModelPath != null)
        {
            _confirmationSession = new InferenceSession(confirmationModelPath);
            if (!_confirmationSession.OutputMetadata.ContainsKey("variety_logits"))
                throw new InvalidDataException("The Green Ice confirmation model has no 'variety_logits' output.");
        }
    }

    public static async Task<LeafClassifierService> CreateAsync(CancellationToken cancellationToken = default)
    {
        var modelPath = await StagePackagedModelAsync(MultiTaskModel, cancellationToken);
        var isMultiTask = modelPath != null;

        modelPath ??= await StagePackagedModelAsync(LegacyModel, cancellationToken);
        if (modelPath == null)
            throw new FileNotFoundException("No packaged lettuce model was found. Reinstall the application and try again.");

        // Keep the primary multitask model as the single live inference path.
        // The optional confirmation model invokes a second native ONNX session
        // immediately after the first one and can terminate the host process on
        // some desktop/mobile native runtimes. The primary model already emits
        // variety logits, so disabling this optional pass preserves scan results
        // while keeping photo analysis stable.
        var confirmationModelPath = (string?)null;

        return await Task.Run(
            () => new LeafClassifierService(modelPath, isMultiTask, confirmationModelPath),
            cancellationToken);
    }

    static async Task<string?> StagePackagedModelAsync(string fileName, CancellationToken cancellationToken)
    {
        var modelDirectory = Path.Combine(FileSystem.AppDataDirectory, "Models");
        Directory.CreateDirectory(modelDirectory);

        var destination = Path.Combine(modelDirectory, fileName);
        if (!await TryCopyPackagedFileAsync($"Models/{fileName}", destination, cancellationToken))
            return null;

        // PyTorch exported these models with external tensor data. ONNX Runtime
        // resolves that sidecar relative to the model, so stage both together.
        var dataName = fileName + ".data";
        var dataDestination = Path.Combine(modelDirectory, dataName);
        await TryCopyPackagedFileAsync($"Models/{dataName}", dataDestination, cancellationToken);

        return destination;
    }

    static async Task<bool> TryCopyPackagedFileAsync(
        string packagePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var source = await FileSystem.OpenAppPackageFileAsync(packagePath);

            if (source.CanSeek && File.Exists(destinationPath) && new FileInfo(destinationPath).Length == source.Length)
                return true;

            var temporaryPath = destinationPath + ".tmp";
            await using (var destination = File.Create(temporaryPath))
                await source.CopyToAsync(destination, cancellationToken);

            File.Move(temporaryPath, destinationPath, true);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    public LeafPrediction Classify(string imagePath)
    {
        using var original = SKBitmap.Decode(imagePath)
            ?? throw new InvalidDataException("The selected image format could not be decoded.");
        using var resized = original.Resize(
            new SKImageInfo(224, 224),
            new SKSamplingOptions(SKFilterMode.Linear))
            ?? throw new InvalidDataException("The selected image could not be resized.");

        var input = new DenseTensor<float>(new[] { 1, 3, 224, 224 });
        for (var y = 0; y < 224; y++)
        {
            for (var x = 0; x < 224; x++)
            {
                var pixel = resized.GetPixel(x, y);
                input[0, 0, y, x] = (pixel.Red / 255f - Mean[0]) / Std[0];
                input[0, 1, y, x] = (pixel.Green / 255f - Mean[1]) / Std[1];
                input[0, 2, y, x] = (pixel.Blue / 255f - Mean[2]) / Std[2];
            }
        }

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", input) };
        using var results = _session.Run(inputs);

        var prediction = _isMultiTask && results.Count >= 2
            ? ProcessMultiTaskOutput(results)
            : ProcessLegacyOutput(results);

        if (_confirmationSession != null && prediction.IsLettuce && prediction.VarietyScores.Count > 0)
        {
            using var confirmationResults = _confirmationSession.Run(inputs);
            ApplyVarietyConsensus(prediction, confirmationResults);
        }

        return prediction;
    }

    static void ApplyVarietyConsensus(
        LeafPrediction prediction,
        IReadOnlyList<DisposableNamedOnnxValue> confirmationResults)
    {
        var resultMap = confirmationResults.ToDictionary(result => result.Name, StringComparer.OrdinalIgnoreCase);
        if (!resultMap.TryGetValue("variety_logits", out var varietyResult))
            throw new InvalidDataException("The Green Ice confirmation model returned no 'variety_logits' output.");

        var confirmationLogits = varietyResult.AsEnumerable<float>().ToArray();
        if (confirmationLogits.Length != VarietyClasses.Length)
            throw new InvalidDataException(
                $"Confirmation output 'variety_logits' returned {confirmationLogits.Length} values; expected {VarietyClasses.Length}.");

        if (!prediction.VarietyScores.TryGetValue("green_ice", out var primaryGreenProbability))
            throw new InvalidDataException("The primary model returned no Green Ice probability.");

        // A Green Ice result is allowed only when both independently trained
        // models put it above 50%. The minimum probability is a conservative,
        // normalized consensus score; either model can veto a false positive.
        var confirmationGreenProbability = Softmax(confirmationLogits)[0];
        var consensusGreenProbability = MathF.Min(primaryGreenProbability, confirmationGreenProbability);
        var consensusProbabilities = new[] { consensusGreenProbability, 1f - consensusGreenProbability };
        var consensusIndex = IndexOfMaximum(consensusProbabilities);

        prediction.VarietyLabel = VarietyClasses[consensusIndex];
        prediction.VarietyConfidence = consensusProbabilities[consensusIndex];
        prediction.VarietyScores = ToScores(VarietyClasses, consensusProbabilities);
    }

    static LeafPrediction ProcessMultiTaskOutput(IReadOnlyList<DisposableNamedOnnxValue> results)
    {
        var resultMap = results.ToDictionary(result => result.Name, StringComparer.OrdinalIgnoreCase);
        var lettuceLogits = ReadOutput(resultMap, "lettuce_logits", results[0], LettuceClasses.Length);
        var healthFallback = results.Count >= 3 && resultMap.ContainsKey("variety_logits")
            ? results[2]
            : results[1];
        var healthLogits = ReadOutput(resultMap, "health_logits", healthFallback, HealthClasses.Length);

        var lettuceProbs = Softmax(lettuceLogits);
        var lettuceMaxIndex = IndexOfMaximum(lettuceProbs);
        var prediction = new LeafPrediction
        {
            IsLettuce = lettuceMaxIndex == 1,
            LettuceConfidence = lettuceProbs[lettuceMaxIndex],
            LettuceScores = ToScores(LettuceClasses, lettuceProbs)
        };

        if (!prediction.IsLettuce)
            return prediction;

        if (resultMap.TryGetValue("variety_logits", out var varietyResult))
        {
            var varietyLogits = varietyResult.AsEnumerable<float>().ToArray();
            if (varietyLogits.Length != VarietyClasses.Length)
                throw new InvalidDataException(
                    $"Model output 'variety_logits' returned {varietyLogits.Length} values; expected {VarietyClasses.Length}.");

            var varietyProbs = Softmax(varietyLogits);
            var varietyMaxIndex = IndexOfMaximum(varietyProbs);
            prediction.VarietyLabel = VarietyClasses[varietyMaxIndex];
            prediction.VarietyConfidence = varietyProbs[varietyMaxIndex];
            prediction.VarietyScores = ToScores(VarietyClasses, varietyProbs);
        }

        var healthProbs = Softmax(healthLogits);
        var healthMaxIndex = IndexOfMaximum(healthProbs);
        prediction.HealthLabel = HealthClasses[healthMaxIndex];
        prediction.HealthConfidence = healthProbs[healthMaxIndex];
        prediction.HealthScores = ToScores(HealthClasses, healthProbs);

        return prediction;
    }

    static LeafPrediction ProcessLegacyOutput(IReadOnlyList<DisposableNamedOnnxValue> results)
    {
        if (results.Count == 0)
            throw new InvalidDataException("The health model returned no output.");

        var probabilities = Softmax(results[0].AsEnumerable<float>().ToArray());
        if (probabilities.Length != HealthClasses.Length)
            throw new InvalidDataException($"The health model returned {probabilities.Length} values; expected {HealthClasses.Length}.");

        var maxIndex = IndexOfMaximum(probabilities);
        return new LeafPrediction
        {
            // The legacy model only accepts cropped lettuce images. It cannot
            // perform the separate non-lettuce or age tasks.
            IsLettuce = true,
            LettuceConfidence = 1f,
            LettuceScores = new Dictionary<string, float> { ["lettuce"] = 1f },
            HealthLabel = HealthClasses[maxIndex],
            HealthConfidence = probabilities[maxIndex],
            HealthScores = ToScores(HealthClasses, probabilities)
        };
    }

    static float[] ReadOutput(
        IReadOnlyDictionary<string, DisposableNamedOnnxValue> results,
        string name,
        DisposableNamedOnnxValue fallback,
        int expectedLength)
    {
        var values = (results.TryGetValue(name, out var result) ? result : fallback)
            .AsEnumerable<float>()
            .ToArray();

        if (values.Length != expectedLength)
            throw new InvalidDataException($"Model output '{name}' returned {values.Length} values; expected {expectedLength}.");

        return values;
    }

    static float[] Softmax(IReadOnlyList<float> logits)
    {
        if (logits.Count == 0)
            throw new InvalidDataException("The model returned an empty output.");

        var maximum = logits.Max();
        var exponentials = logits.Select(value => MathF.Exp(value - maximum)).ToArray();
        var total = exponentials.Sum();
        if (!float.IsFinite(total) || total <= 0)
            throw new InvalidDataException("The model returned invalid confidence values.");

        return exponentials.Select(value => value / total).ToArray();
    }

    static int IndexOfMaximum(IReadOnlyList<float> values)
    {
        var maximumIndex = 0;
        for (var index = 1; index < values.Count; index++)
        {
            if (values[index] > values[maximumIndex])
                maximumIndex = index;
        }

        return maximumIndex;
    }

    static Dictionary<string, float> ToScores(IReadOnlyList<string> classes, IReadOnlyList<float> probabilities)
        => classes.Select((label, index) => new { label, value = probabilities[index] })
            .ToDictionary(item => item.label, item => item.value);

    public void Dispose()
    {
        _confirmationSession?.Dispose();
        _session.Dispose();
    }
}
