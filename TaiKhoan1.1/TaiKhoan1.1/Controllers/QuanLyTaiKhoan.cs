using Microsoft.AspNetCore.Mvc;
using TaiKhoan1._1.Model11._1.QuanLyTaiKhoan;

namespace TaiKhoan1._1.Controllers
{
    public class QuanLyTaiKhoan : Controller
    {
        //https://localhost:7160/
        private readonly ILogger<QuanLyTaiKhoan> _logger;
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiKey = "https://localhost:7160/api/quanlytaikhoan";
        public QuanLyTaiKhoan(ILogger<QuanLyTaiKhoan> logger, IHttpClientFactory httpClientFactory, HttpClient httpClient)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _httpClient = httpClient;
        }
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> TaoTaiKhoan([FromBody] TaiKhoanCreate taikhoan)
        {
            if(!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var client = _httpClientFactory.CreateClient();
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(taikhoan);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{_apiKey}/taotaikhoan", httpContent);
            if(response.IsSuccessStatusCode)
            {
                return Ok("Them nhan vien thanh cong.");
            }
            else
            {
                return StatusCode((int)response.StatusCode, "Error creating account.");
            }
            
        }
    }
}
