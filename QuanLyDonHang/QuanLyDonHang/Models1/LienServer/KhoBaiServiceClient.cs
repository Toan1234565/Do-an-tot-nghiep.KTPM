using QuanLyDonHang.Models1.QuanLyDonHang.Models1;
using System.Net.Http.Json;
using System.Text.Json;

namespace QuanLyDonHang.Models1.LienServer
{
    public class KhoBaiServiceClient : IKhoBaiService
    {
        private readonly HttpClient _client;
        public KhoBaiServiceClient(HttpClient client) { _client = client; }

        public async Task<int?> TimKhoGanNhatAsync(int maDiaChi)
        {
            // URL khớp với API bên Server Kho bãi của bạn (Port 7286)
            var response = await _client.GetAsync($"https://localhost:7286/api/quanlykhobai/tim-kho-gan-nhat/{maDiaChi}");

            if (!response.IsSuccessStatusCode) return null;

            var data = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (data.TryGetProperty("maKho", out var id))
            {
                return id.GetInt32();
            }
            return null;
        }
    }
}