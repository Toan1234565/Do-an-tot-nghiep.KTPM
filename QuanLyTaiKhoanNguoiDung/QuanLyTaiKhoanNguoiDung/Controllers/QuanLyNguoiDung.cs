using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuanLyTaiKhoanNguoiDung.Controllers.QuanLykho;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyNhanVien;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyTaiXe;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyPhanQuyen;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyKhoBai;
using QuanLyTaiKhoanNguoiDung.Models12.SeverQuanLyKhachHang.QuanLyDiaChi;
using System.Security;
using System.Security.Claims;

namespace QuanLyTaiKhoanNguoiDung.Controllers
{
    public class QuanLyNguoiDung : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TmdtContext _context;
        private readonly PhanQuyenService _phanQuyen;

        private readonly ILogger<QuanLyNguoiDung> _logger; // Thêm dòng này
        public QuanLyNguoiDung(IHttpClientFactory httpClientFactory, TmdtContext context, ILogger<QuanLyNguoiDung> logger, PhanQuyenService phanQuyen)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _logger = logger;
            _phanQuyen = phanQuyen;
        }

        private readonly string apiBaseUrl = "https://localhost:7022/api/quanlynguoidung";
        private readonly string apiBaseUrlChucVu = "https://localhost:7022/api/quanlyphanquyen";
        private readonly string apiDiaChi = "https://localhost:7149/api/quanlydiachi";
        private readonly string apiBaseUrlLichSuVP = "https://localhost:7022/api/quanlyllichsuvipham";
        private readonly string apiKhoUrl = "https://localhost:7286/api/quanlykhobai/danhsachtenkho";

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

        public async Task<IActionResult> DanhSachNhanVien(string? searchTerm, int? maChucVu, int? maKho, int page = 1, bool trangthai = true)
        {
            // 1. Kiểm tra quyền hạn
            var currentUserId = GetCurrentUserId();
            var permission = await _phanQuyen.GetUserPermissionAsync(currentUserId);

            if (permission == null) return RedirectToAction("DangNhap", "QuanLyPhanQuyen");

            if (!permission.IsQuanLyTong && !permission.IsQuanLyKho)
                return StatusCode(403, new { message = "Bạn không có quyền truy cập." });

            // 2. Thiết lập bộ lọc
            int? filterMaKho = permission.IsQuanLyTong ? maKho : permission.MaKho;
            ViewBag.IsLockKho = !permission.IsQuanLyTong;

            ViewBag.CurrentSearch = searchTerm;
            ViewBag.CurrentMaChucVu = maChucVu;
            ViewBag.CurrentMaKho = filterMaKho;
            ViewBag.CurrentTrangThai = trangthai;
            ViewBag.UserPermission = permission;

            var client = _httpClientFactory.CreateClient("BypassSSL");

            // --- 3. LẤY DANH SÁCH KHO ---
            ViewBag.DanhSachKho = new List<TenKhobaiModels>();
            try
            {              
                var responseKho = await client.GetAsync(apiKhoUrl);
                if (responseKho.IsSuccessStatusCode)
                {
                    var contentKho = await responseKho.Content.ReadAsStringAsync();
                    var allKhos = JsonConvert.DeserializeObject<List<TenKhobaiModels>>(contentKho) ?? new List<TenKhobaiModels>();

                    ViewBag.DanhSachKho = permission.IsQuanLyTong
                        ? allKhos
                        : allKhos.Where(k => k.MaKho == permission.MaKho).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"API Kho hàng lỗi: {ex.Message}");
                ViewBag.ErrorKho = "Không thể tải danh sách kho.";
            }

            // --- 4. LẤY DANH SÁCH CHỨC VỤ ---
            ViewBag.DanhSachChucVu = new List<ChucVuModel>();
            try
            {
                // Chú ý: Đảm bảo apiBaseUrlChucVu đã được định nghĩa ở cấp Class hoặc lấy từ config
                var responseChucVu = await client.GetAsync($"{apiBaseUrlChucVu}/danhsachchucvu");
                if (responseChucVu.IsSuccessStatusCode)
                {
                    var contentChucVu = await responseChucVu.Content.ReadAsStringAsync();
                    ViewBag.DanhSachChucVu = JsonConvert.DeserializeObject<List<ChucVuModel>>(contentChucVu);
                }
            }
            catch (Exception ex) { _logger.LogWarning($"API Chức vụ lỗi: {ex.Message}"); }

            // --- 5. GỌI API NHÂN VIÊN (SỬA LỖI QUERY STRING) ---
            int pageIndex = page < 1 ? 1 : page;

            // Lọc bỏ các giá trị null để tránh lỗi AddQueryString
            var queryParams = new Dictionary<string, string?>();
            if (!string.IsNullOrEmpty(searchTerm)) queryParams.Add("searchTerm", searchTerm);
            if (maChucVu.HasValue) queryParams.Add("maChucVu", maChucVu.Value.ToString());
            if (filterMaKho.HasValue) queryParams.Add("maKho", filterMaKho.Value.ToString());
            queryParams.Add("page", pageIndex.ToString());
            queryParams.Add("trangthai", trangthai.ToString().ToLower());

            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{apiBaseUrl}/danhsachnguoidung", queryParams);

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonResult = Newtonsoft.Json.Linq.JObject.Parse(content);

                    // Ép kiểu an toàn
                    var dsNhanVien = jsonResult["data"]?.ToObject<List<NguoiDungListModel>>() ?? new List<NguoiDungListModel>();
                    ViewBag.TotalPages = jsonResult["totalPages"]?.Value<int>() ?? 0;
                    ViewBag.CurrentPage = jsonResult["currentPage"]?.Value<int>() ?? 1;

                    return View(dsNhanVien);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng khi tải danh sách nhân viên");
            }

            return View(new List<NguoiDungListModel>());
        }

        public async Task<IActionResult> ChiTietNhanVien(int maNhanVien)
        {
            if (maNhanVien <= 0) return RedirectToAction("DanhSachNhanVien", "QuanLyNguoiDung");

            var client = _httpClientFactory.CreateClient("BypassSSL");
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
            };

            // 1. Khởi tạo các Task (Đã sửa tên taskKho)
            var taskNhanVien = client.GetAsync($"{apiBaseUrl}/chitietnhanvien/{maNhanVien}");
            var taskViPham = client.GetAsync($"{apiBaseUrlLichSuVP}/LichSuViPham/{maNhanVien}");
            var taskChucVu = client.GetAsync($"{apiBaseUrlChucVu}/danhsachchucvu");
            var taskKho = client.GetAsync($"{apiKhoUrl}");

            try
            {
                // 2. Chờ tất cả API chạy song song (Bổ sung taskKho vào đây)
                // Lưu ý: GetAsync hiếm khi throw exception trừ khi lỗi mạng vật lý/DNS.
                // Nếu muốn an toàn tuyệt đối 100% không chết chùm, cần viết hàm wrapper bọc try-catch riêng cho từng GetAsync.
                await Task.WhenAll(taskNhanVien, taskViPham, taskChucVu, taskKho);

                // --- XỬ LÝ NHÂN VIÊN (Bắt buộc) ---
                var resNhanVien = await taskNhanVien;
                if (!resNhanVien.IsSuccessStatusCode)
                {
                    _logger.LogError("API Nhân viên trả về lỗi: {Code}", resNhanVien.StatusCode);
                    return View("Error");
                }

                var contentNhanVien = await resNhanVien.Content.ReadAsStringAsync();
                var nguoiDung = JsonConvert.DeserializeObject<NguoiDungDetailModel>(contentNhanVien, settings);

                // --- XỬ LÝ CHỨC VỤ (Không bắt buộc) ---
                try
                {
                    var resChucVu = await taskChucVu;
                    if (resChucVu.IsSuccessStatusCode)
                    {
                        var contentChucVu = await resChucVu.Content.ReadAsStringAsync();
                        ViewBag.DanhSachChucVu = JsonConvert.DeserializeObject<List<ChucVuModel>>(contentChucVu);
                    }
                    else
                    {
                        ViewBag.DanhSachChucVu = new List<ChucVuModel>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Lỗi xử lý dữ liệu chức vụ: {Msg}", ex.Message);
                    ViewBag.DanhSachChucVu = new List<ChucVuModel>();
                }

                // --- XỬ LÝ VI PHẠM (Không bắt buộc) ---
                try
                {
                    var resViPham = await taskViPham;
                    if (resViPham.IsSuccessStatusCode)
                    {
                        var contentVP = await resViPham.Content.ReadAsStringAsync();
                        ViewBag.LichSuViPham = JsonConvert.DeserializeObject<List<LichSuViPhamModels>>(contentVP);
                    }
                    else
                    {
                        ViewBag.LichSuViPham = new List<LichSuViPhamModels>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Lỗi xử lý lịch sử vi phạm: {Msg}", ex.Message);
                    ViewBag.LichSuViPham = new List<LichSuViPhamModels>();
                }

                // --- XỬ LÝ KHO HÀNG ---
                try
                {
                    var resKho = await taskKho;
                    if (resKho.IsSuccessStatusCode)
                    {
                        var jsonString = await resKho.Content.ReadAsStringAsync();

                        // Cần Deserialize để biến chuỗi JSON thành List Object
                        var listKho = JsonConvert.DeserializeObject<List<TenKhobaiModels>>(jsonString);

                        ViewBag.DanhSachKho = listKho;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("API Kho hàng lỗi: {Msg}", ex.Message);
                    ViewBag.DanhSachKho = new List<TenKhobaiModels>(); // Trả về list rỗng để View không bị lỗi
                    ViewBag.ErrorKho = "Không thể tải danh sách kho.";
                }

                // --- XỬ LÝ ĐỊA CHỈ (Phụ thuộc vào nhân viên, chạy tuần tự sau khi có nguoiDung) ---
                if (nguoiDung?.MaDiaChi > 0)
                {
                    try
                    {
                        var resDiaChi = await client.GetAsync($"{apiDiaChi}/chitietdiachi/{nguoiDung.MaDiaChi}");
                        if (resDiaChi.IsSuccessStatusCode)
                        {
                            var contentDiaChi = await resDiaChi.Content.ReadAsStringAsync();
                            ViewBag.DiaChi = JsonConvert.DeserializeObject<DiaChiModel>(contentDiaChi);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Lỗi load địa chỉ: {Msg}", ex.Message);
                    }
                }

                return View(nguoiDung);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống nghiêm trọng khi tải chi tiết nhân viên {Id}", maNhanVien);
                ViewBag.Error = "Đã xảy ra lỗi không mong muốn khi kết nối tới các dịch vụ. Vui lòng thử lại sau.";
                return View(new NguoiDungDetailModel());
            }
        }
    }
}