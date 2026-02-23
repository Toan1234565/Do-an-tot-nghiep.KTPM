using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuanLyTaiKhoanNguoiDung.Models12;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyDonHang;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhachHang;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyKhachHang
{
    public class MucDoDichVu : Controller
    {
        private readonly ILogger<MucDoDichVu> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl = "https://localhost:7149/api/mucdichvu";
        private readonly string _apiBaseUrlDH = "https://localhost:7264/api/quanlydonhang";

        public MucDoDichVu(ILogger<MucDoDichVu> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> DanhSachMucDichVu(string? status = "active", string? search = null)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            var danhSach = new List<MucDoDichVuModesl>();

            try
            {
                // 1. Xử lý tham số status để khớp với API (nếu status là "all" thì không gửi tham số trangThai)
                var queryParams = new List<string>();

                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    queryParams.Add($"trangThai={status}");
                }

                if (!string.IsNullOrEmpty(search))
                {
                    // Sử dụng Uri.EscapeDataString để xử lý các ký tự đặc biệt trong từ khóa tìm kiếm
                    queryParams.Add($"search={Uri.EscapeDataString(search)}");
                }

                // 2. Ghép URL hoàn chỉnh
                string queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
                string apiUrl = $"{_apiBaseUrl}/dsmucdichvu{queryString}";

                _logger.LogInformation("Calling API: {Url}", apiUrl);

                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    danhSach = JsonConvert.DeserializeObject<List<MucDoDichVuModesl>>(content)
                               ?? new List<MucDoDichVuModesl>();
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

            // 3. Truyền dữ liệu ra View
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = search;

            return View(danhSach);
        }
    }
}