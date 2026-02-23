using System.Net.Http.Headers;
using System.Text.Json;

namespace QuanLyKhachHang.Models1.QuanLyMucDoDichVu
{
    public interface IFlightService
    {
        Task<string> GetNearestAirport(double lat, double lon);
        Task<(DateTime? DepartureTime, DateTime? ArrivalTime, string FlightNo, decimal Price)> GetEarliestFlight(string origin, string dest, DateTime fromTime);
    }

    public class AmadeusFlightService : IFlightService
    {
        private readonly HttpClient _httpClient;
        private string _accessToken = "";
        private DateTime _tokenExpiration = DateTime.MinValue;
        private readonly string _clientId = "19k8qorqpMZ5ABrh62D92VGmkcroyQzG";
        private readonly string _clientSecret = "dDAPcO5AJh0O9z75";

        public AmadeusFlightService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://test.api.amadeus.com/");
        }

        private async Task AuthenticateAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiration) return;

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret)
            });

            var response = await _httpClient.PostAsync("v1/security/oauth2/token", content);
            if (!response.IsSuccessStatusCode) throw new Exception("Xác thực Amadeus thất bại.");

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            _accessToken = doc.RootElement.GetProperty("access_token").GetString();
            _tokenExpiration = DateTime.Now.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32() - 60);
        }

        public async Task<string> GetNearestAirport(double lat, double lon)
        {
            try
            {
                await AuthenticateAsync();
                string url = $"v1/reference-data/locations/airports?latitude={lat}&longitude={lon}&radius=500&page[limit]=1&sort=distance";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return doc.RootElement.GetProperty("data")[0].GetProperty("iataCode").GetString();
            }
            catch { return null; }
        }

        public async Task<(DateTime? DepartureTime, DateTime? ArrivalTime, string FlightNo, decimal Price)> GetEarliestFlight(string origin, string dest, DateTime fromTime)
        {
            try
            {
                await AuthenticateAsync();
                string dateStr = fromTime.ToString("yyyy-MM-dd");
                string url = $"v2/shopping/flight-offers?originLocationCode={origin}&destinationLocationCode={dest}&departureDate={dateStr}&adults=1&nonStop=true&max=10";

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return (null, null, null, 0);

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var data = doc.RootElement.GetProperty("data");
                if (data.GetArrayLength() == 0) return (null, null, null, 0);

                var validFlights = new List<(DateTime Dep, DateTime Arr, string No, decimal Price)>();
                foreach (var offer in data.EnumerateArray())
                {
                    var segment = offer.GetProperty("itineraries")[0].GetProperty("segments")[0];
                    DateTime dep = DateTime.Parse(segment.GetProperty("departure").GetProperty("at").GetString());
                    DateTime arr = DateTime.Parse(segment.GetProperty("arrival").GetProperty("at").GetString());
                    string no = segment.GetProperty("carrierCode").GetString() + segment.GetProperty("number").GetString();
                    decimal price = decimal.Parse(offer.GetProperty("price").GetProperty("total").GetString());

                    if (dep >= fromTime) validFlights.Add((dep, arr, no, price));
                }

                var earliest = validFlights.OrderBy(f => f.Dep).FirstOrDefault();
                return earliest.Dep != default ? (earliest.Dep, earliest.Arr, earliest.No, earliest.Price) : (null, null, null, 0);
            }
            catch { return (null, null, null, 0); }
        }
    }
}