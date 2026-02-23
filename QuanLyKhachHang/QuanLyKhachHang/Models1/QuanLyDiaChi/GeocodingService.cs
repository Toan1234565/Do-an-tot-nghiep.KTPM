using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuanLyKhachHang.Services
{
    public interface IGeocodingService
    {
        Task<(double? lat, double? lon)> GetCoordinatesAsync(string? duong, string? phuong, string? thanhPho);
    }

    public class GeocodingService : IGeocodingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GeocodingService> _logger;

        public GeocodingService(IHttpClientFactory httpClientFactory, ILogger<GeocodingService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<(double? lat, double? lon)> GetCoordinatesAsync(string? duong, string? phuong, string? thanhPho)
        {
            if (string.IsNullOrWhiteSpace(thanhPho)) return (null, null);

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "LogisticsApp/1.0");

                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(duong)) parts.Add(duong);
                if (!string.IsNullOrWhiteSpace(phuong)) parts.Add(phuong);
                if (!string.IsNullOrWhiteSpace(thanhPho)) parts.Add(thanhPho);
                parts.Add("Vietnam");

                string fullAddress = string.Join(", ", parts);
                string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(fullAddress)}&format=json&limit=1";

                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<List<GeocodeResult>>(json);

                    if (data != null && data.Count > 0)
                    {
                        double.TryParse(data[0].Lat, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double latRes);
                        double.TryParse(data[0].Lon, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lonRes);
                        return (latRes, lonRes);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Lỗi Geocoding: {Message}", ex.Message);
            }
            return (null, null);
        }

        private class GeocodeResult
        {
            [JsonPropertyName("lat")] public string? Lat { get; set; }
            [JsonPropertyName("lon")] public string? Lon { get; set; }
        }
    }
}