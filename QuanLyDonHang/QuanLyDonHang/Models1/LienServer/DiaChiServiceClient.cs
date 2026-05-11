using QuanLyDonHang.Models1.QuanLyDonHang.Models1;
using QuanLyDonHang.Models1.ServerKhachHang;
using System.Text.Json;

namespace QuanLyDonHang.Models1.LienServer
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

        public async Task<(int maDiaChi, string maVungH3)> CheckDiaChiAsync(dynamic diaChiReq)
        {
            // Ép kiểu diaChiReq sang (object) để trình biên dịch tìm được extension method
            var response = await _client.PostAsJsonAsync("https://localhost:7149/api/quanlydiachi/check_dia_chi", (object)diaChiReq);

            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<JsonElement>();

            int id = data.GetProperty("maDiaChi").GetInt32();
            string h3 = data.GetProperty("maVungH3").GetString() ?? "";

            return (id, h3);
        }

        public async Task<DiaChiModel?> GetChiTietDiaChiAsync(int maDiaChi)
        {
            try
            {
                // Endpoint tương ứng với [HttpGet("chitietdiachi/{maDiaChi}")] bên Server Địa chỉ
                var response = await _client.GetAsync($"api/quanlydiachi/chitietdiachi/{maDiaChi}");

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
