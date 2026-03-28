using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models12.CauHinhDiem;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyKhachHang
{
    public class CauHinhDiemController : Controller
    {
        private readonly ILogger<CauHinhDiemController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl = "https://localhost:7149/api/cauhinhtichdiem";

        public CauHinhDiemController(ILogger<CauHinhDiemController> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> CauHinhDiemView()
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            CauHinhDiemModels? cauHinh = null;

            try
            {
                string apiUrl = $"{_apiBaseUrl}/lay-cau-hinh";
                _logger.LogInformation("Calling API: {Url}", apiUrl);

                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    cauHinh = JsonConvert.DeserializeObject<CauHinhDiemModels>(content);
                }
                else
                {
                    _logger.LogWarning("API error: {StatusCode}", response.StatusCode);
                    ViewBag.ErrorMessage = "Không thể lấy dữ liệu từ máy chủ.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối API");
                ViewBag.ErrorMessage = "Lỗi kết nối hệ thống. Vui lòng thử lại sau.";
            }

            return View(cauHinh);
        }
    }
}
