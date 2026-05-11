using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models12;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyDonHang.QuanLyDonHang;
using QuanLyTaiKhoanNguoiDung.Models12.SeverQuanLyKhachHang.QuanLyKhachHang;
using System.Net.Http;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyKhachHang
{
    public class QuanLyKhachHang : Controller
    {
        private readonly ILogger<QuanLyKhachHang> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string apiBaseUrl = "https://localhost:7149/api/quanlykhachhang";
        private readonly string apiBaseUrlDonHang = "https://localhost:7264/api/quanlydonhang";

        public QuanLyKhachHang(ILogger<QuanLyKhachHang> logger, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // 1. Trang danh sách khách hàng (Giữ nguyên logic của bạn)
        public async Task<IActionResult> DanhSachKhachHang(string? searchTerm, int page = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            var queryParams = new Dictionary<string, string?>
            {
                ["searchTerm"] = searchTerm?.Trim(),
                ["page"] = page.ToString()
            };

            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{apiBaseUrl}/danhsachkhachhang", queryParams);

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<PaginationResult<KhachHangModels>>(jsonData);

                    ViewBag.TotalPages = result?.TotalPages ?? 0;
                    ViewBag.CurrentPage = result?.CurrentPage ?? 1;
                    ViewBag.SearchTerm = searchTerm;

                    return View(result?.Data ?? new List<KhachHangModels>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API danh sách khách hàng");
            }

            return View(new List<KhachHangModels>());
        }

        // 2. Trang chi tiết khách hàng - PHIÊN BẢN CHỐNG SẬP GIAO DIỆN
        public async Task<IActionResult> ChiTietKhachHang(int maKhachHang)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            var taskResponse = client.GetAsync($"{apiBaseUrl}/khachhang/{maKhachHang}");

            KhachHangModels? model = null;

            try
            {
                var response = await taskResponse;

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    // SỬA TẠI ĐÂY: Thêm JsonSerializerSettings để đọc JSON linh hoạt hơn
                    var settings = new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore
                    };

                    model = JsonConvert.DeserializeObject<KhachHangModels>(content, settings);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    TempData["Error"] = "Không tìm thấy khách hàng này.";
                    return RedirectToAction("DanhSachKhachHang");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối API khách hàng {ID}", maKhachHang);
                TempData["Error"] = "Hệ thống API đang bảo trì hoặc dữ liệu không hợp lệ.";
                return RedirectToAction("DanhSachKhachHang");
            }

            // XỬ LÝ DỮ LIỆU ĐƠN HÀNG ĐỂ VIEW HIỂN THỊ
            // Lấy dữ liệu từ thuộc tính DanhSachDonHang (đối tượng PagedResponse) đổ vào ViewBag để đồng bộ với View của bạn
            if (model?.DanhSachDonHang?.Data != null)
            {
                ViewBag.DonHangs = model.DanhSachDonHang.Data;
            }
            else
            {
                ViewBag.DonHangs = new List<DonHangModels>();
                ViewBag.MessageOrder = "Dữ liệu đơn hàng hiện không khả dụng.";
            }

            return View(model);
        }
    }
}