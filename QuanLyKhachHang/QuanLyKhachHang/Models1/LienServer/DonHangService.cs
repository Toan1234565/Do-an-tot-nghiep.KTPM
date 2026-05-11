using Microsoft.Extensions.Logging;
using QuanLyKhachHang.Models1.LienServer;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuanLyKhachHang.Models1.LienServer
{

    public class DonHangServiceClient : IDonHangService
    {
        private readonly HttpClient _client;
        private readonly ILogger<DonHangServiceClient> _logger;

        public DonHangServiceClient(HttpClient client, ILogger<DonHangServiceClient> logger)
        {
            _client = client;
            _logger = logger;
        }

        
        public async Task<PagedDonHangResponse?> GetDanhSachDonHangByKhachHangAsync(int maKhachHang, int page = 1, int pageSize = 10)
        {
            try
            {
                var url = $"https://localhost:7264/api/quanlydonhang/danhsachdonhangtheokhachhang/{maKhachHang}?page={page}&pageSize={pageSize}";

                var response = await _client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    // Cấu hình Options để đọc được JSON camelCase và linh hoạt kiểu dữ liệu
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true, // Không phân biệt hoa thường (MaDonHang == madonhang)
                        NumberHandling = JsonNumberHandling.AllowReadingFromString // Nếu API trả về "1" vẫn hiểu là số 1
                    };

                    return await response.Content.ReadFromJsonAsync<PagedDonHangResponse>(options);
                }

                _logger.LogWarning("API lỗi. KH ID: {MaKH}, Status: {Status}", maKhachHang, response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                // Lỗi parse JSON sẽ rơi vào đây
                _logger.LogError(ex, "Lỗi kết nối hoặc Parse JSON API đơn hàng cho KH: {MaKH}", maKhachHang);
                return null;
            }
        }
    }
}