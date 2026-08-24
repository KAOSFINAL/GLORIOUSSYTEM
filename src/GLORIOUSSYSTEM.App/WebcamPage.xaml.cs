using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;

namespace GLORIOUSSYSTEM.App;

public class PredictionItem : INotifyPropertyChanged
{
    string _label = "";
    string _icon = "";
    float _confidence = 0;
    Color _color = Colors.Gray;

    public string Label
    {
        get => _label;
        set { _label = value; OnPropertyChanged(); }
    }

    public string Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(); }
    }

    public float Confidence
    {
        get => _confidence;
        set { _confidence = value; OnPropertyChanged(); }
    }

    public Color Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class WebcamPage : ContentPage
{
    LeafClassifierService? _classifier;
    string? _initError;

    public WebcamPage()
    {
        InitializeComponent();

        try
        {
            _classifier = new LeafClassifierService();
        }
        catch (Exception ex)
        {
            _initError = ex.ToString();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_initError != null)
        {
            LoadingLabel.Text = "Model failed to load";
            LoadingBorder.IsVisible = false;
        }
    }

    async void OnChoosePhotoClicked(object sender, EventArgs e)
    {
        if (_classifier == null)
        {
            await DisplayAlert("Error", "Classifier not initialized: " + _initError, "OK");
            return;
        }

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            FileTypes = FilePickerFileType.Images,
            PickerTitle = "Select a photo"
        });

        if (result == null) return;

        await ClassifyImage(result.FullPath);
    }

    async void OnTakePhotoClicked(object sender, EventArgs e)
    {
        if (_classifier == null)
        {
            await DisplayAlert("Error", "Classifier not initialized: " + _initError, "OK");
            return;
        }

        var result = await MediaPicker.Default.CapturePhotoAsync();

        if (result == null) return;

        await ClassifyImage(result.FullPath);
    }

    async Task ClassifyImage(string imagePath)
    {
        // Show preview
        PreviewImage.Source = ImageSource.FromFile(imagePath);
        PlaceholderBorder.IsVisible = false;
        ImageBorder.IsVisible = true;
        ClearButton.IsVisible = true;
        ChoosePhotoButton.Text = "Choose Another";
        ResultsCard.IsVisible = false;

        // Show loading
        LoadingLabel.Text = "Analyzing...";
        LoadingBorder.IsVisible = true;

        // Small delay to show loading state
        await Task.Delay(300);

        // Run classification on background thread
        var prediction = await Task.Run(() => _classifier!.Classify(imagePath));

        // Update UI on main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateResults(prediction);
            LoadingBorder.IsVisible = false;
            ResultsCard.IsVisible = true;

            // Animate results entrance
            AnimateResultsEntrance();
        });
    }

    void UpdateResults(LeafPrediction prediction)
    {
        // Reset all result cards
        LettuceDetectionCard.IsVisible = false;
        HealthCard.IsVisible = false;
        AgeCard.IsVisible = false;
        DetailedPredictionsLabel.IsVisible = false;
        PredictionsList.IsVisible = false;

        // Lettuce Detection Result
        var isLettuce = prediction.IsLettuce;
        var lettuceColor = isLettuce ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
        var lettuceIcon = isLettuce ? "🌿" : "🚫";
        var lettuceLabel = isLettuce ? "Lettuce Detected" : "Not Lettuce";
        var lettuceDetail = isLettuce ? "Analyzing health and growth stage..." : "Showing a non-lettuce object";

        LettuceStatusBorder.BackgroundColor = lettuceColor;
        LettuceStatusIcon.Text = lettuceIcon;
        LettuceStatusLabel.Text = lettuceLabel;
        LettuceStatusLabel.TextColor = lettuceColor;
        LettuceConfidenceLabel.Text = $"Confidence: {prediction.LettuceConfidence:P0}";
        LettuceDetailLabel.Text = lettuceDetail;
        LettuceDetectionCard.IsVisible = true;

        if (isLettuce)
        {
            // Health Classification Result
            var healthInfo = GetHealthInfo(prediction.HealthLabel);
            HealthStatusBorder.BackgroundColor = healthInfo.Color;
            HealthStatusIcon.Text = healthInfo.Icon;
            HealthStatusLabel.Text = $"Health: {prediction.HealthLabel.FirstCharToUpper()}";
            HealthStatusLabel.TextColor = healthInfo.Color;
            HealthConfidenceLabel.Text = $"Confidence: {prediction.HealthConfidence:P0}";
            HealthDetailLabel.Text = "Nutrient status and disease detection";
            HealthCard.IsVisible = true;

            // Age Classification Result
            var ageInfo = GetAgeInfo(prediction.AgeLabel);
            AgeStatusBorder.BackgroundColor = ageInfo.Color;
            AgeStatusIcon.Text = ageInfo.Icon;
            AgeStatusLabel.Text = $"Growth Stage: {prediction.AgeLabel.FirstCharToUpper()}";
            AgeStatusLabel.TextColor = ageInfo.Color;
            AgeConfidenceLabel.Text = $"Confidence: {prediction.AgeConfidence:P0}";
            AgeDetailLabel.Text = "Estimated development phase";
            AgeCard.IsVisible = true;

            // Detailed Predictions
            DetailedPredictionsLabel.IsVisible = true;
            PredictionsList.IsVisible = true;

            var allItems = new List<PredictionItem>();

            // Lettuce detection
            allItems.Add(new PredictionItem
            {
                Label = "Lettuce Detection",
                Icon = "🌿",
                Confidence = prediction.LettuceConfidence,
                Color = lettuceColor
            });

            // Health scores
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

            // Age scores
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
    }

    (Color Color, string Icon) GetHealthInfo(string healthLabel)
    {
        return healthLabel.ToLower() switch
        {
            "healthy" => (Color.FromArgb("#10B981"), "✅"),
            "deficient" => (Color.FromArgb("#F59E0B"), "⚠️"),
            "diseased" => (Color.FromArgb("#EF4444"), "🦠"),
            _ => (Colors.Gray, "❓")
        };
    }

    (Color Color, string Icon) GetAgeInfo(string ageLabel)
    {
        return ageLabel.ToLower() switch
        {
            "seedling" => (Color.FromArgb("#8B5CF6"), "🌱"),
            "vegetative" => (Color.FromArgb("#10B981"), "🌿"),
            "mature" => (Color.FromArgb("#3B82F6"), "🥬"),
            "harvest_ready" => (Color.FromArgb("#F59E0B"), "🌾"),
            _ => (Colors.Gray, "❓")
        };
    }

    void OnClearClicked(object sender, EventArgs e)
    {
        PreviewImage.Source = null;
        PlaceholderBorder.IsVisible = true;
        ImageBorder.IsVisible = false;
        ClearButton.IsVisible = false;
        ChoosePhotoButton.Text = "Choose Photo";
        ResultsCard.IsVisible = false;
    }

    void OnSaveResultClicked(object sender, EventArgs e)
    {
        // TODO: Implement save to database
        DisplayAlert("Saved", "Classification result saved to history", "OK");
    }

    void OnClassifyAnotherClicked(object sender, EventArgs e)
    {
        OnClearClicked(sender, e);
    }

    async void AnimateResultsEntrance()
    {
        ResultsCard.Opacity = 0;
        ResultsCard.TranslationY = 20;
        await Task.WhenAll(
            ResultsCard.FadeToAsync(1, 300, Easing.CubicOut),
            ResultsCard.TranslateToAsync(0, 0, 300, Easing.CubicOut)
        );
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