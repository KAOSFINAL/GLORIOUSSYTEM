using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GLORIOUSSYSTEM.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

public class PredictionItem : INotifyPropertyChanged
{
    string _label = "";
    string _icon = "";
    float _confidence;
    Color _color = Colors.Gray;

    public string Label { get => _label; set { _label = value; OnPropertyChanged(); } }
    public string Icon { get => _icon; set { _icon = value; OnPropertyChanged(); } }
    public float Confidence { get => _confidence; set { _confidence = value; OnPropertyChanged(); } }
    public Color Color { get => _color; set { _color = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class WebcamPage : ContentPage
{
    LeafClassifierService? _classifier;
    string? _initError;
    bool _isClassifying;
    Task? _classifierInitialization;
    bool? _isWideLayout;
    LeafPrediction? _lastPrediction;
    string? _currentImagePath;

    public WebcamPage()
    {
        InitializeComponent();
        // The shell keeps this page alive, so start staging ONNX assets in the
        // background during app startup instead of on the first Webcam tap.
        _classifierInitialization = InitializeClassifierAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _classifierInitialization ??= InitializeClassifierAsync();
    }

    void OnWorkspaceSizeChanged(object? sender, EventArgs e)
    {
        var isWide = WorkspaceGrid.Width >= 820;
        if (!isWide)
            PreviewContainer.HeightRequest = Math.Clamp(WorkspaceGrid.Width * 0.78, 280, 420);
        if (_isWideLayout == isWide) return;
        _isWideLayout = isWide;

        WorkspaceGrid.ColumnDefinitions.Clear();
        WorkspaceGrid.RowDefinitions.Clear();

        if (isWide)
        {
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.65, GridUnitType.Star) });
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(ResultsColumn, 1);
            Grid.SetRow(ResultsColumn, 0);
            WorkspaceGrid.ColumnSpacing = 20;
            WorkspaceGrid.RowSpacing = 0;
            PreviewContainer.HeightRequest = 470;
        }
        else
        {
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(ResultsColumn, 0);
            Grid.SetRow(ResultsColumn, 1);
            WorkspaceGrid.ColumnSpacing = 0;
            WorkspaceGrid.RowSpacing = 20;
        }
    }

    async Task InitializeClassifierAsync()
    {
        ModelStatusLabel.Text = "PREPARING";
        try
        {
            _classifier = await LeafClassifierService.CreateAsync();
            ModelStatusLabel.Text = $"READY - {_classifier.ModelKind}";
            ModelStatusLabel.TextColor = GetResourceColor("OnPrimaryContainer", Colors.White);
        }
        catch (Exception ex)
        {
            _initError = ex.Message;
            ModelStatusLabel.Text = "UNAVAILABLE";
            ModelStatusLabel.TextColor = GetResourceColor("Error", Colors.Red);
        }
    }

    async void OnChoosePhotoClicked(object sender, EventArgs e)
    {
        try
        {
            ScanErrorLabel.IsVisible = false;
            if (!await EnsureClassifierAsync()) return;

            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Select a lettuce photo"
            });

