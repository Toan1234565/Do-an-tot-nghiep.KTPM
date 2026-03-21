using MailKit.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12; // Chứa PaginationResult
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyNhanVien;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyTaiXe;
using System.Text.Encodings.Web;

namespace QuanLyTaiKhoanNguoiDung.Controllers
{
    public class QuanLytaiXe : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLytaiXe> _logger;
        private readonly string apiBaseUrl = "https://localhost:7022/api/quanlytaixe";
        private readonly string apiBaseUrlLichSuVP = "https://localhost:7022/api/quanlyllichsuvipham";
        private readonly TmdtContext _context;

        public QuanLytaiXe(IHttpClientFactory httpClientFactory, ILogger<QuanLytaiXe> logger, TmdtContext context)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _context = context;
        }
        public async Task<IActionResult> DanhSachTaiXe(string? search, string? loaiBang, int? maKho, string? trangthaihoatdong, string? sortBy = "MaNguoiDung", bool isDescending = true, int page = 1, bool trangthai =true)
        {          
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentLoaiBang = loaiBang;
            ViewBag.CurrentSortBy = sortBy;
            ViewBag.CurrentMaKho = maKho;
            ViewBag.IsDescending = isDescending;
            ViewBag.CurrentTrangThai = trangthai;
            ViewBag.CurrentTrangThaiHoatDong = trangthaihoatdong;
            var client = _httpClientFactory.CreateClient("BypassSSL");


            int pageIndex = page < 1 ? 1 : page;


            var queryParams = $"{apiBaseUrl}/danhsachtaixe?search={search}&loaiBang={loaiBang}&maKho={maKho}&sortBy={sortBy}&isDescending={isDescending}&page={page}&trangthaihoatdong={trangthaihoatdong}&trangthai={trangthai}";
            //string apiUrl = $"{apiBaseUrl}/danhsachnguoidung?searchTerm={searchTerm}&maChucVu={maChucVu}&maKho={maKho}&page={pageIndex}";

            try
            {
                var response = await client.GetAsync(queryParams);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonResult = Newtonsoft.Json.Linq.JObject.Parse(content);


                    var dsNhanVien = jsonResult["data"]?.ToObject<List<QuanLyTaiXeModels>>() ?? new List<QuanLyTaiXeModels>();

                    ViewBag.TotalPages = (int)(jsonResult["totalPages"] ?? 0);
                    ViewBag.CurrentPage = (int)(jsonResult["currentPage"] ?? 1);

                    return View(dsNhanVien);
                }

            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            return View(new List<QuanLyTaiXeModels>());
        }
        
        public async Task<IActionResult> ChiTietTaiXe(int id)
        {
            var client = _httpClientFactory.CreateClient();
            var viewModel = new HoSoTaiXeViewModel();

            // 1. Định nghĩa các Task nhưng CHƯA await ngay lập tức
            var taskChiTiet = GetChiTietAsync(client, id);
            var taskLichSu = GetLichSuViPhamAsync(client, id);

            // 2. Chạy song song cả 2
            await Task.WhenAll(taskChiTiet, taskLichSu);

            // 3. Lấy kết quả
            viewModel.ChiTiet = await taskChiTiet;
            viewModel.DanhSachViPham = await taskLichSu;

            // Kiểm tra nếu thông tin cơ bản không có thì mới báo lỗi chính
            if (viewModel.ChiTiet == null)
            {
                TempData["Error"] = "Không thể tải thông tin chi tiết tài xế.";
                return RedirectToAction("DanhSachTaiXe");
            }

            return View(viewModel);
        }

        // Hàm phụ để lấy Chi Tiết - Bọc try-catch để lỗi không làm sập luồng chính
        private async Task<ChiTietTaiXeModels> GetChiTietAsync(HttpClient client, int id)
        {
            try
            {
                var response = await client.GetAsync($"{apiBaseUrl}/chitiet/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ChiTietTaiXeModels>(jsonData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi API Chi Tiết");
            }
            return null;
        }

        // Hàm phụ để lấy Lịch Sử Vi Phạm - Lỗi thì trả về danh sách rỗng
        private async Task<List<LichSuViPhamModels>> GetLichSuViPhamAsync(HttpClient client, int id)
        {
            try
            {
                var response = await client.GetAsync($"{apiBaseUrlLichSuVP}/LichSuViPham/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<LichSuViPhamModels>>(jsonData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi API Lịch Sử Vi Phạm");
            }
            return new List<LichSuViPhamModels>(); // Trả về list rỗng để View không bị NullReference
        }
        
    }
}