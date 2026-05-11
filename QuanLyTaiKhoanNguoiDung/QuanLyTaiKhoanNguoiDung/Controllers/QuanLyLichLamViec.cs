using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyLichLamViec;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyPhanQuyen;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyKhoBai;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace QuanLyTaiKhoanNguoiDung.Controllers
{
    public class QuanLyLichLamViec : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyLichLamViec> _logger;
        // Tốt nhất nên để vào appsettings.json, nhưng ở đây dùng biến hằng cho demo
        private readonly string apiBaseUrl = "https://localhost:7022/api/quanlylichlamviec";
        private readonly PhanQuyenService _phanQuyen;

        public QuanLyLichLamViec(IHttpClientFactory httpClientFactory, ILogger<QuanLyLichLamViec> logger, PhanQuyenService phanQuyen)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _phanQuyen = phanQuyen;
        }

        private int? GetCurrentUserId()
        {
            // 1. Thử lấy từ Claims (Cookie Authentication)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }

            // 2. Dự phòng: Thử lấy từ Session (Nếu Cookie bị lỗi nhưng Session còn)
            var sessionUserId = HttpContext.Session.GetString("MaNguoiDung");
            if (!string.IsNullOrEmpty(sessionUserId) && int.TryParse(sessionUserId, out int sUserId))
            {
                return sUserId;
            }

            return null;
        }

        public async Task<IActionResult> DanhSachLichLamViec(DateOnly? thoigian, int? maKho, string? trangthai = "Đã duyệt", int page = 1)
        {
            // 1. Kiểm tra quyền hạn
            var permission = await _phanQuyen.GetUserPermissionAsync(GetCurrentUserId());
            if (permission == null)
                return RedirectToAction("DangNhap", "QuanLyPhanQuyen");

            // 2. Xác định MaKho được phép truy vấn
            int? filterMaKho = permission.IsQuanLyTong ? maKho : permission.MaKho;
            ViewBag.IsLockKho = !permission.IsQuanLyTong;

            // 3. Giữ trạng thái Form
            DateOnly selectedDate = thoigian ?? DateOnly.FromDateTime(DateTime.Now);
            ViewBag.CurrentThoiGian = selectedDate.ToString("yyyy-MM-dd");
            ViewBag.CurrentMaKho = filterMaKho;
            ViewBag.CurrentTrangThai = trangthai;
            ViewBag.UserPermission = permission;

            // Khởi tạo danh sách kho rỗng để tránh lỗi Null ở View
            ViewBag.DanhSachKho = new List<TenKhobaiModels>();

            var client = _httpClientFactory.CreateClient("BypassSSL");

            // ---------------------------------------------------------
            // 4. GỌI API KHO HÀNG (Cố gắng lấy cho Dropdown, lỗi thì bỏ qua)
            // ---------------------------------------------------------
            try
            {
                string apiKhoUrl = "https://localhost:7286/api/quanlykhobai/danhsachtenkho";
                var responseKho = await client.GetAsync(apiKhoUrl);
                if (responseKho.IsSuccessStatusCode)
                {
                    var contentKho = await responseKho.Content.ReadAsStringAsync();
                    var allKhos = JsonConvert.DeserializeObject<List<TenKhobaiModels>>(contentKho) ?? new List<TenKhobaiModels>();

                    if (permission.IsQuanLyTong)
                        ViewBag.DanhSachKho = allKhos;
                    else
                        ViewBag.DanhSachKho = allKhos.Where(k => k.MaKho == permission.MaKho).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"API Kho hàng lỗi (Lịch làm việc): {ex.Message}");
                // Vẫn chạy tiếp để lấy lịch làm việc bên dưới
            }

            // ---------------------------------------------------------
            // 5. GỌI API LỊCH LÀM VIỆC (Dữ liệu chính)
            // ---------------------------------------------------------
            int pageIndex = page < 1 ? 1 : page;
            var queryParams = new Dictionary<string, string?>
            {
                ["thoigian"] = selectedDate.ToString("yyyy-MM-dd"),
                ["maKho"] = filterMaKho?.ToString(),
                ["page"] = pageIndex.ToString(),
                ["trangthai"] = trangthai
            };

            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{apiBaseUrl}/danhsachlichlamviec", queryParams);

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonResult = Newtonsoft.Json.Linq.JObject.Parse(content);

                    var dsLich = jsonResult["data"]?.ToObject<List<DangKyCaTrucModels>>() ?? new List<DangKyCaTrucModels>();

                    ViewBag.TotalPages = (int)(jsonResult["totalPages"] ?? 0);
                    ViewBag.CurrentPage = (int)(jsonResult["currentPage"] ?? 1);
                    ViewBag.QueryDate = jsonResult["queryDate"]?.ToString();

                    return View(dsLich);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    ViewBag.Error = "Bạn không có quyền truy cập dữ liệu của kho này.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối API lịch làm việc");
                ViewBag.Error = "Lỗi kết nối máy chủ dữ liệu lịch trực.";
            }

            // Trả về view với danh sách rỗng nếu có lỗi ở API chính
            return View(new List<DangKyCaTrucModels>());
        }
    }
}