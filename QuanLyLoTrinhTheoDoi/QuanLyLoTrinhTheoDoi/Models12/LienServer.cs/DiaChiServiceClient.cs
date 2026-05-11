using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;
using System.Text.Json;

namespace QuanLyLoTrinhTheoDoi.Models12.LienServer.cs
{
    public class DiaChiServiceClient : IDiaChiService
    {
        private readonly HttpClient _client;
        private readonly ILogger<DiaChiServiceClient> _logger;

        public DiaChiServiceClient(HttpClient client, ILogger<DiaChiServiceClient> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<DiaChiModel?> GetChiTietDiaChiAsync(int maDiaChi)
        {
            try
            {
                // Endpoint tương ứng với [HttpGet("chitietdiachi/{maDiaChi}")] bên Server Địa chỉ
                var response = await _client.GetAsync($"https://localhost:7149/api/quanlydiachi/chitietdiachi/{maDiaChi}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<DiaChiModel>();
                }

                _logger.LogWarning($"Không tìm thấy thông tin địa chỉ ID: {maDiaChi}. Status: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi kết nối API lấy chi tiết địa chỉ ID: {maDiaChi}");
                return null;
            }
        }
    }
}
