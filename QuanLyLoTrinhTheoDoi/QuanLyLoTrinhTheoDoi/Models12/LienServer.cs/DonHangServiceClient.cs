using Microsoft.Extensions.Logging;
using QuanLyLoTrinhTheoDoi.Models12.LienServer.cs;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;
using System.Net.Http.Json;

namespace QuanLyLoTrinhTheoDoi.Models12.LienServer
{
    public class DonHangServiceClient : IDonHangService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DonHangServiceClient> _logger;

        public DonHangServiceClient(HttpClient httpClient, ILogger<DonHangServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ChiTietDonHangLoTrinhModel?> GetChiTietDonHangAsync(int madonhang)
        {
            try
            {
                // Gọi API với route bạn đã định nghĩa ở Controller
                // Lưu ý: Tên endpoint "chi-tiet-don-hang/{madonhang}"
                var response = await _httpClient.GetAsync($"https://localhost:7264/api/quanlydonhang/chi-tiet-don-hang/{madonhang}");

                if (response.IsSuccessStatusCode)
                {
                    // Giải mã JSON trả về thành Object
                    return await response.Content.ReadFromJsonAsync<ChiTietDonHangLoTrinhModel>();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Không tìm thấy đơn hàng mã {MaDonHang} từ API.", madonhang);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi lấy chi tiết đơn hàng {MaDonHang}", madonhang);
                return null;
            }
        }
    }
}