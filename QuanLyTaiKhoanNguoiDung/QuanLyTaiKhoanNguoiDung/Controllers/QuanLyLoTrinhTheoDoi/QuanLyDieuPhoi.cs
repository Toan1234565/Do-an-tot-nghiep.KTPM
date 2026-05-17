using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
// Import đúng namespace chứa Model của bạn để sử dụng
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyLoTrinh.QuanLyDieuPhoi;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyKhoBai;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyLoTrinhTheoDoi
{
    public class QuanLyDieuPhoi : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyDieuPhoi> _logger;

        // Port 7097 là Server Lộ trình 
        private readonly string apiBaseUrl = "https://localhost:7097/api/DieuPhoiThongMinh";

        public QuanLyDieuPhoi(IHttpClientFactory httpClientFactory, ILogger<QuanLyDieuPhoi> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public IActionResult QuanLyDieuPhoiLoTrinh12()
        {
            return View();
        }

        // --- GET: QUẢN LÝ ĐIỀU PHỐI LỘ TRÌNH (HIỂN THỊ DANH SÁCH) ---
        public async Task<IActionResult> QuanLyDieuPhoiLoTrinh(
            [FromQuery] DateTime? ngayDieuPhoi,
            [FromQuery] int? maKhoQuanLy, // 1. BỔ SUNG: Thêm tham số lọc theo mã kho
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                // 2. BỔ SUNG: Tải danh sách tên kho bãi để hiển thị lên dropdown bộ lọc ở View
                var danhSachKho = new List<TenKhobaiModels>();
                try
                {
                    var khoResponse = await client.GetAsync($"https://localhost:7286/api/quanlykhobai/danhsachtenkho");
                    if (khoResponse.IsSuccessStatusCode)
                    {
                        danhSachKho = await khoResponse.Content.ReadFromJsonAsync<List<TenKhobaiModels>>() ?? new List<TenKhobaiModels>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi gọi API lấy danh sách tên kho bãi");
                }

                // Truyền danh sách kho và mã kho đang chọn qua ViewBag để View sử dụng
                ViewBag.DanhSachKho = danhSachKho;
                ViewBag.MaKhoQuanLy = maKhoQuanLy;

                // 3. Xây dựng URL kèm Query String để lọc và phân trang chuẩn chỉ
                string url = $"{apiBaseUrl}/cho-dieu-phoi-thu-cong?page={page}&pageSize={pageSize}";
                if (ngayDieuPhoi.HasValue)
                {
                    url += $"&ngayDieuPhoi={ngayDieuPhoi.Value:yyyy-MM-dd}";
                }

                // BỔ SUNG: Gắn thêm mã kho vào URL API nếu người dùng có chọn lọc
                if (maKhoQuanLy.HasValue)
                {
                    url += $"&maKhoQuanLy={maKhoQuanLy.Value}";
                }

                // 4. Gọi API đến Server Lộ trình
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    // Bóc tách JSON theo cấu trúc dữ liệu strongly-typed an toàn
                    var apiResult = await response.Content.ReadFromJsonAsync<ApiResponseData>();

                    // Truyền các thông tin bổ trợ qua ViewBag để hiển thị trên giao diện (Pagination & Filter control)
                    ViewBag.NgayDieuPhoi = ngayDieuPhoi?.ToString("yyyy-MM-dd");
                    ViewBag.CurrentPage = apiResult?.CurrentPage ?? page;
                    ViewBag.TotalPages = apiResult?.TotalPages ?? 1;
                    ViewBag.TotalItems = apiResult?.TotalItems ?? 0;

                    // Gửi danh sách dữ liệu chuẩn Model sang View
                    var danhSachLoTrinh = apiResult?.Data ?? new List<LoTrinhDieuPhoiThuCongModels>();
                    return View(danhSachLoTrinh);
                }

                _logger.LogWarning($"Không thể lấy dữ liệu từ API Lộ trình. Status Code: {response.StatusCode}");
                SetErrorViewBag("Không thể kết nối lấy dữ liệu từ hệ thống điều phối.");
                return View(new List<LoTrinhDieuPhoiThuCongModels>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối Server Lộ trình");
                SetErrorViewBag("Hệ thống mất kết nối với máy chủ điều phối hành trình. Vui lòng thử lại sau.");
                return View(new List<LoTrinhDieuPhoiThuCongModels>());
            }
        }
        // --- POST: XỬ LÝ LƯU GÁN XE TỪ MODAL SUBMIT VỀ ---
        [HttpPost]
        public async Task<IActionResult> LuuGanXe(int maLoTrinh, int maNguoiDung, int maPhuongTien)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                // Chuẩn bị dữ liệu Payload gửi đi API
                var payload = new
                {
                    MaLoTrinh = maLoTrinh,
                    MaNguoiDung = maNguoiDung,   // Mã tài xế
                    MaPhuongTien = maPhuongTien  // Mã xe
                };

                // Endpoint API cập nhật tài xế và phương tiện bên Server Lộ Trình (hãy điều chỉnh lại URL cho đúng thực tế API của bạn)
                string url = $"{apiBaseUrl}/cap-nhat-dieu-phoi";

                var response = await client.PostAsJsonAsync(url, payload);

                if (response.IsSuccessStatusCode)
                {
                    // Lưu trạng thái thành công tạm thời để view hoặc layout hiển thị thông báo nếu cần
                    TempData["SuccessMessage"] = "Gán tài xế và phương tiện thành công!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Gán phương tiện thất bại. Vui lòng kiểm tra lại trạng thái xe/tài xế.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi thực hiện gán xe cho lộ trình {maLoTrinh}");
                TempData["ErrorMessage"] = "Có lỗi xảy ra trong quá trình kết nối máy chủ để lưu dữ liệu.";
            }

            // Sau khi xử lý xong, điều hướng quay trở lại trang danh sách để cập nhật giao diện
            return RedirectToAction(nameof(QuanLyDieuPhoiLoTrinh));
        }

        private void SetErrorViewBag(string message)
        {
            ViewBag.ErrorMessage = message;
            ViewBag.CurrentPage = 1;
            ViewBag.TotalPages = 1;
            ViewBag.TotalItems = 0;
        }
    }

    // Đã thay đổi kiểu dữ liệu từ List<dynamic> sang List<LoTrinhDieuPhoiThuCongModels> để đồng bộ với View
    public class ApiResponseData
    {
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }

        public List<LoTrinhDieuPhoiThuCongModels> Data { get; set; } = new List<LoTrinhDieuPhoiThuCongModels>();
    }
}