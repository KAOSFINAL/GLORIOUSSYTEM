using System.Net.Http.Json;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

public class ApiSensorReading
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Model { get; set; }
    public double? MinThreshold { get; set; }
    public double? MaxThreshold { get; set; }
    public double? LatestValue { get; set; }
    public string? LatestMetric { get; set; }
    public DateTime? LatestTimestamp { get; set; }
}

public class ApiSensorService
{
    readonly HttpClient _http;

    public ApiSensorService(string? baseUrl = null)
    {
        var platformDefault = OperatingSystem.IsAndroid()
            ? "http://10.0.2.2:5053/"
            : "http://localhost:5053/";
        baseUrl ??= Preferences.Get("Api_BaseUrl", platformDefault);
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(8)
        };
    }

    public async Task<List<ApiSensorReading>> GetLatestAsync()
    {
        var result = await _http.GetFromJsonAsync<List<ApiSensorReading>>("api/readings/latest");
        return result ?? new List<ApiSensorReading>();
    }
}
