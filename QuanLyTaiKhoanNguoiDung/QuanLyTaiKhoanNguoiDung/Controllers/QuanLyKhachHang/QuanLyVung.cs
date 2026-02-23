using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyBangGiaVung;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhachHang;
using System.Net.Http.Json;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyKhachHang
{
    public class QuanLyVung : Controller
    {
        private readonly ILogger<QuanLyVung> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string apiBaseUrl = "https://localhost:7149/api/quanlybangiavung";
        private readonly string apiDonhang = "https://localhost:7264/api/danhmucloaihang";

        public QuanLyVung(ILogger<QuanLyVung> logger, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> GetDanhSachBangGiaVung(
    string? searchTerm,
    int page = 1,
    string khuvuclay = "Tất cả",
    string khuvucgiao = "Tất cả",
    string loaitinhgia = "Tất cả",
    bool? isActive = true)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            int pageSize = 15;
            List<BangGiaVungModels> data = new List<BangGiaVungModels>();
            int totalItems = 0;

            // --- 1. GỌI API BẢNG GIÁ VÙNG (API CHÍNH) ---
            var queryParams = new Dictionary<string, string?>
            {
                ["searchTerm"] = searchTerm,
                ["page"] = page.ToString(),
                ["pageSize"] = pageSize.ToString(),
                ["khuvuclay"] = khuvuclay,
                ["khuvucgiao"] = khuvucgiao,
                ["loaitinhgia"] = loaitinhgia,
                ["isActive"] = isActive?.ToString().ToLower()
            };
            var filteredParams = queryParams.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value!);
            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{apiBaseUrl}/dsbanggia", filteredParams);

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(jsonString);
                    var itemsJson = result?.items?.ToString();
                    data = string.IsNullOrEmpty(itemsJson)
                        ? new List<BangGiaVungModels>()
                        : JsonConvert.DeserializeObject<List<BangGiaVungModels>>(itemsJson);
                    totalItems = result?.totalItems ?? 0;
                }
                else
                {
                    _logger.LogError($"API BangGiaVung Error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể kết nối API Bảng giá vùng");
                // Không return luôn, để code chạy tiếp lấy dữ liệu API khác nếu cần
            }

            // --- 2. GỌI API LOẠI HÀNG (API PHỤ) ---
            try
            {
                var responseLoaiHang = await client.GetAsync($"{apiDonhang}/laytatcaloaihang");
                if (responseLoaiHang.IsSuccessStatusCode)
                {
                    var jsonLH = await responseLoaiHang.Content.ReadAsStringAsync();
                    var listLH = JsonConvert.DeserializeObject<List<LoaiHangModels>>(jsonLH);
                    ViewBag.ListLoaiHang = listLH;
                }
                else
                {
                    _logger.LogWarning("API LoaiHang không trả về dữ liệu thành công.");
                    ViewBag.ListLoaiHang = new List<LoaiHangModels>(); // Trả về list rỗng để View không lỗi
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API Loại hàng đang bị ngắt kết nối.");
                ViewBag.ListLoaiHang = new List<LoaiHangModels>();
            }

            // --- 3. GÁN METADATA CHO VIEW ---
            ViewBag.SearchTerm = searchTerm;
            ViewBag.KhuVucLay = khuvuclay;
            ViewBag.KhuVucGiao = khuvucgiao;
            ViewBag.LoaiTinhGia = loaitinhgia;
            ViewBag.IsActive = isActive;
            ViewBag.CurrentPage = page;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            return View(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BangGiaVungModels model)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            var response = await client.PostAsJsonAsync($"{apiBaseUrl}/themmoibanggia", model);

            if (response.IsSuccessStatusCode)
                return Json(new { success = true, message = "Thêm thành công!" });

            return Json(new { success = false, message = "Lỗi khi thêm!" });
        }
    }
}