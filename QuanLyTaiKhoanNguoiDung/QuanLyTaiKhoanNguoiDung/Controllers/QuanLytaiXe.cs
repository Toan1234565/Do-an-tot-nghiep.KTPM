using MailKit.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12; // Chứa PaginationResult
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyNhanVien;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyTaiXe;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyPhanQuyen;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyKhoBai;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace QuanLyTaiKhoanNguoiDung.Controllers
{
    public class QuanLytaiXe : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLytaiXe> _logger;
        private readonly string apiBaseUrl = "https://localhost:7022/api/quanlytaixe";
        private readonly string apiBaseUrlNhanVien = "https://localhost:7022/api/quanlynguoidung";
        private readonly string apiBaseUrlLichSuVP = "https://localhost:7022/api/quanlyllichsuvipham";
        private readonly TmdtContext _context;
        private readonly PhanQuyenService _phanQuyen;     

        public QuanLytaiXe(IHttpClientFactory httpClientFactory, ILogger<QuanLytaiXe> logger, TmdtContext context, PhanQuyenService phanQuyen)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _context = context;
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

        public async Task<IActionResult> DanhSachTaiXe(string? search, string? loaiBang, int? maKho, string? trangthaihoatdong, string? sortBy = "MaNguoiDung", bool isDescending = true, int page = 1, bool trangthai = true)
        {
            // 1. Kiểm tra quyền hạn
            var permission = await _phanQuyen.GetUserPermissionAsync(GetCurrentUserId());

            if (permission == null)
                return RedirectToAction("DangNhap", "QuanLyPhanQuyen");

            if (!permission.IsQuanLyTong && !permission.IsQuanLyKho)
                return StatusCode(403, new { message = "Bạn không có quyền truy cập danh sách tài xế." });

            // 2. Xác định MaKho được phép lọc
            int? filterMaKho = permission.IsQuanLyTong ? maKho : permission.MaKho;
            ViewBag.IsLockKho = !permission.IsQuanLyTong;

            // 3. Thiết lập ViewBag giữ trạng thái Form
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentLoaiBang = loaiBang;
            ViewBag.CurrentSortBy = sortBy;
            ViewBag.CurrentMaKho = filterMaKho;
            ViewBag.IsDescending = isDescending;
            ViewBag.CurrentTrangThai = trangthai;
            ViewBag.CurrentTrangThaiHoatDong = trangthaihoatdong;

            // KHỞI TẠO MẶC ĐỊNH để tránh lỗi Null Reference tại View
            ViewBag.DanhSachKho = new List<TenKhobaiModels>();

            var client = _httpClientFactory.CreateClient("BypassSSL");

            // ---------------------------------------------------------
            // 4. GỌI API KHO HÀNG (Luồng phụ - Lỗi vẫn chạy tiếp)
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
                // Chỉ log cảnh báo, không làm gián đoạn luồng chính
                _logger.LogWarning($"API Kho hàng (Tài xế) không phản hồi: {ex.Message}");
            }

            // ---------------------------------------------------------
            // 5. GỌI API DANH SÁCH TÀI XẾ (Luồng chính)
            // ---------------------------------------------------------
            int pageIndex = page < 1 ? 1 : page;
            var queryParams = new Dictionary<string, string?>
            {
                ["search"] = search,
                ["loaiBang"] = loaiBang,
                ["maKho"] = filterMaKho?.ToString(),
                ["sortBy"] = sortBy,
                ["isDescending"] = isDescending.ToString().ToLower(),
                ["page"] = pageIndex.ToString(),
                ["trangthaihoatdong"] = trangthaihoatdong,
                ["trangthai"] = trangthai.ToString().ToLower()
            };

            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{apiBaseUrl}/danhsachtaixe", queryParams);

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonResult = Newtonsoft.Json.Linq.JObject.Parse(content);

                    var dsTaiXe = jsonResult["data"]?.ToObject<List<TaiXeListModel>>() ?? new List<TaiXeListModel>();

                    ViewBag.TotalPages = (int)(jsonResult["totalPages"] ?? 0);
                    ViewBag.CurrentPage = (int)(jsonResult["currentPage"] ?? 1);

                    return View(dsTaiXe);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối API danh sách tài xế");
                ViewBag.Error = "Không thể kết nối với máy chủ quản lý tài xế.";
            }

            return View(new List<TaiXeListModel>());
        }      
    }
}