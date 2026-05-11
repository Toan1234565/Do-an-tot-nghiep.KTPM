using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models12;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyNhanVien;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyPhanQuyen;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyKhoBai;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyPhuongTien;
using System.Security.Claims;
namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLykho
{
    public class QuanLyPhuongTien : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TmdtContext _context;
        private readonly PhanQuyenService _phanQuyen;

        private readonly ILogger<QuanLyPhuongTien> _logger; // Thêm dòng này
        public QuanLyPhuongTien(IHttpClientFactory httpClientFactory, TmdtContext context, ILogger<QuanLyPhuongTien> logger, PhanQuyenService phanQuyen)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _logger = logger;
            _phanQuyen = phanQuyen;
        }

        public IActionResult Index()
        {
            return View();
        }

        string apiBaseUrl = "https://localhost:7286/api/quanlyxe";
        string apiBaseTenKho = "https://localhost:7286/api/quanlykhobai";
        string apiBaseDinhMuc = "https://localhost:7286/api/quanlydinhmuc";
        string apiNguoiDung = "https://localhost:7022/api/quanlynguoidung";

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

        public async Task<IActionResult> DanhSachPhuongTien(
            string? searchTerm,
            string? maLoaiXe,
            int page = 1,
            string status = "Tất cả",
            int? maKho = null,
            string trangThaiDangKiem = "Tất cả")
        {
            // 1. Kiểm tra quyền của bậc quân vương
            var permission = await _phanQuyen.GetUserPermissionAsync(GetCurrentUserId());
            if (permission == null) return RedirectToAction("DangNhap", "QuanLyPhanQuyen");

            int? filterMaKho = permission.IsQuanLyTong ? maKho : (permission.IsQuanLyKho ? permission.MaKho : null);
            if (filterMaKho == null && !permission.IsQuanLyTong) return Forbid();

            // Khởi tạo các giá trị hỗ trợ và túi chứa lỗi
            var apiErrors = new List<string>();
            ViewBag.IsLockKho = !permission.IsQuanLyTong;
            ViewBag.CurrentMaKho = filterMaKho;
            ViewBag.DanhSachKho = new List<TenKhobaiModels>();
            ViewBag.DanhSachLoaiXe = new List<LoaiXeModels>();

            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 2. Triệu hồi dữ liệu hỗ trợ (Kho & Loại Xe)
            try
            {
                var responseKho = await client.GetAsync($"{apiBaseTenKho}/danhsachtenkho");
                if (responseKho.IsSuccessStatusCode)
                {
                    var content = await responseKho.Content.ReadAsStringAsync();
                    var allKhos = JsonConvert.DeserializeObject<List<TenKhobaiModels>>(content) ?? new List<TenKhobaiModels>();
                    ViewBag.DanhSachKho = permission.IsQuanLyTong ? allKhos : allKhos.Where(k => k.MaKho == permission.MaKho).ToList();
                }
                else { apiErrors.Add($"API Kho trả lỗi: {responseKho.StatusCode}"); }

                var responseLoaiXe = await client.GetAsync($"{apiBaseUrl}/danhsachloaixe");
                if (responseLoaiXe.IsSuccessStatusCode)
                {
                    var content = await responseLoaiXe.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(content); // Giả định API này bọc trong object 'data'
                    ViewBag.DanhSachLoaiXe = result?.data?.ToObject<List<LoaiXeModels>>() ?? new List<LoaiXeModels>();
                }
                else { apiErrors.Add($"API Loại Xe trả lỗi: {responseLoaiXe.StatusCode}"); }
            }
            catch (Exception ex) { apiErrors.Add($"Lỗi kết nối dữ liệu hỗ trợ: {ex.Message}"); }

            // 3. Chuẩn bị tham số và gọi API Danh sách xe (Trục chính)
            var queryParams = new Dictionary<string, string?>
            {
                ["searchTerm"] = searchTerm,
                ["maLoaiXe"] = maLoaiXe,
                ["page"] = page.ToString(),
                ["status"] = status,
                ["trangthaidangkiem"] = trangThaiDangKiem,
                ["maKho"] = filterMaKho?.ToString()
            };
            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{apiBaseUrl}/danhsachxe", queryParams);

            try
            {
                var xeRes = await client.GetAsync(apiUrl);
                if (xeRes.IsSuccessStatusCode)
                {
                    var content = await xeRes.Content.ReadAsStringAsync();

                    // ÉP KIỂU TƯỜNG MINH: Tránh lỗi dynamic lambda
                    var jsonResult = JsonConvert.DeserializeObject<PaginationResult<PhuongTienListModel>>(content);

                    ViewBag.TotalPages = jsonResult?.TotalPages ?? 0;
                    ViewBag.CurrentPage = jsonResult?.CurrentPage ?? 1;
                    ViewBag.CurrentMaLoaiXe = maLoaiXe;
                    ViewBag.CurrentSearch = searchTerm;
                    ViewBag.CurrentStatus = status;
                    ViewBag.CurrentTrangThaiDangKiem = trangThaiDangKiem;

                    var dsPhuongTien = jsonResult?.Data ?? new List<PhuongTienListModel>();

                    // --- TRUY VẤN TÊN NHÂN VIÊN SONG SONG (Đã sửa lỗi Lambda) ---
                    var tasks = dsPhuongTien
                        .Where(x => x.MaNguoiDung.HasValue)
                        .Select(async item =>
                        {
                            try
                            {
                                var responseNV = await client.GetAsync($"{apiNguoiDung}/lay-ten-nhan-vien/{item.MaNguoiDung}");
                                if (responseNV.IsSuccessStatusCode)
                                {
                                    var nvContent = await responseNV.Content.ReadAsStringAsync();
                                    var nvInfo = JsonConvert.DeserializeObject<TenNhanVienModel>(nvContent);
                                    item.TenNguoiPhuTrach = nvInfo?.TenTaiXeThucHien ?? "Không xác định";
                                }
                                else { item.TenNguoiPhuTrach = "Lỗi API tên"; }
                            }
                            catch { item.TenNguoiPhuTrach = "Mất kết nối NV"; }
                        }).ToList();

                    // Chờ tất cả thám tử thu thập tên xong
                    await Task.WhenAll(tasks);

                    ViewBag.ApiWarning = apiErrors; // Gửi cảnh báo lỗi API phụ ra View
                    return View(dsPhuongTien);
                }
                else
                {
                    ViewBag.Error = $"API Danh sách xe chính báo lỗi: {xeRes.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng tại DanhSachPhuongTien");
                ViewBag.Error = "Không thể kết nối với máy chủ dữ liệu xe.";
            }

            ViewBag.ApiWarning = apiErrors;
            return View(new List<PhuongTienListModel>());
        }

        public async Task<IActionResult> ChiTietPhuongTien(int maPhuongTien)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrl}/chitietphuongtien/{maPhuongTien}";
            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var nguoiDung = JsonConvert.DeserializeObject<PhuongTienDetailModel>(content);
                    return View(nguoiDung);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;

            }
            return View(new List<PhuongTienDetailModel>());
        }

        public async Task<IActionResult> LichTrinhBaoTri(
            [FromQuery] int soNgayBaoTri = 15,
            [FromQuery] int soNgayDangKiem = 30,
            [FromQuery] int soNgayPhiDb = 30,
            [FromQuery] int pageBT = 1,
            [FromQuery] int pageDK = 1,
            [FromQuery] int pagePDB = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            int pageSize = 10;

            // Định nghĩa URL
            string apiBaoTriUrl = $"{apiBaseUrl}/xe-sap-den-han-bao-tri?soNgayCanhBao={soNgayBaoTri}&page={pageBT}&pageSize={pageSize}";
            string apiDangKiemUrl = $"{apiBaseUrl}/xe-sap-het-han-dang-kiem?soNgayCanhBao={soNgayDangKiem}&page={pageDK}&pageSize={pageSize}";
            string apiPhiDuongBoUrl = $"{apiBaseUrl}/xe-sap-het-han-phi-duong-bo?soNgayCanhBao={soNgayPhiDb}&page={pagePDB}&pageSize={pageSize}";

            try
            {
                // Gọi đồng thời 3 API
                var taskBT = client.GetAsync(apiBaoTriUrl);
                var taskDK = client.GetAsync(apiDangKiemUrl);
                var taskPDB = client.GetAsync(apiPhiDuongBoUrl);

                await Task.WhenAll(taskBT, taskDK, taskPDB);

                // Đọc và xử lý dữ liệu (Sử dụng hàm Helper bên dưới)
                var dataBT = await ProcessApiResponse(taskBT.Result);
                var dataDK = await ProcessApiResponse(taskDK.Result);
                var dataPDB = await ProcessApiResponse(taskPDB.Result);

                // Gán dữ liệu cho Bảo trì
                ViewBag.DsSapBaoTri = dataBT.Data;
                ViewBag.TotalItemsBT = dataBT.TotalItems;
                ViewBag.TotalPagesBT = (int)Math.Ceiling((double)dataBT.TotalItems / pageSize);

                // Gán dữ liệu cho Đăng kiểm
                ViewBag.DsSapHetDangKiem = dataDK.Data;
                ViewBag.TotalItemsDK = dataDK.TotalItems;
                ViewBag.TotalPagesDK = (int)Math.Ceiling((double)dataDK.TotalItems / pageSize);

                // Gán dữ liệu cho Phí đường bộ
                ViewBag.DsSapHetPhiDuongBo = dataPDB.Data;
                ViewBag.TotalItemsPDB = dataPDB.TotalItems;
                ViewBag.TotalPagesPDB = (int)Math.Ceiling((double)dataPDB.TotalItems / pageSize);

                // Lưu tham số filter để hiển thị lại trên Form
                ViewBag.CurrentPageBT = pageBT;
                ViewBag.CurrentPageDK = pageDK;
                ViewBag.CurrentPagePDB = pagePDB;
                ViewBag.SoNgayBaoTri = soNgayBaoTri;
                ViewBag.SoNgayDangKiem = soNgayDangKiem;
                ViewBag.SoNgayPhiDb = soNgayPhiDb;

                return View();
            }
            catch (Exception ex)
            {
                SetDefaultViewBagValues();
                ViewBag.Error = "Lỗi kết nối hệ thống: " + ex.Message;
                return View();
            }
        }

        // Hàm Helper để tránh lặp code xử lý JSON
        private async Task<(List<BaoTriModel> Data, int TotalItems)> ProcessApiResponse(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                return (new List<BaoTriModel>(), 0);

            var content = await response.Content.ReadAsStringAsync();
            // Sử dụng dynamic để đọc linh hoạt hoặc tạo một class ApiResponse chung
            var json = JsonConvert.DeserializeObject<dynamic>(content);

            var data = json?.data?.ToObject<List<BaoTriModel>>() ?? new List<BaoTriModel>();
            int total = (int)(json?.totalItems ?? 0);

            return (data, total);
        }

        private void SetDefaultViewBagValues()
        {
            ViewBag.DsSapBaoTri = new List<BaoTriModel>();
            ViewBag.DsSapHetDangKiem = new List<BaoTriModel>();
            ViewBag.DsSapHetPhiDuongBo = new List<BaoTriModel>();
            ViewBag.TotalItemsBT = 0; ViewBag.TotalItemsDK = 0; ViewBag.TotalItemsPDB = 0;
            ViewBag.TotalPagesBT = 0; ViewBag.TotalPagesDK = 0; ViewBag.TotalPagesPDB = 0;
        }

        public async Task<IActionResult> DanhSachDinhMuc([FromQuery] int? maLoaiXe, [FromQuery] int page = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 1. Chuẩn bị các URL API
            // API lấy danh sách định mức (đã group) có kèm tham số lọc
            string apiDinhMucUrl = $"{apiBaseDinhMuc}/danhsachdinhmuc?maLoaiXe={maLoaiXe}&page={page}";
            // API lấy danh sách loại xe cho dropdown
            string apiLoaiXeUrl = $"{apiBaseUrl}/danhsachloaixe";

            try
            {
                // 2. Gọi song song cả 2 API để tối ưu tốc độ
                var taskDinhMuc = client.GetAsync(apiDinhMucUrl);
                var taskLoaiXe = client.GetAsync(apiLoaiXeUrl);

                await Task.WhenAll(taskDinhMuc, taskLoaiXe);

                // 3. Xử lý dữ liệu Danh sách loại xe (Dropdown)
                var resLoaiXe = await taskLoaiXe;
                if (resLoaiXe.IsSuccessStatusCode)
                {
                    var contentLoai = await resLoaiXe.Content.ReadAsStringAsync();
                    // Nếu API trả về JObject có trường "data"
                    var jsonLoai = Newtonsoft.Json.Linq.JObject.Parse(contentLoai);
                    ViewBag.DanhSachLoaiXe = jsonLoai["data"]?.ToObject<List<LoaiXeModels>>()
                                             ?? new List<LoaiXeModels>();
                }

                // 4. Xử lý dữ liệu Định mức (Dữ liệu chính của bảng)
                var resDinhMuc = await taskDinhMuc;
                if (resDinhMuc.IsSuccessStatusCode)
                {
                    var contentDinhMuc = await resDinhMuc.Content.ReadAsStringAsync();

                    // Deserialize vào List<LoaiXeModels> vì API trả về cấu trúc Grouped (LoaiXe + List HangMuc)
                    var dsDinhMucGrouped = JsonConvert.DeserializeObject<List<LoaiXeModels>>(contentDinhMuc);

                    // Lưu các giá trị hiện tại vào ViewBag để giữ trạng thái trên View
                    ViewBag.CurrentPage = page;
                    ViewBag.CurrentMaLoaiXe = maLoaiXe;

                    return View(dsDinhMucGrouped);
                }
                else
                {
                    _logger.LogWarning($"API Định mức trả về lỗi: {resDinhMuc.StatusCode}");
                    return View(new List<LoaiXeModels>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực hiện lọc định mức theo loại xe");
                ViewBag.Error = "Không thể kết nối đến máy chủ: " + ex.Message;
                return View(new List<LoaiXeModels>());
            }
        }
    }
    }