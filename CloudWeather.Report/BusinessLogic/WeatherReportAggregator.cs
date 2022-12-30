using System.Text.Json;
using Microsoft.Extensions.Options;

public interface IWeatherReportAggregator
{
    public Task<WeatherReport> BuildReport(string zip, int days);
}


public class WeatherReportAggregator : IWeatherReportAggregator
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<WeatherReportAggregator> _logger;
    private readonly WeatherDataConfig _weatherDataConfig;
    private readonly WeatherReportDbContext _db;

    public WeatherReportAggregator(
        IHttpClientFactory http,
        ILogger<WeatherReportAggregator> logger,
        IOptions<WeatherDataConfig> weatherDataConfig,
        WeatherReportDbContext db)
    {
        _http = http;
        _logger = logger;
        _weatherDataConfig = weatherDataConfig.Value;
        _db = db;

    }

    public async Task<WeatherReport> BuildReport(string zip, int days)
    {
        var httpClient = _http.CreateClient();
        var precipData = await FetchPrecipitationData(httpClient, zip, days);
        var totalSnow = GetTotalSnow(precipData);
        var totalRain = GetTotalRain(precipData);
        _logger.LogInformation(
            $"zip: {zip} over the last {days} days: " +
            $"total snow: {totalSnow}, rain: {totalRain}"
        );
        var tempData = await FetchTemperatureData(httpClient, zip, days);
        var averageHighTemp = tempData.Average(t => t.TempHighF);
        var averageLowTemp = tempData.Average(t => t.TempLowF);

        _logger.LogInformation(
            $"zip: {zip} over the last {days} days: " +
            $"total snow: {totalSnow}, rain: {totalRain}"
        );

        var weatherReport = new WeatherReport{
            AverageHighF = Math.Round(averageHighTemp, 1),
            AverageLowF = Math.Round(averageLowTemp,1),
            RainfallTotalInches = totalRain,
            SnowTotalInches = totalSnow,
            ZipCode =zip,
            CreatedOn = DateTime.UtcNow
        };

        //TODO: Use 'cached' weather reports instead of doing round trips
        _db.Add(weatherReport);
        await _db.SaveChangesAsync();

        return weatherReport;
    }

    private decimal GetTotalRain(IEnumerable<PrecipitationModel> precipData)
    {
        var totalSnow = precipData
            .Where(p => p.WeatherType == "snow")
            .Sum(p => p.AmountInches);
        return Math.Round(totalSnow, 1);
    }

    private decimal GetTotalSnow(IEnumerable<PrecipitationModel> precipData)
    {
        var totalRain = precipData
            .Where(p => p.WeatherType == "rain")
            .Sum(p => p.AmountInches);
        return Math.Round(totalRain, 1);

    }

    private async Task<List<TemperatureModel>> FetchTemperatureData(HttpClient httpClient, string zip, int days)
    {
        var endPoint = BuildTemperatureServiceEndpoint(zip, days);
        var temperatureRecords = await httpClient.GetAsync(endPoint);
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var temperatureData = await temperatureRecords
            .Content
            .ReadFromJsonAsync<List<TemperatureModel>>(jsonSerializerOptions);
        return temperatureData ?? new List<TemperatureModel>();
    }

    private async Task<List<PrecipitationModel>> FetchPrecipitationData(HttpClient httpClient, string zip, int days)
    {
        var endPoint = BuildPrecipitationServiceEndpoint(zip, days);
        var precipRecords = await httpClient.GetAsync(endPoint);
        var jsonSerializerOptions = new JsonSerializerOptions{
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var precipData = await precipRecords
            .Content
            .ReadFromJsonAsync<List<PrecipitationModel>>(jsonSerializerOptions);
        return precipData ?? new List<PrecipitationModel>();
    }

    private string BuildTemperatureServiceEndpoint(string zip, int days) =>
            $"{_weatherDataConfig.TempDataProtocol}://{_weatherDataConfig.TempDataHost}:{_weatherDataConfig.TempDataPort}/observation/{zip}?days={days}";

    private string BuildPrecipitationServiceEndpoint(string zip, int days) =>
            $"{_weatherDataConfig.PrecipDataProtocol}://{_weatherDataConfig.PrecipDataHost}:{_weatherDataConfig.PrecipDataPort}/observation/{zip}?days={days}";


}