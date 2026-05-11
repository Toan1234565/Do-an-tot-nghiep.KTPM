using Microsoft.Extensions.Logging; // Cần thiết cho ILogger
using QuanLyLoTrinhTheoDoi.Models12.LienServer.cs;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;
using System.Net.Http.Json; // Cần thiết để dùng ReadFromJsonAsync

namespace QuanLyLoTrinhTheoDoi.Models12.LienServer
{
    public class ChiTietKhachHangServerClient : IKhachHangServiceClient
    {
        private readonly HttpClient _client;
        private readonly ILogger<ChiTietKhachHangServerClient> _logger;

        public ChiTietKhachHangServerClient(HttpClient client, ILogger<ChiTietKhachHangServerClient> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<KhachHangSummaryDto?> GetChiTietKhachHangAsync(int maKhachHang)
        {
            try
            {
                // Sử dụng đường dẫn tương đối nếu HttpClient đã được cấu hình BaseAddress ở Program.cs
                // Hoặc giữ nguyên URL tuyệt đối nếu bạn muốn gọi trực tiếp
                var response = await _client.GetAsync($"https://localhost:7149/api/quanlykhachhang/chi-tiet-khach-hang/{maKhachHang}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<KhachHangSummaryDto>();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Không tìm thấy khách hàng với mã: {MaKhachHang}", maKhachHang);
                }
                else
                {
                    _logger.LogError("Lỗi API: {StatusCode} khi lấy khách hàng {MaKhachHang}", response.StatusCode, maKhachHang);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API lấy thông tin khách hàng cho mã: {MaKhachHang}", maKhachHang);
                return null;
            }
        }
    }
}