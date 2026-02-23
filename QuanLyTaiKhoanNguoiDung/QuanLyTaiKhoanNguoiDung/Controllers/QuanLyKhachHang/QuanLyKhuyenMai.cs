using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhuyenMai;
using System.Net.Http.Json;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyKhachHang
{
    public class QuanLyKhuyenMai : Controller
    {
        private readonly ILogger<QuanLyKhuyenMai> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string apiBaseUrl = "https://localhost:7149/api/quanlykhuyenmai";

        public QuanLyKhuyenMai(ILogger<QuanLyKhuyenMai> logger, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> DanhSachKhuyenMai(
            string? searchTerm,
            int page = 1,
            int? loaikhuyenmai = null,
            DateTime? bd = null,
            DateTime? kt = null,
            bool? isActive = true)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            int pageSize = 10; // Khớp với pageSize ở API

            // 1. Xây dựng Query String linh hoạt
            var queryParams = new Dictionary<string, string?>
            {
                ["search"] = searchTerm,
                ["page"] = page.ToString(),
                ["isActive"] = isActive?.ToString().ToLower(),
                ["loaikhuyenmai"] = loaikhuyenmai?.ToString(),
                ["bd"] = bd?.ToString("yyyy-MM-dd"),
                ["kt"] = kt?.ToString("yyyy-MM-dd")
            };

            // Lọc bỏ các tham số null
            var filteredParams = queryParams.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value!);
            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{apiBaseUrl}/danhsachkhuyenmai", filteredParams);

            try
            {
                // 2. Chạy song song 2 Task để tối ưu hiệu năng
                var taskKM = client.GetAsync(apiUrl);
                var taskLoai = client.GetAsync($"{apiBaseUrl}/danhsachloai");

                await Task.WhenAll(taskKM, taskLoai);

                var responseKM = await taskKM;
                var responseLoai = await taskLoai;

                // Xử lý Danh sách Khuyến mãi
                List<KhuyenMaiModels> data = new List<KhuyenMaiModels>();
                if (responseKM.IsSuccessStatusCode)
                {
                    var jsonString = await responseKM.Content.ReadAsStringAsync();
                    // Nếu API trả về object bọc (có totalItems), dùng dynamic để bóc tách
                    // Nếu API chỉ trả về List đơn thuần, hãy dùng DeserializeObject<List<...>>
                    data = JsonConvert.DeserializeObject<List<KhuyenMaiModels>>(jsonString) ?? new List<KhuyenMaiModels>();
                }

                // Xử lý Danh sách Loại (để đổ vào Dropdown search hoặc hiển thị tên)
                if (responseLoai.IsSuccessStatusCode)
                {
                    var jsonLoai = await responseLoai.Content.ReadAsStringAsync();
                    var listLoai = JsonConvert.DeserializeObject<List<LoaiKhuyenMaiModels>>(jsonLoai);
                    ViewBag.ListLoaiKM = listLoai;
                }

                // 3. Metadata cho Phân trang và giữ trạng thái Filter
                ViewBag.SearchTerm = searchTerm;
                ViewBag.LoaiKM = loaikhuyenmai;
                ViewBag.NgayBD = bd?.ToString("yyyy-MM-dd");
                ViewBag.NgayKT = kt?.ToString("yyyy-MM-dd");
                ViewBag.IsActive = isActive;
                ViewBag.CurrentPage = page;

                // Giả định API cũ của bạn trả về List, ta tính toán phân trang dựa trên số lượng lấy được
                // Nếu API nâng cấp trả về TotalCount, hãy dùng: int totalItems = result.totalCount;
                // Ở đây tạm thời xử lý hiển thị phân trang cơ bản:
                ViewBag.TotalPages = data.Count < pageSize && page == 1 ? 1 : page + 1;

                return View(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối API Khuyến mãi");
                return View(new List<KhuyenMaiModels>());
            }
        }     
    }
}