using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyLoTrinhTheoDoi
{
    public class QuanLyDieuPhoi : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyDieuPhoi> _logger;
        // Port 7097 là Server Lộ trình của bạn
        private readonly string apiBaseUrl = "https://localhost:7097/api/dieuphoilotrinh";

        public QuanLyDieuPhoi(IHttpClientFactory httpClientFactory, ILogger<QuanLyDieuPhoi> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> QuanLyDieuPhoiLoTrinh()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                // Giả sử bạn có API lấy tất cả lộ trình đang hoạt động
                var response = await client.GetAsync($"{apiBaseUrl}/tat-ca-lo-trinh");

                if (response.IsSuccessStatusCode)
                {
                    var danhSachLoTrinh = await response.Content.ReadFromJsonAsync<List<dynamic>>();
                    return View(danhSachLoTrinh);
                }

                _logger.LogWarning("Không thể lấy dữ liệu từ API Lộ trình.");
                return View(new List<dynamic>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối Server Lộ trình");
                return View(new List<dynamic>());
            }
        }
    }
}