            if (result == null) return;
            await ClassifyImage(await StageImageAsync(result));
        }
        catch (Exception ex)
        {
            await ReportScanErrorAsync("Photo Error", ex);
        }
    }

    async void OnTakePhotoClicked(object sender, EventArgs e)
    {
        try
        {
            ScanErrorLabel.IsVisible = false;
            if (!await EnsureClassifierAsync()) return;

            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await ReportScanErrorAsync("Camera Unavailable", new NotSupportedException("Camera capture is not supported on this device."));
                return;
            }

            var result = await MediaPicker.Default.CapturePhotoAsync();
            if (result == null) return;
            await ClassifyImage(await StageImageAsync(result));
        }
        catch (PermissionException)
        {
            await ReportScanErrorAsync("Camera Permission", new UnauthorizedAccessException("Camera access was denied. Allow camera access for GLORIOUSSYSTEM and try again."));
        }
        catch (FeatureNotSupportedException)
        {
            await ReportScanErrorAsync("Camera Unavailable", new NotSupportedException("This device does not support photo capture."));
        }
        catch (Exception ex)
        {
            await ReportScanErrorAsync("Camera Error", ex);
        }
    }

    async Task<bool> EnsureClassifierAsync()
    {
        try
        {
            _classifierInitialization ??= InitializeClassifierAsync();
            await _classifierInitialization;
            if (_classifier != null) return true;

            await ReportScanErrorAsync("AI model unavailable",
                new InvalidOperationException(_initError ?? "The crop model could not be loaded."));
            return false;
        }
        catch (Exception ex)
        {
            await ReportScanErrorAsync("AI model unavailable", ex);
            return false;
        }
    }

    async Task ClassifyImage(string imagePath)
    {
        if (_isClassifying || _classifier == null || string.IsNullOrWhiteSpace(imagePath)) return;
        if (!File.Exists(imagePath))
        {
            await DisplayAlertAsync("Image Error", "The selected image could not be opened.", "OK");
            return;
        }

        DeleteTemporaryImage();
        _currentImagePath = imagePath;
        _isClassifying = true;
        SetBusyState(true);

        try
        {
            PreviewImage.Source = ImageSource.FromFile(imagePath);
            PlaceholderBorder.IsVisible = false;
            ImageBorder.IsVisible = true;
            ClearButton.IsVisible = true;
            ChoosePhotoButton.Text = "CHOOSE ANOTHER";
            ResultsCard.IsVisible = false;

            LoadingLabel.Text = "Analyzing crop...";
            LoadingBorder.IsVisible = true;

            await Task.Yield();
            var prediction = await Task.Run(() => _classifier.Classify(imagePath));

            _lastPrediction = prediction;
            UpdateResults(prediction);
            LoadingBorder.IsVisible = false;
            ResultsCard.IsVisible = true;
            await AnimateResultsEntrance();
        }
        catch (Exception ex)
        {
            LoadingBorder.IsVisible = false;
            ResultsCard.IsVisible = false;
            await ReportScanErrorAsync("Scan Error", ex);
        }
        finally
        {
            _isClassifying = false;
            SetBusyState(false);
        }
    }

    void SetBusyState(bool busy)
    {
        ChoosePhotoButton.IsEnabled = !busy;
        TakePhotoButton.IsEnabled = !busy;
        ClearButton.IsEnabled = !busy;
    }

    async Task ReportScanErrorAsync(string title, Exception exception)
    {
        var detail = exception.Message;
        if (string.IsNullOrWhiteSpace(detail))
            detail = "The photo could not be processed. Please choose a JPG or PNG image and try again.";

        ScanErrorLabel.Text = detail;
        ScanErrorLabel.IsVisible = true;
        System.Diagnostics.Debug.WriteLine($"{title}: {exception}");

        try
        {
            await DisplayAlertAsync(title, detail, "OK");
        }
        catch
        {
            // Keep the inline error visible if the platform cannot present a modal alert.
        }
    }

    void UpdateResults(LeafPrediction prediction)
    {
        LettuceDetectionCard.IsVisible = false;
        HealthCard.IsVisible = false;
        VarietyCard.IsVisible = false;
        DetailedPredictionsLabel.IsVisible = false;
        PredictionsList.IsVisible = false;

        var isLettuce = prediction.IsLettuce;
        var lettuceColor = isLettuce ? Color.FromArgb("#74E0A2") : Color.FromArgb("#FF7B86");
        var lettuceIcon = isLettuce ? "OK" : "NO";

        LettuceStatusBorder.BackgroundColor = lettuceColor;
        LettuceStatusIcon.Text = lettuceIcon;
        LettuceStatusLabel.Text = isLettuce ? "Lettuce Detected" : "Not Lettuce";
        LettuceStatusLabel.TextColor = lettuceColor;
        LettuceConfidenceLabel.Text = $"Confidence: {prediction.LettuceConfidence:P0}";
        LettuceDetailLabel.Text = isLettuce ? "Variety and health available" : "Object is outside the lettuce class";
        LettuceDetectionCard.IsVisible = true;

        if (!isLettuce) return;

        var healthInfo = GetHealthInfo(prediction.HealthLabel);
        HealthStatusBorder.BackgroundColor = healthInfo.Color;
        HealthStatusIcon.Text = healthInfo.Icon;
        HealthStatusLabel.Text = $"Health: {prediction.HealthLabel.FirstCharToUpper()}";
        HealthStatusLabel.TextColor = healthInfo.Color;
        HealthConfidenceLabel.Text = $"Confidence: {prediction.HealthConfidence:P0}";
        HealthDetailLabel.Text = "Nutrient status and disease detection";
        HealthCard.IsVisible = true;

        if (!string.IsNullOrWhiteSpace(prediction.VarietyLabel) && prediction.VarietyLabel != "unknown")
        {
            var varietyInfo = GetVarietyInfo(prediction.VarietyLabel);
            VarietyStatusBorder.BackgroundColor = varietyInfo.Color;
            VarietyStatusIcon.Text = varietyInfo.Icon;
            VarietyStatusLabel.Text = $"Variety: {prediction.VarietyLabel.Replace('_', ' ').FirstCharToUpper()}";
            VarietyStatusLabel.TextColor = varietyInfo.Color;
            VarietyConfidenceLabel.Text = $"Confidence: {prediction.VarietyConfidence:P0}";
            VarietyDetailLabel.Text = prediction.VarietyLabel == "green_ice"
                ? "Green Ice lettuce match"
                : "Lettuce outside the labeled Green Ice class";
            VarietyCard.IsVisible = true;
        }

        DetailedPredictionsLabel.IsVisible = true;
        PredictionsList.IsVisible = true;

        var allItems = new List<PredictionItem>
        {
            new()
            {
                Label = "Lettuce Detection",
                Icon = "OK",
                Confidence = prediction.LettuceConfidence,
                Color = lettuceColor
            }
        };

        foreach (var kv in prediction.HealthScores.OrderByDescending(x => x.Value))
        {
            var info = GetHealthInfo(kv.Key);
            allItems.Add(new PredictionItem
            {
                Label = $"Health: {kv.Key.FirstCharToUpper()}",
                Icon = info.Icon,
                Confidence = kv.Value,
                Color = info.Color
            });
        }

        foreach (var kv in prediction.VarietyScores.OrderByDescending(x => x.Value))
        {
            var info = GetVarietyInfo(kv.Key);
            allItems.Add(new PredictionItem
            {
                Label = $"Variety: {kv.Key.Replace('_', ' ').FirstCharToUpper()}",
                Icon = info.Icon,
                Confidence = kv.Value,
                Color = info.Color
            });
        }

        PredictionsList.ItemsSource = allItems;
    }

    static (Color Color, string Icon) GetHealthInfo(string healthLabel) => healthLabel.ToLower() switch
    {
        "healthy" => (Color.FromArgb("#74E0A2"), "H"),
        "deficient" => (Color.FromArgb("#F2C94C"), "N"),
        "diseased" => (Color.FromArgb("#FF7B86"), "D"),
        _ => (Colors.Gray, "?")
    };

    static (Color Color, string Icon) GetVarietyInfo(string varietyLabel) => varietyLabel.ToLower() switch
    {
        "green_ice" => (Color.FromArgb("#74E0A2"), "GI"),
        "not_green_ice" => (Color.FromArgb("#67C4F4"), "NG"),
        _ => (Colors.Gray, "?")
    };

    void OnClearClicked(object sender, EventArgs e)
    {
        DeleteTemporaryImage();
        _lastPrediction = null;
        PreviewImage.Source = null;
        PlaceholderBorder.IsVisible = true;
        ImageBorder.IsVisible = false;
        LoadingBorder.IsVisible = false;
        ClearButton.IsVisible = false;
        ChoosePhotoButton.Text = "CHOOSE PHOTO";
        ResultsCard.IsVisible = false;
        ScanErrorLabel.IsVisible = false;
    }

    async void OnSaveResultClicked(object sender, EventArgs e)
    {
        if (_lastPrediction == null || string.IsNullOrWhiteSpace(_currentImagePath) || !File.Exists(_currentImagePath))
        {
            await DisplayAlertAsync("Nothing to save", "Run a crop scan first.", "OK");
            return;
        }

        try
        {
            var savedDirectory = Path.Combine(FileSystem.AppDataDirectory, "Scans");
            Directory.CreateDirectory(savedDirectory);
            var extension = Path.GetExtension(_currentImagePath);
            var savedPath = Path.Combine(savedDirectory, $"scan_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}{extension}");
            File.Copy(_currentImagePath, savedPath, true);

            using var scope = (App.Services ?? throw new InvalidOperationException("Application services are unavailable.")).CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
            var camera = await db.Cameras.OrderBy(item => item.Id).FirstOrDefaultAsync();
            if (camera == null)
            {
                camera = new Camera { Name = "Mobile crop scanner", Model = DeviceInfo.Current.Model };
                db.Cameras.Add(camera);
            }

            var predictionLabel = !_lastPrediction.IsLettuce
                ? "non_lettuce"
                : string.IsNullOrWhiteSpace(_lastPrediction.VarietyLabel) || _lastPrediction.VarietyLabel == "unknown"
                    ? _lastPrediction.HealthLabel
                    : $"{_lastPrediction.VarietyLabel}/{_lastPrediction.HealthLabel}";
            var confidence = _lastPrediction.IsLettuce && _lastPrediction.HealthConfidence > 0
                ? _lastPrediction.VarietyConfidence > 0
                    ? Math.Min(_lastPrediction.VarietyConfidence, _lastPrediction.HealthConfidence)
                    : _lastPrediction.HealthConfidence
                : _lastPrediction.LettuceConfidence;

            db.LeafClassifications.Add(new LeafClassification
            {
                Camera = camera,
                Timestamp = DateTime.UtcNow,
                ImagePath = savedPath,
                PredictedClass = predictionLabel,
                Confidence = confidence
            });
            await db.SaveChangesAsync();
            await DisplayAlertAsync("Scan saved", "The classification and image were added to local history.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Save failed", ex.Message, "OK");
        }
    }

    void OnClassifyAnotherClicked(object sender, EventArgs e) => OnClearClicked(sender, e);

    async Task AnimateResultsEntrance()
    {
        ResultsCard.Opacity = 0;
        ResultsCard.TranslationY = 20;
        await Task.WhenAll(
            ResultsCard.FadeToAsync(1, 180, Easing.CubicOut),
            ResultsCard.TranslateToAsync(0, 0, 180, Easing.CubicOut));
    }

    static async Task<string> StageImageAsync(FileResult result)
    {
        var scansDirectory = Path.Combine(FileSystem.CacheDirectory, "Scans");
        Directory.CreateDirectory(scansDirectory);

        var extension = Path.GetExtension(result.FileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 8)
            extension = ".jpg";

        var imagePath = Path.Combine(scansDirectory, $"scan_{Guid.NewGuid():N}{extension.ToLowerInvariant()}");
        await using var input = await result.OpenReadAsync();
        await using var output = File.Create(imagePath);
        await input.CopyToAsync(output);
        return imagePath;
    }

    void DeleteTemporaryImage()
    {
        try
        {
            var scanCache = Path.GetFullPath(Path.Combine(FileSystem.CacheDirectory, "Scans"));
            var imagePath = string.IsNullOrWhiteSpace(_currentImagePath) ? null : Path.GetFullPath(_currentImagePath);
            if (imagePath != null && imagePath.StartsWith(scanCache, StringComparison.OrdinalIgnoreCase) && File.Exists(imagePath))
                File.Delete(imagePath);
        }
        catch
        {
            // Cache cleanup must never make the scanner unusable.
        }

        _currentImagePath = null;
    }

    static Color GetResourceColor(string key, Color fallback)
        => Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : fallback;
}

static class StringExtensions
{
    public static string FirstCharToUpper(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToUpper(input[0]) + input.Substring(1).ToLower();
    }
}
