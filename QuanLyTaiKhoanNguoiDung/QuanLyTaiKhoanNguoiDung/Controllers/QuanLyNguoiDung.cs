using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyDiaChi;
using QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan;

namespace QuanLyTaiKhoanNguoiDung.Controllers
{
    public class QuanLyNguoiDung : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TmdtContext _context;
        public QuanLyNguoiDung(IHttpClientFactory httpClientFactory, TmdtContext context)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
        }
        string apiBaseUrl = "https://localhost:7022/api/quanlynguoidung";
        string apiDiaChi = "https://localhost:7149/api/quanlydiachi";
        public async Task<IActionResult> DanhSachNhanVien(string? searchTerm, int? maChucVu, int page = 1, string? donvi= "Tất cả")
        {

            var chucVus = await _context.ChucVus
                .Select(cv => new {
                    maChucVu = cv.MaChucVu,
                    tenChucVu = cv.TenChucVu
                })
                .ToListAsync();
            ViewBag.DanhSachChucVu = chucVus;
            ViewBag.CurrentSearch = searchTerm;
            ViewBag.CurrentMaChucVu = maChucVu; // Kiểu int?
            ViewBag.CurrentDonVi = donvi;
            var client = _httpClientFactory.CreateClient("BypassSSL");

           
            int pageIndex = page < 1 ? 1 : page;

            
            string apiUrl = $"{apiBaseUrl}/danhsachnguoidung?searchTerm={searchTerm}&maChucVu={maChucVu}&page={pageIndex}&donvi={donvi}";

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonResult = Newtonsoft.Json.Linq.JObject.Parse(content);

            
                    var dsNhanVien = jsonResult["data"]?.ToObject<List<NguoiDungModel>>() ?? new List<NguoiDungModel>();

                    ViewBag.TotalPages = (int)(jsonResult["totalPages"] ?? 0);
                    ViewBag.CurrentPage = (int)(jsonResult["currentPage"] ?? 1);

                    return View(dsNhanVien);
                }
                
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            return View(new List<NguoiDungModel>());
        }
        public async Task<IActionResult> ChiTietNhanVien(int maNhanVien)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrl}/chitietnhanvien/{maNhanVien}";
            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var nguoiDung = JsonConvert.DeserializeObject<NguoiDungModel>(content);
                    // Gọi API danh sách địa chỉ đã được gắn Cache
                    var tasks = new List<Task>();

                    // Task lấy Địa chỉ
                    Task<HttpResponseMessage>? taskDiaChi = null;
                    if (nguoiDung?.MaDiaChi > 0)
                    {
                        taskDiaChi = client.GetAsync($"{apiDiaChi}/chitietdiachi/{nguoiDung.MaDiaChi}");
                        tasks.Add(taskDiaChi);
                    }
                    if (taskDiaChi != null && taskDiaChi.Result.IsSuccessStatusCode)
                    {
                        var contentDiaChi = await taskDiaChi.Result.Content.ReadAsStringAsync();
                        ViewBag.DiaChi = JsonConvert.DeserializeObject<DiaChiModel>(contentDiaChi);
                    }
                    return View(nguoiDung);
                }
               
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                
            }
            return View(new List<NguoiDungModel>());
        }
    }
}