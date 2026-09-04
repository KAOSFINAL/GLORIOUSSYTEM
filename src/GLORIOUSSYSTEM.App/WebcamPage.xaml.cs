using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;

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

    public WebcamPage()
    {
        InitializeComponent();

        try
        {
            _classifier = new LeafClassifierService();
        }
        catch (Exception ex)
        {
            _initError = ex.Message;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_initError != null)
        {
            LoadingLabel.Text = "AI model unavailable";
            LoadingBorder.IsVisible = false;
        }
    }

    async void OnChoosePhotoClicked(object sender, EventArgs e)
    {
        if (!EnsureClassifier()) return;

        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Select a lettuce photo"
            });

            if (result == null) return;
            await ClassifyImage(result.FullPath);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Photo Error", ex.Message, "OK");
        }
    }

    async void OnTakePhotoClicked(object sender, EventArgs e)
    {
        if (!EnsureClassifier()) return;

        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await DisplayAlertAsync("Camera Unavailable", "Camera capture is not supported on this device.", "OK");
            return;
        }

        try
        {
            var result = await MediaPicker.Default.CapturePhotoAsync();
            if (result == null) return;
            await ClassifyImage(result.FullPath);
        }
        catch (PermissionException)
        {
            await DisplayAlertAsync("Camera Permission", "Camera access was denied. Allow camera access for GLORIOUSSYSTEM and try again.", "OK");
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlertAsync("Camera Unavailable", "This device does not support photo capture.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Camera Error", ex.Message, "OK");
        }
    }

    bool EnsureClassifier()
    {
        if (_classifier != null) return true;

        _ = DisplayAlertAsync("AI Model Error", _initError ?? "The CNN model could not be loaded.", "OK");
        return false;
    }

    async Task ClassifyImage(string imagePath)
    {
        if (_isClassifying || _classifier == null || string.IsNullOrWhiteSpace(imagePath)) return;
        if (!File.Exists(imagePath))
        {
            await DisplayAlertAsync("Image Error", "The selected image could not be opened.", "OK");
            return;
        }

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

            UpdateResults(prediction);
            LoadingBorder.IsVisible = false;
            ResultsCard.IsVisible = true;
            await AnimateResultsEntrance();
        }
        catch (Exception ex)
        {
            LoadingBorder.IsVisible = false;
            ResultsCard.IsVisible = false;
            await DisplayAlertAsync("Scan Error", "The image could not be analyzed.\n\n" + ex.Message, "OK");
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

    void UpdateResults(LeafPrediction prediction)
    {
        LettuceDetectionCard.IsVisible = false;
        HealthCard.IsVisible = false;
        AgeCard.IsVisible = false;
        DetailedPredictionsLabel.IsVisible = false;
        PredictionsList.IsVisible = false;

        var isLettuce = prediction.IsLettuce;
        var lettuceColor = isLettuce ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
        var lettuceIcon = isLettuce ? "🌿" : "🚫";

        LettuceStatusBorder.BackgroundColor = lettuceColor;
        LettuceStatusIcon.Text = lettuceIcon;
        LettuceStatusLabel.Text = isLettuce ? "Lettuce Detected" : "Not Lettuce";
        LettuceStatusLabel.TextColor = lettuceColor;
        LettuceConfidenceLabel.Text = $"Confidence: {prediction.LettuceConfidence:P0}";
        LettuceDetailLabel.Text = isLettuce ? "Health and growth stage available" : "Object is outside the lettuce class";
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

        var ageInfo = GetAgeInfo(prediction.AgeLabel);
        AgeStatusBorder.BackgroundColor = ageInfo.Color;
        AgeStatusIcon.Text = ageInfo.Icon;
        AgeStatusLabel.Text = $"Growth Stage: {prediction.AgeLabel.FirstCharToUpper()}";
        AgeStatusLabel.TextColor = ageInfo.Color;
        AgeConfidenceLabel.Text = $"Confidence: {prediction.AgeConfidence:P0}";
        AgeDetailLabel.Text = "Estimated development phase";
        AgeCard.IsVisible = true;

        DetailedPredictionsLabel.IsVisible = true;
        PredictionsList.IsVisible = true;

        var allItems = new List<PredictionItem>
        {
            new()
            {
                Label = "Lettuce Detection",
                Icon = "🌿",
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

        foreach (var kv in prediction.AgeScores.OrderByDescending(x => x.Value))
        {
            var info = GetAgeInfo(kv.Key);
            allItems.Add(new PredictionItem
            {
                Label = $"Age: {kv.Key.FirstCharToUpper()}",
                Icon = info.Icon,
                Confidence = kv.Value,
                Color = info.Color
            });
        }

        PredictionsList.ItemsSource = allItems;
    }

    static (Color Color, string Icon) GetHealthInfo(string healthLabel) => healthLabel.ToLower() switch
    {
        "healthy" => (Color.FromArgb("#10B981"), "✅"),
        "deficient" => (Color.FromArgb("#F59E0B"), "⚠️"),
        "diseased" => (Color.FromArgb("#EF4444"), "🦠"),
        _ => (Colors.Gray, "❓")
    };

    static (Color Color, string Icon) GetAgeInfo(string ageLabel) => ageLabel.ToLower() switch
    {
        "seedling" => (Color.FromArgb("#8B5CF6"), "🌱"),
        "vegetative" => (Color.FromArgb("#10B981"), "🌿"),
        "mature" => (Color.FromArgb("#3B82F6"), "🥬"),
        "harvest_ready" => (Color.FromArgb("#F59E0B"), "🌾"),
        _ => (Colors.Gray, "❓")
    };

    void OnClearClicked(object sender, EventArgs e)
    {
        PreviewImage.Source = null;
        PlaceholderBorder.IsVisible = true;
        ImageBorder.IsVisible = false;
        LoadingBorder.IsVisible = false;
        ClearButton.IsVisible = false;
        ChoosePhotoButton.Text = "CHOOSE PHOTO";
        ResultsCard.IsVisible = false;
    }

    async void OnSaveResultClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Saved", "Classification result saved to history", "OK");
    }

    void OnClassifyAnotherClicked(object sender, EventArgs e) => OnClearClicked(sender, e);

    async Task AnimateResultsEntrance()
    {
        ResultsCard.Opacity = 0;
        ResultsCard.TranslationY = 20;
        await Task.WhenAll(
            ResultsCard.FadeToAsync(1, 300, Easing.CubicOut),
            ResultsCard.TranslateToAsync(0, 0, 300, Easing.CubicOut));
    }
}

static class StringExtensions
{
    public static string FirstCharToUpper(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToUpper(input[0]) + input.Substring(1).ToLower();
    }
}