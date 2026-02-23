using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyLoTrinhTheoDoi;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhuongTien;
using QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan;
using System.Collections.Generic;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLyLoTrinhTheoDoi
{
    public class QuanLySuCo : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLySuCo> _logger; // Đã sửa loại Logger
        string apiBaseUrl = "https://localhost:7097/api/baocaosuco";
        string apiPhuongTien = "https://localhost:7286/api/quanlyxe";
        string apiNguoiDung = "https://localhost:7022/api/quanlynguoidung";

        public QuanLySuCo(IHttpClientFactory httpClientFactory, ILogger<QuanLySuCo> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public IActionResult Index() => RedirectToAction(nameof(DanhSachSuCo));

        public async Task<IActionResult> DanhSachSuCo(
            string? searchTerm,
            int page = 1,
            string trangthai = "Tất cả",
            string loai = "Tất cả",
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 1. Xây dựng URL cho danh sách sự cố và danh sách loại sự cố
            var queryParams = new Dictionary<string, string?>
            {
                ["Search"] = searchTerm,
                ["trangThai"] = trangthai,
                ["loai"] = loai,
                ["page"] = page.ToString(),
                ["fromDate"] = fromDate?.ToString("yyyy-MM-dd"),
                ["toDate"] = toDate?.ToString("yyyy-MM-dd")
            };

            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{apiBaseUrl}/danhsachsuco", queryParams);
            string apiLoaiSuCoUrl = $"{apiBaseUrl}/getloaisuco";

            try
            {
                // 2. Gọi đồng thời cả 2 API để tối ưu tốc độ
                var taskSuCo = client.GetAsync(apiUrl);
                var taskLoai = client.GetAsync(apiLoaiSuCoUrl);

                await Task.WhenAll(taskSuCo, taskLoai);

                var sucoRes = await taskSuCo;
                var loaiRes = await taskLoai;

                // 3. Xử lý dữ liệu Loại Sự Cố (Dropdown)
                if (loaiRes.IsSuccessStatusCode)
                {
                    var loaiData = await loaiRes.Content.ReadAsStringAsync();
                    // Giả định API trả về List<LoaiSuCoModels>
                    var listLoai = JsonConvert.DeserializeObject<List<LoaiSuCoModels>>(loaiData);
                    ViewBag.LoaiSuCoList = listLoai;
                }

                // 4. Xử lý dữ liệu Danh sách Sự cố (Table)
                if (sucoRes.IsSuccessStatusCode)
                {
                    

                    var sucoData = await sucoRes.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<SuCoApiResponse>(sucoData);

                    // Đồng bộ dữ liệu lên View
                    ViewBag.CurrentSearch = searchTerm;
                    ViewBag.CurrentStatus = trangthai;
                    ViewBag.CurrentLoai = loai;
                    ViewBag.BatDay = fromDate?.ToString("yyyy-MM-dd");
                    ViewBag.KetThuc = toDate?.ToString("yyyy-MM-dd");
                    ViewBag.CurrentPage = result?.CurrentPage ?? 1;
                    ViewBag.TotalPages = result?.TotalPages ?? 1;

                    return View(result?.SuCoList ?? new List<SuCoModels>());
                }
                else
                {
                    _logger.LogError($"API Error: {sucoRes.StatusCode}");
                    ModelState.AddModelError("", "Không thể tải danh sách sự cố.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối API hệ thống");
                ModelState.AddModelError("", "Lỗi kết nối máy chủ.");
            }

            return View(new List<SuCoModels>());
        }
        public async Task<IActionResult> ChiTietSuCo(int maSuCo)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrl}/chitietsuco/{maSuCo}";

            try
            {
                // 1. Gọi API lấy chi tiết sự cố (API chính)
                var response = await client.GetAsync(apiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = "Không tìm thấy thông tin sự cố này.";
                    return View(new SuCoModels());
                }

                var content = await response.Content.ReadAsStringAsync();
                var suCo = JsonConvert.DeserializeObject<SuCoModels>(content);

                if (suCo?.MaLoTrinhNavigation != null)
                {
                    // 2. Định nghĩa hàm gọi API Phương tiện an toàn
                    // Nếu API sập hoặc không tìm thấy, hệ thống vẫn chạy tiếp
                    async Task<PhuongTienModel?> GetPhuongTienSafe(int maPhuongTien)
                    {
                        try
                        {
                            string apiPhuongTienUrl = $"{apiPhuongTien}/chitietphuongtien/{maPhuongTien}";
                            var ptRes = await client.GetAsync(apiPhuongTienUrl);
                            if (ptRes.IsSuccessStatusCode)
                            {
                                var ptContent = await ptRes.Content.ReadAsStringAsync();
                                return JsonConvert.DeserializeObject<PhuongTienModel>(ptContent);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Không thể lấy thông tin phương tiện cho mã {maPhuongTien}: {ex.Message}");
                        }
                        return null;
                    }
                    async Task<NguoiDungModel?> GetNguoiDungSafe(int maNguoiDung)
                    {
                        try
                        {
                            string apiNguoiDungUrl = $"{apiNguoiDung}/chitietnhanvien/{maNguoiDung}";
                            var ndRes = await client.GetAsync(apiNguoiDungUrl);
                            if (ndRes.IsSuccessStatusCode)
                            {
                                var ndContent = await ndRes.Content.ReadAsStringAsync();
                                return JsonConvert.DeserializeObject<NguoiDungModel>(ndContent);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Không thể lấy thông tin người dùng cho mã {maNguoiDung}: {ex.Message}");
                        }
                        return null;
                    }

                    // 3. Thực thi lấy thông tin phương tiện
                    // Nếu MaPhuongTien null, maPT sẽ bằng 0
                    int maPT = suCo.MaLoTrinhNavigation.MaPhuongTien ?? 0;
                    if (maPT > 0)
                    {
                        ViewBag.PhuongTien = await GetPhuongTienSafe(maPT);
                    }
                    ViewBag.PhuongTien = await GetPhuongTienSafe(maPT);

                    int maTaiXe = suCo.MaLoTrinhNavigation.MaNguoiDung ??0;
                    if(maTaiXe > 0)
                    {
                        ViewBag.TaiXe = await GetNguoiDungSafe(maTaiXe);
                    }
                    ViewBag.TaiXe = await GetNguoiDungSafe(maTaiXe);
                }

                return View(suCo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải chi tiết sự cố");
                ViewBag.Error = "Lỗi kết nối hệ thống: " + ex.Message;
                return View(new SuCoModels());
            }
        }
    }

    // Class hứng dữ liệu trả về từ API (Phải khớp với cấu trúc JSON của API)
    public class SuCoApiResponse
    {
        public int TotalItems { get; set; }
        public int PageSize { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public List<SuCoModels>? SuCoList { get; set; }
    }
}