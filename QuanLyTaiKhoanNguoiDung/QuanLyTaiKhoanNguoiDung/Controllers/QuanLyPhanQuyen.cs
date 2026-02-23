using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan;

namespace QuanLyTaiKhoanNguoiDung.Controllers
{
    public class QuanLyPhanQuyen : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TmdtContext _context;
        public QuanLyPhanQuyen(IHttpClientFactory httpClientFactory, TmdtContext context)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
        }
        String apiBaseUrl = "https://localhost:7022/api/quanlynguoidung";
        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> PhanQuyen()
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrl}/danhsachchucvu";
            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var phanquyen = JsonConvert.DeserializeObject<List<ChucVuModel>>(content);
                    return View(phanquyen);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            return View(new List<ChucVuModel>());
        }
    }
}
