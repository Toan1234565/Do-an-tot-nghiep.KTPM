using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12; // Chứa PaginationResult
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
        

        public QuanLytaiXe(IHttpClientFactory httpClientFactory, ILogger<QuanLytaiXe> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> DanhSachTaiXe(
            string? search,
            string? loaiBang,
            string? sortBy = "MaNguoiDung",
            bool isDescending = true,
            int page = 1)
        {
            var client = _httpClientFactory.CreateClient();

            var url = $"{apiBaseUrl}/danhsachtaixe?" +
                      $"search={UrlEncoder.Default.Encode(search ?? "")}&" +
                      $"loaiBang={UrlEncoder.Default.Encode(loaiBang ?? "")}&" +
                      $"sortBy={sortBy}&" +
                      $"isDescending={isDescending}&" +
                      $"page={page}";

            // Khởi tạo object rỗng để tránh lỗi null ở View nếu API thất bại
            var result = new PaginationResult<QuanLyTaiXeModels>();

            try
            {
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();

                    // THAY THẾ dynamic bằng Type cụ thể
                    var decodedResult = JsonConvert.DeserializeObject<PaginationResult<QuanLyTaiXeModels>>(jsonData);
                    if (decodedResult != null) result = decodedResult;
                }
                else
                {
                    TempData["Error"] = "Không thể lấy dữ liệu từ hệ thống.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
            }

            // Gán lại ViewBag để giữ trạng thái Form lọc
            ViewBag.Search = search;
            ViewBag.LoaiBang = loaiBang;
            ViewBag.SortBy = sortBy;
            ViewBag.IsDescending = isDescending;

            return View(result);
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