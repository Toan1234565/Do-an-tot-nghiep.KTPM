using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyDiaChi;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyDonHang;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhachHang;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhoBai;
using QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyDonHang
{
    public class QuanLyDonHang : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TmdtContext _context;
        private readonly ILogger<QuanLyDonHang> _logger;
        string apiBaseUrl = "https://localhost:7264/api/quanlydonhang";
        string apiDiaChi = "https://localhost:7149/api/quanlydiachi";
        string apiKhachHang = "https://localhost:7149/api/quanlykhachhang";
        string apiVung = "https://localhost:7149/api/quanlybangiavung";
        public QuanLyDonHang(IHttpClientFactory httpClientFactory, TmdtContext context,ILogger<QuanLyDonHang> logger)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _logger = logger;
        }
        public IActionResult Index()
        {
            return View();

        }
        public async Task<IActionResult> DanhSachDonHang(
            string? searchTerm,
            int page = 1,
            string trangthai = "Tất cả",
            DateTime? batday = null,
            DateTime? ketthuc = null)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            int pageSize = 15; // Thống nhất với cấu hình phân trang của API

            // 1. Chuẩn bị tham số truy vấn
            var queryParams = new Dictionary<string, string?>
            {
                ["searchTerm"] = searchTerm,
                ["page"] = page.ToString(),
                ["pageSize"] = pageSize.ToString(),
                ["trangthai"] = trangthai,
                ["batday"] = batday?.ToString("yyyy-MM-dd"),
                ["ketthuc"] = ketthuc?.ToString("yyyy-MM-dd")
            };

            // Loại bỏ các tham số null để URL gọn sạch
            var filteredParams = queryParams
                .Where(kv => kv.Value != null)
                .ToDictionary(kv => kv.Key, kv => kv.Value!);

            
            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{apiBaseUrl}/danhsachdonhang", filteredParams);

            try
            {
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // Giải mã JSON trả về từ API (Object chứa { totalItems, data })
                    var result = JsonConvert.DeserializeObject<ApiResponse>(jsonString);

                    // 2. Đưa dữ liệu vào ViewBag để View sử dụng
                    ViewBag.CurrentSearch = searchTerm;
                    ViewBag.CurrentStatus = trangthai;
                    ViewBag.BatDay = batday?.ToString("yyyy-MM-dd");
                    ViewBag.KetThuc = ketthuc?.ToString("yyyy-MM-dd");
                    ViewBag.CurrentPage = page;
                    ViewBag.PageSize = pageSize;

                    // Tính tổng số trang
                    int totalItems = result?.TotalItems ?? 0;
                    ViewBag.TotalItems = totalItems;
                    ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                    // Trả về danh sách đơn hàng cho Model của View
                    return View(result?.Data ?? new List<DonHangModels>());
                }
                else
                {
                    // Xử lý khi API trả về lỗi (404, 500, v.v.)
                    _logger.LogError($"API Error: {response.StatusCode}");
                    ModelState.AddModelError(string.Empty, "Không thể tải dữ liệu từ máy chủ.");
                    return View(new List<DonHangModels>());
                }
            }
            catch (Exception ex)
            {
                // Xử lý khi mất kết nối mạng hoặc lỗi hệ thống
                _logger.LogError(ex, "Lỗi kết nối API DanhSachDonHang");
                ModelState.AddModelError(string.Empty, "Hệ thống đang gặp sự cố kết nối. Vui lòng thử lại sau.");
                return View(new List<DonHangModels>());
            }
        }


        public async Task<IActionResult> ChiTietDonHang(int maDonHang)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrl}/thongtindonhang/{maDonHang}";

            try
            {
                // 1. Gọi API đơn hàng chính (Bắt buộc phải có)
                var response = await client.GetAsync(apiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return NotFound("Không tìm thấy thông tin đơn hàng.");
                }

                var content = await response.Content.ReadAsStringAsync();
                var donHang = JsonConvert.DeserializeObject<DonHangModels>(content);
                var kienHangDauTien = donHang?.KienHangs?.FirstOrDefault();

                // 2. Khởi tạo danh sách các Task gọi API địa chỉ
                var tasks = new List<Task>();

                // Task 1: Địa chỉ Giao
                if (donHang?.MaDiaChiLayHang > 0)
                {
                    tasks.Add(Task.Run(async () => {
                        try
                        {
                            var res = await client.GetAsync($"{apiDiaChi}/chitietdiachi/{donHang.MaDiaChiLayHang}");
                            if (res.IsSuccessStatusCode)
                            {
                                var json = await res.Content.ReadAsStringAsync();
                                ViewBag.DiaChiLayHang = JsonConvert.DeserializeObject<DiaChiModel>(json);
                            }
                        }
                        catch { /* Để mặc định null để View hiển thị "Đang tải" */ }
                    }));
                }
                // task 2: lay thogn tin khach hang
                tasks.Add(Task.Run(async () => {
                    try
                    {
                        var res = await client.GetAsync($"{apiKhachHang}/chi-tiet-khach-hang/{donHang?.MaKhachHang}");
                        if (res.IsSuccessStatusCode)
                        {
                            var json = await res.Content.ReadAsStringAsync();
                            ViewBag.ThongTinKhachHang = JsonConvert.DeserializeObject<KhachHangModels>(json);
                        }
                    }
                    catch { }
                }));
                // Task 3: Địa chỉ Lấy hang 
                if (donHang?.MaDiaChiLayHang > 0)
                {
                    tasks.Add(Task.Run(async () => {
                        try
                        {
                            var res = await client.GetAsync($"{apiDiaChi}/chitietdiachi/{donHang?.MaDiaChiNhanHang}");
                            if (res.IsSuccessStatusCode)
                            {
                                var json = await res.Content.ReadAsStringAsync();
                                ViewBag.DiaChiNhanHang = JsonConvert.DeserializeObject<DiaChiModel>(json);
                            }
                        }
                        catch { }
                    }));
                }

                // Task 3: Địa chỉ Kho Hiện Tại
                if (kienHangDauTien?.MaKhoHienTai > 0)
                {
                    tasks.Add(Task.Run(async () => {
                        try
                        {
                            var res = await client.GetAsync($"{apiDiaChi}/chitietdiachi/{kienHangDauTien.MaKhoHienTai}");
                            if (res.IsSuccessStatusCode)
                            {
                                var json = await res.Content.ReadAsStringAsync();
                                ViewBag.DiaChiKhoHienTai = JsonConvert.DeserializeObject<DiaChiModel>(json);
                            }
                        }
                        catch { }
                    }));
                }

                // Chờ tất cả thực hiện xong (Task nào lỗi kệ nó, các Task khác vẫn chạy)
                await Task.WhenAll(tasks);

                // 3. Tính khoảng cách (Chỉ tính khi cả 2 API đích thành công)
                if (ViewBag.DiaChiKhoHienTai != null && ViewBag.DiaChiGiao != null)
                {
                    var kho = (DiaChiModel)ViewBag.DiaChiKhoHienTai;
                    var giao = (DiaChiModel)ViewBag.DiaChiGiao;
                    var khachHang = (KhachHangModels)ViewBag.ThongTinKhachHang;
                    var diachinhanhang = (DiaChiModel)ViewBag.DiaChiLayHang;

                    double distance = GeoHelper.CalculateDistance(kho.ViDo ?? 0, kho.KinhDo ?? 0, giao.ViDo ?? 0, giao.KinhDo ?? 0);
                    ViewBag.DistanceRemaining = Math.Round(distance, 2);
                }

                return View(donHang);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống ChiTietDonHang");
                return View(new DonHangModels());
            }
        }
        // Hàm hỗ trợ tính khoảng cách (Nên để trong một class Helper)
        public static class GeoHelper
        {
            public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
            {
                var R = 6371; // Radius of the earth in km
                var dLat = ToRadians(lat2 - lat1);
                var dLon = ToRadians(lon2 - lon1);
                var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                        Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                        Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                return R * c;
            }
            private static double ToRadians(double deg) => deg * (Math.PI / 180);
        }


        // 1. Action GET để hiển thị trang tạo đơn hàng
        [HttpGet]
        public IActionResult TaoDonHang()
        {
            // Có thể khởi tạo một model mặc định nếu cần
            return View(new DonHangCreate());
        }

        // 2. Action POST để tiếp nhận dữ liệu từ View và gọi API thực tế
        [HttpPost]
        [ValidateAntiForgeryToken] // Bảo mật chống CSRF
        public async Task<IActionResult> TaoDonHang(DonHangCreate request)
        {
            // Kiểm tra tính hợp lệ của Model ở phía giao diện
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrl}/tao-moi"; // URL tới Server API xử lý logic

            try
            {
                // Gửi request sang Server API
                var response = await client.PostAsJsonAsync(apiUrl, request);

                if (response.IsSuccessStatusCode)
                {
                    // Đọc kết quả trả về từ API
                    var result = await response.Content.ReadAsStringAsync();
                    var successData = JsonConvert.DeserializeObject<dynamic>(result);

                    // Thông báo thành công (có thể dùng TempData để hiển thị Toastr/Alert ở View)
                    TempData["SuccessMessage"] = "Tạo đơn hàng thành công!";

                    // Chuyển hướng về trang danh sách hoặc chi tiết đơn hàng vừa tạo
                    return RedirectToAction("DanhSachDonHang");
                }
                else
                {
                    // Xử lý khi API trả về lỗi (ví dụ: 400 Bad Request, 500 Error)
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"API TaoDonHang Error: {errorContent}");

                    ModelState.AddModelError(string.Empty, $"Lỗi từ hệ thống: {errorContent}");
                    return View(request);
                }
            }
            catch (Exception ex)
            {
                // Xử lý khi mất kết nối tới API
                _logger.LogError(ex, "Lỗi kết nối khi gọi API TaoDonHang");
                ModelState.AddModelError(string.Empty, "Không thể kết nối tới máy chủ xử lý đơn hàng.");
                return View(request);
            }
        }
    }
}

