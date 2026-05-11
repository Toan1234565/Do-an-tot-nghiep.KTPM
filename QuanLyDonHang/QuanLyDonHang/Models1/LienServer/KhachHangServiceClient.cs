using QuanLyDonHang.Models1.QuanLyDonHang.Models1;
using QuanLyDonHang.Models1.ServerKhachHang;
using System.Text.Json;

namespace QuanLyDonHang.Models1.LienServer
{
    public class KhachHangServiceClient : IKhachHangService
    {
        private readonly HttpClient _client;
        private readonly ILogger<KhachHangServiceClient> _logger;

        public KhachHangServiceClient(HttpClient client, ILogger<KhachHangServiceClient> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<int> CheckSoDienThoaiAsync(string sdt, string ten, dynamic diaChi)
        {
            var response = await _client.PostAsJsonAsync("api/quanlykhachhang/check_so_dien_thoai", new
            {
                SoDienThoai = sdt,
                TenLienHe = ten,
                DiaChi = diaChi
            });

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<JsonElement>();
            // Lấy đúng property "maKhachHang" từ JSON trả về của API
            return data.GetProperty("maKhachHang").GetInt32();
        }

        public async Task<(decimal soTienGiam, int? maKhuyenMai)> ApDungKhuyenMaiAsync(string code, decimal tongTien, int maKhachHang)
        {
            var response = await _client.PostAsJsonAsync("https://localhost:7149/api/quanlykhachhang/ap_dung_khuyen_mai", new
            {
                Code = code,
                TongTien = tongTien,
                MaKhachHang = maKhachHang
            });

            if (!response.IsSuccessStatusCode) return (0, null);

            var data = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (
                data.GetProperty("soTienGiam").GetDecimal(),
                data.TryGetProperty("maKhuyenMai", out var id) ? id.GetInt32() : (int?)null
            );
        }

        // --- Phương thức thêm mới để lấy chi tiết ---
        public async Task<KhachHangModels?> GetChiTietKhachHangAsync(int maKhachHang)
        {
            try
            {
                // Gọi tới Endpoint bạn đã viết ở Server Khách hàng (Port 7149)
                // Lưu ý: Nếu BaseAddress của HttpClient đã là https://localhost:7149/ thì chỉ cần ghi phần sau
                var response = await _client.GetAsync($"https://localhost:7149/api/quanlykhachhang/chi-tiet-khach-hang/{maKhachHang}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<KhachHangModels>();
                }

                _logger.LogWarning($"Không tìm thấy khách hàng ID: {maKhachHang}. Status: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi gọi API chi tiết khách hàng ID: {maKhachHang}");
                return null;
            }
        }

    }
}
