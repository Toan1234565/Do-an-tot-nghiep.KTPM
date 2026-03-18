using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyLichLamViec;
using System.Net.Http.Headers;

namespace QuanLyTaiKhoanNguoiDung.Controllers
{
    public class QuanLyLichLamViec : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyLichLamViec> _logger;
        // Tốt nhất nên để vào appsettings.json, nhưng ở đây dùng biến hằng cho demo
        private readonly string apiBaseUrl = "https://localhost:7022/api/quanlylichlamviec";

        public QuanLyLichLamViec(IHttpClientFactory httpClientFactory, ILogger<QuanLyLichLamViec> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> DanhSachLichLamViec(DateOnly? thoigian, int? maKho, int page = 1)
        {
            // 1. Xử lý mặc định nếu ngày trống (Đồng bộ với logic API)
            DateOnly selectedDate = thoigian ?? DateOnly.FromDateTime(DateTime.Now);

            ViewBag.CurrentThoiGian = selectedDate.ToString("yyyy-MM-dd");
            ViewBag.CurrentMaKho = maKho;
            ViewBag.CurrentPage = page < 1 ? 1 : page;

            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 2. Lấy Token từ Cookie/Session (Nếu hệ thống của bạn dùng JWT để định danh người dùng)
            var token = Request.Cookies["AuthToken"];
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // 3. Xây dựng URL với định dạng ngày chuẩn ISO (yyyy-MM-dd)
            string apiUrl = $"{apiBaseUrl}/danhsachlichlamviec?thoigian={selectedDate:yyyy-MM-dd}&maKho={maKho}&page={ViewBag.CurrentPage}";

            try
            {
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonResult = Newtonsoft.Json.Linq.JObject.Parse(content);

                    // Map dữ liệu từ phần "data" trong JSON trả về
                    var dsNhanVien = jsonResult["data"]?.ToObject<List<DangKyCaTrucModels>>() ?? new List<DangKyCaTrucModels>();

                    ViewBag.TotalPages = (int)(jsonResult["totalPages"] ?? 0);
                    ViewBag.QueryDate = jsonResult["queryDate"]?.ToString(); // Ngày mà API thực tế đã truy vấn

                    return View(dsNhanVien);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Login", "Account"); // Hoặc trang thông báo lỗi
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    ViewBag.Error = "Bạn không có quyền truy cập dữ liệu kho này.";
                }
                else
                {
                    ViewBag.Error = "Không thể lấy dữ liệu từ hệ thống API.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối API lịch làm việc");
                ViewBag.Error = "Lỗi kết nối máy chủ: " + ex.Message;
            }

            // Trả về danh sách trống nếu có lỗi để tránh crash View
            return View(new List<DangKyCaTrucModels>());
        }
    }
}