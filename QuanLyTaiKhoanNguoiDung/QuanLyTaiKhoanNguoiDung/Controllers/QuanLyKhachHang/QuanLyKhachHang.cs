using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models12;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyDonHang;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhachHang;
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

            // Khởi tạo các Task gọi song song
            var taskKH = client.GetAsync($"{apiBaseUrl}/khachhang/{maKhachHang}");
            var taskDonHang = client.GetAsync($"{apiBaseUrlDonHang}/danhsachdonhangtheokhachhang/{maKhachHang}?pageSize=10");

            // Biến chứa dữ liệu
            KhachHangModels? khachHangModel = null;
            var listDonHang = new List<DonHangModels>();

            // CHỜ CẢ 2 API NHƯNG KHÔNG ĐỂ CRASH NẾU 1 BÊN LỖI
            try
            {
                // Sử dụng Task.WhenAll nhưng bao bọc cẩn thận
                await Task.WhenAll(taskKH, taskDonHang);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Có lỗi kết nối khi gọi các API cho khách hàng {ID}", maKhachHang);
                // Không return ở đây, cứ để code chạy tiếp xuống dưới để kiểm tra từng response
            }

            // --- XỬ LÝ DỮ LIỆU KHÁCH HÀNG (BẮT BUỘC) ---
            try
            {
                if (taskKH.IsCompletedSuccessfully)
                {
                    var resKH = await taskKH;
                    if (resKH.IsSuccessStatusCode)
                    {
                        var contentKH = await resKH.Content.ReadAsStringAsync();
                        khachHangModel = JsonConvert.DeserializeObject<KhachHangModels>(contentKH);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi giải mã JSON khách hàng {ID}", maKhachHang);
            }

            // Nếu KHÔNG lấy được thông tin khách hàng, trang web không có gì để hiện -> Về danh sách
            if (khachHangModel == null)
            {
                TempData["Error"] = "Không thể tải thông tin khách hàng.";
                return RedirectToAction("DanhSachKhachHang");
            }

            // --- XỬ LÝ DỮ LIỆU ĐƠN HÀNG (TÙY CHỌN - LỖI VẪN HIỆN TRANG) ---
            try
            {
                if (taskDonHang.IsCompletedSuccessfully)
                {
                    var resDonHang = await taskDonHang;
                    if (resDonHang.IsSuccessStatusCode)
                    {
                        var contentDonHang = await resDonHang.Content.ReadAsStringAsync();
                        var resultDonHang = JsonConvert.DeserializeObject<PaginationResult<DonHangModels>>(contentDonHang);
                        if (resultDonHang?.Data != null)
                        {
                            listDonHang = resultDonHang.Data.ToList();
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("API Đơn hàng bị sập hoặc timeout cho khách hàng {ID}", maKhachHang);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xử lý đơn hàng cho khách hàng {ID}", maKhachHang);
                // Không làm gì cả, listDonHang vẫn là list rỗng đã khởi tạo ở trên
            }

            // Đưa vào ViewBag (Luôn đảm bảo không null để View không lỗi)
            ViewBag.DonHangs = listDonHang;

            return View(khachHangModel);
        }
    }
}