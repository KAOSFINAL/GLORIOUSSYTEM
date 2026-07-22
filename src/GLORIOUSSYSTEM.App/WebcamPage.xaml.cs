namespace GLORIOUSSYSTEM.App;

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
            ResultLabel.Text = "Model failed to load";
            DetailLabel.Text = _initError;
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
            PickerTitle = "Select a leaf photo"
        });

        if (result == null) return;

        PreviewImage.Source = ImageSource.FromFile(result.FullPath);
        ResultLabel.Text = "Classifying...";
        DetailLabel.Text = "";

        var prediction = _classifier.Classify(result.FullPath);

        ResultLabel.Text = $"{prediction.Label.ToUpper()} ({prediction.Confidence:P0} confidence)";
        DetailLabel.Text = string.Join("  |  ", prediction.AllScores.Select(kv => $"{kv.Key}: {kv.Value:P0}"));
    }
}