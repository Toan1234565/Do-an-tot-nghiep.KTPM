using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models12;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhachHang;
using System.Text;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyKhachHang
{
    public class QuanLyHopDong : Controller
    {
        private readonly ILogger<QuanLyHopDong> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl = "https://localhost:7149/api/quanlyhopdong";

        public QuanLyHopDong(ILogger<QuanLyHopDong> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        // 1. View Danh sách hợp đồng
        [HttpGet]
        public async Task<IActionResult> DanhSachHopDong(string? Search, DateTime? thoiGianBD, DateTime? thoiGianKT, string? trangthai = "Tất cả", int page = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 1. Chuẩn bị các tham số Query String khớp với API Server
            var queryParams = new Dictionary<string, string?>
            {
                ["Search"] = Search?.Trim(),
                ["trangthai"] = trangthai,
                ["thoiGianBD"] = thoiGianBD?.ToString("yyyy-MM-dd"), // Format ISO để API hiểu đúng
                ["thoiGianKT"] = thoiGianKT?.ToString("yyyy-MM-dd"),
                ["page"] = page.ToString()
            };

            // 2. Xây dựng URL (Sửa endpoint thành danhsachhopdong thay vì danhsachkhachhang)
            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{_apiBaseUrl}/danhsachhopdong", queryParams);

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();

                    // Giả định bạn dùng chung cấu trúc PaginationResult cho HopDongVanChuyenModels
                    var result = JsonConvert.DeserializeObject<PaginationResult<HopDongVanChuyenModels>>(jsonData);

                    // 3. Truyền dữ liệu sang View
                    ViewBag.TotalPages = result?.TotalPages ?? 0;
                    ViewBag.CurrentPage = result?.CurrentPage ?? 1;
                    ViewBag.Search = Search;
                    ViewBag.TrangThai = trangthai;
                    ViewBag.ThoiGianBD = thoiGianBD?.ToString("yyyy-MM-dd");
                    ViewBag.ThoiGianKT = thoiGianKT?.ToString("yyyy-MM-dd");

                    return View(result?.Data ?? new List<HopDongVanChuyenModels>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API danh sách hợp đồng");
            }

            // Trả về list trống nếu có lỗi hoặc không có dữ liệu
            return View(new List<HopDongVanChuyenModels>());
        }

        [HttpGet]
        public async Task<IActionResult> ChiTietHopDong(int id)
        {
            // 1. Sử dụng client "BypassSSL" giống như phần danh sách
            var client = _httpClientFactory.CreateClient("BypassSSL");

            try
            {
                // 2. Gọi API lấy chi tiết (Endpoint khớp với Server side đã viết)
                var response = await client.GetAsync($"{_apiBaseUrl}/chitiethopdong/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();

                    // 3. Giải mã dữ liệu vào Model tường minh (bao gồm cả thông tin Khách hàng & Địa chỉ)
                    var detail = JsonConvert.DeserializeObject<HopDongVanChuyenModels>(jsonData);

                    if (detail == null)
                    {
                        return NotFound();
                    }

                    return View(detail);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound(new { message = "Hợp đồng không tồn tại trên hệ thống." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API chi tiết hợp đồng mã: {id}", id);
            }

            // Trả về trang lỗi nếu có sự cố kết nối hoặc lỗi server
            return View("Error");
        }

        // 3. Action Tải file Word
        [HttpGet]
        public async Task<IActionResult> TaiFile(int id, bool inline = false)
        {
            var client = _httpClientFactory.CreateClient();
            // Gọi đến API bạn vừa tạo ở bước trước
            var response = await client.GetAsync($"{_apiBaseUrl}/download-file/{id}");

            if (response.IsSuccessStatusCode)
            {
                var fileBytes = await response.Content.ReadAsByteArrayAsync();

                // Lấy tên file và định dạng từ API trả về
                var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"hopdong_{id}.pdf";
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

                if (inline)
                {
                    // CHẾ ĐỘ XEM: Không truyền fileName vào tham số thứ 3 để trình duyệt mở trực tiếp
                    return File(fileBytes, contentType);
                }

                // CHẾ ĐỘ TẢI: Truyền đầy đủ 3 tham số để ép buộc tải về
                return File(fileBytes, contentType, fileName);
            }

            return BadRequest("Không thể tải hoặc hiển thị file. Vui lòng kiểm tra lại dữ liệu.");
        }
    }
}