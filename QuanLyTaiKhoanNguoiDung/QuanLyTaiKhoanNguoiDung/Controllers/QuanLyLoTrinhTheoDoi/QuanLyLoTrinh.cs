using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyLoTrinh.QuanLyLoTrinhTheoDoi;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyNhanVien;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyPhanQuyen;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyKhoBai;
using QuanLyTaiKhoanNguoiDung.Models12.SeverQuanLyKhachHang.QuanLyDiaChi;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyLoTrinhTheoDoi
{
    public class QuanLyLoTrinh : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyLoTrinh> _logger;
        private readonly PhanQuyenService _phanQuyen;
        // Cấu hình URL tập trung - Port 7097 là Server Lộ trình
        private const string ApiBaseUrl = "https://localhost:7097/api/dieuphoilotrinh";
        private const string ApiBaseUrlV2 = "https://localhost:7022/api/quanlynguoidung";
        private const string apiDiaChi = "https://localhost:7149/api/quanlydiachi";
        private const string apiDonHang = "https://localhost:7264/api/quanlydonhang";
        private const string apiKhachHang = "https://localhost:7149/api/quanlykhachhang";
        private const string apiKho = "https://localhost:7286/api/quanlykho";
        public QuanLyLoTrinh(IHttpClientFactory httpClientFactory, ILogger<QuanLyLoTrinh> logger, PhanQuyenService phanQuyen)
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

        public async Task<IActionResult> QuanLyLoTrinhTheoDoi(
            DateTime? batDau,
            DateTime? ketThuc,
            string trangThai = "Chờ khởi hành",
            int? maKho = null,
            int page = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            var dsLoTrinh = new List<LoTrinhModels>();

            // =========================================================================
            // 1. LẤY DANH SÁCH KHO BÃI (Đổ vào Dropdown lọc)


            var permission = await _phanQuyen.GetUserPermissionAsync(GetCurrentUserId());

            if (permission == null)
                return RedirectToAction("DangNhap", "QuanLyPhanQuyen");

            if (!permission.IsQuanLyTong && !permission.IsQuanLyKho)
                return StatusCode(403, new { message = "Bạn không có quyền truy cập danh sách tài xế." });

            // 2. Xác định MaKho được phép lọc
            int? filterMaKho = permission.IsQuanLyTong ? maKho : permission.MaKho;
            ViewBag.IsLockKho = !permission.IsQuanLyTong;

            try
            {
                string apiKhoUrl = "https://localhost:7286/api/quanlykhobai/danhsachtenkho";
                var responseKho = await client.GetAsync(apiKhoUrl);
                if (responseKho.IsSuccessStatusCode)
                {
                    var contentKho = await responseKho.Content.ReadAsStringAsync();
                    var allKhos = JsonConvert.DeserializeObject<List<TenKhobaiModels>>(contentKho) ?? new List<TenKhobaiModels>();
                    ViewBag.DanhSachKho = allKhos;
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

            // =========================================================================
            // 2. XÂY DỰNG QUERY STRING CHO DANH SÁCH LỘ TRÌNH
            // =========================================================================
            var queryParams = new Dictionary<string, string?>
            {
                ["trangThai"] = trangThai,
                ["page"] = page.ToString(),
                ["pageSize"] = "10" // Khớp với pageSize của Server
            };

            if (maKho.HasValue && maKho > 0) queryParams["maKho"] = maKho.Value.ToString();
            if (batDau.HasValue) queryParams["batdau"] = batDau.Value.ToString("yyyy-MM-dd");
            if (ketThuc.HasValue) queryParams["ketthuc"] = ketThuc.Value.ToString("yyyy-MM-dd");

            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{ApiBaseUrl}/danhsachlotrinh", queryParams);

            // =========================================================================
            // 3. GỌI API LẤY DANH SÁCH LỘ TRÌNH (AGGREGATOR DATA)
            // =========================================================================
            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonResult = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(content);

                    if (jsonResult != null)
                    {
                        // Sửa lỗi Case-sensitive: Map đúng các key VIẾT HOA từ JSON
                        ViewBag.TotalItems = jsonResult["TotalItems"]?.Value<int>() ?? 0;
                        ViewBag.TotalPages = jsonResult["TotalPages"]?.Value<int>() ?? 1;
                        ViewBag.CurrentPage = jsonResult["CurrentPage"]?.Value<int>() ?? 1;

                        // Lấy mảng dữ liệu từ Key "Data"
                        var itemsToken = jsonResult["Data"];
                        if (itemsToken != null)
                        {
                            dsLoTrinh = itemsToken.ToObject<List<LoTrinhModels>>();
                        }
                    }
                }
                else
                {
                    ViewBag.Error = "Không thể lấy dữ liệu từ máy chủ điều phối.";
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Lỗi kết nối hệ thống: " + ex.Message;
                _logger.LogError(ex, "Lỗi khi gọi API danh sách lộ trình");
            }

            // =========================================================================
            // 4. TRẢ DỮ LIỆU VỀ VIEW
            // =========================================================================
            ViewBag.CurrentTrangThai = trangThai;
            ViewBag.CurrentMaKho = maKho;
            ViewBag.CurrentBatDau = batDau?.ToString("yyyy-MM-dd");
            ViewBag.CurrentKetThuc = ketThuc?.ToString("yyyy-MM-dd");

            return View(dsLoTrinh);
        }

        public async Task<IActionResult> ChiTietLoTrinhTheoDoi(int maLoTrinh)
        {
            // 1. Khởi tạo Client
            var client = _httpClientFactory.CreateClient("BypassSSL");

            try
            {
                // 2. Gọi API tổng hợp từ Backend
                // Backend của bạn hiện đã xử lý gom: Thông tin xe, Điểm dừng (có tọa độ), Kiện hàng (có thông tin đơn)
                var response = await client.GetAsync($"{ApiBaseUrl}/chi-tiet-lo-trinh/{maLoTrinh}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("API Lộ trình {Ma} trả về lỗi: {Code} - Nội dung: {Msg}", maLoTrinh, response.StatusCode, errorContent);

                    ViewBag.Error = $"Không tìm thấy dữ liệu cho lộ trình số {maLoTrinh}.";
                    return View("Error"); // Hoặc trả về View chính với Model null
                }

                // 3. Đọc và giải mã dữ liệu
                var content = await response.Content.ReadAsStringAsync();
                var ctLoTrinh = JsonConvert.DeserializeObject<ChiTietLoTrinhModels>(content);

                if (ctLoTrinh == null)
                {
                    ViewBag.Error = "Dữ liệu lộ trình nhận được bị trống.";
                    return View(null);
                }

                // 4. Xử lý dữ liệu bổ trợ cho View (Tài xế, Thống kê nhanh)
                // Lấy tên tài xế (Backend đã gán vào MaPtTxNavigation hoặc TenTaiXe tùy theo class Model của bạn)
                ViewBag.TenTaiXe = ctLoTrinh.TenTaiXe ?? "Tài xế đang cập nhật";

                // Tính toán nhanh số điểm đã hoàn thành (nếu cần hiển thị Progress Bar)
                ViewBag.DiemDaDen = ctLoTrinh.DiemDungs?.Count(d => d.ThoiGianDenThucTe != null) ?? 0;
                ViewBag.TongDiem = ctLoTrinh.TongSoDiemDung;

                // 5. Trả về View cùng Model đã đầy đủ thông tin
                return View(ctLoTrinh);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Lỗi kết nối HTTP khi gọi lộ trình {Ma}", maLoTrinh);
                ViewBag.Error = "Không thể kết nối đến máy chủ API. Vui lòng thử lại sau.";
                return View(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi xử lý lộ trình {Ma}", maLoTrinh);
                ViewBag.Error = "Đã xảy ra lỗi không xác định trong quá trình tải dữ liệu.";
                return View(null);
            }
        }

        // Hàm bổ trợ gọi API an toàn: Tự catch lỗi để không làm sập luồng chính
        private async Task<T?> GetApiDataAsync<T>(HttpClient client, string url, string apiName) where T : class
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (typeof(T) == typeof(Newtonsoft.Json.Linq.JObject))
                        return Newtonsoft.Json.Linq.JObject.Parse(content) as T;

                    return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(content);
                }
                _logger.LogWarning($"{apiName} trả về mã lỗi: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi {ApiName} tại {Url}", apiName, url);
            }
            return null;
        }
    }
}