using Microsoft.AspNetCore.Mvc;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyNhanVien;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNhatKyHeThong;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyNhatKyHeThong
{
    public class DanhSachNhatKyHeThong : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DanhSachNhatKyHeThong> _logger;
        // Tốt nhất nên để vào appsettings.json, nhưng ở đây dùng biến hằng cho demo
        private readonly string apiBaseUrl = "https://localhost:7088/api/nhatkyhethong";

        public DanhSachNhatKyHeThong(IHttpClientFactory httpClientFactory, ILogger<DanhSachNhatKyHeThong> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }
        public async Task<IActionResult> _NhatKyPartial(int page = 1, string tendichvu = "")
        {
            // Gọi API giống hệt hàm ThongBaoNhatKy bạn đã viết
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrl}/getall-nhatky?page={page}&pageSize=10&tendichvu={tendichvu}";

            var response = await client.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var jsonResult = Newtonsoft.Json.Linq.JObject.Parse(content);
                var dsNhatKy = jsonResult["data"]?.ToObject<List<NhatKyHeThongModels>>() ?? new List<NhatKyHeThongModels>();

                
                return PartialView("~/Views/DanhSachNhatKyHeThong/_NhatKyPartial.cshtml", dsNhatKy);
            }
            return Content("Không thể tải dữ liệu nhật ký.");
        }
    }
}