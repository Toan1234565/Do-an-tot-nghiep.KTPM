using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhoBai;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhuongTien;
using QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLykho
{
    public class QuanLyPhuongTien : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TmdtContext _context;

        private readonly ILogger<QuanLyPhuongTien> _logger; // Thêm dòng này
        public QuanLyPhuongTien(IHttpClientFactory httpClientFactory, TmdtContext context, ILogger<QuanLyPhuongTien> logger)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        string apiBaseUrl = "https://localhost:7286/api/quanlyxe";
        string apiBaseTenKho = "https://localhost:7286/api/quanlykhobai";
        string apiBaseDinhMuc = "https://localhost:7286/api/quanlydinhmuc";

        // Thêm tham số trangThaiDangKiem vào Action
        public async Task<IActionResult> DanhSachPhuongTien(
            string? searchTerm,
            string? maLoaiXe,
            int page = 1,
            string status = "Tất cả",
            string trangThaiDangKiem = "Tất cả")
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 1. Tạo URL cho danh sách xe với đầy đủ các tham số lọc
            var queryParams = new Dictionary<string, string?>
            {
                ["searchTerm"] = searchTerm,
                ["maLoaiXe"] = maLoaiXe,
                ["page"] = page.ToString(),
                ["status"] = status,
                ["trangthaidangkiem"] = trangThaiDangKiem
            };

            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{apiBaseUrl}/danhsachxe", queryParams);
            string apiLoaiXeUrl = $"{apiBaseUrl}/danhsachloaixe";
            string apiKhoUrl = $"{apiBaseTenKho}/danhsachtenkho";
            try
            {
                // Gọi song song 2 API
                var taskLoaiXe = client.GetAsync(apiLoaiXeUrl);
                var taskTenKho = client.GetAsync(apiKhoUrl);
                var taskXe = client.GetAsync(apiUrl);



                await Task.WhenAll(taskLoaiXe, taskTenKho, taskXe);

                // 2. Xử lý danh sách loại xe (để hiển thị vào Dropdown lọc)
                var loaiXeRes = await taskLoaiXe;
                if (loaiXeRes.IsSuccessStatusCode)
                {
                    var contentLoai = await loaiXeRes.Content.ReadAsStringAsync();
                    var jsonLoai = Newtonsoft.Json.Linq.JObject.Parse(contentLoai);
                    ViewBag.DanhSachLoaiXe = jsonLoai["data"]?.ToObject<List<LoaiXeModels>>() ?? new List<LoaiXeModels>();
                }
                var tenKho = await taskTenKho;
                if (tenKho.IsSuccessStatusCode)
                {
                    var contentTenKho = await tenKho.Content.ReadAsStringAsync();

                    // SỬA TẠI ĐÂY: Vì API trả về mảng trực tiếp, không dùng JObject.Parse(["data"])
                    // Hãy dùng JsonConvert để Deserialize trực tiếp thành List
                    ViewBag.DanhSachKho = JsonConvert.DeserializeObject<List<QuanLyKhobaiModels>>(contentTenKho)
                                          ?? new List<QuanLyKhobaiModels>();
                }
                // 3. Xử lý danh sách phương tiện
                var xeRes = await taskXe;
                if (xeRes.IsSuccessStatusCode)
                {
                    var content = await xeRes.Content.ReadAsStringAsync();
                    dynamic jsonResult = Newtonsoft.Json.JsonConvert.DeserializeObject(content);

                    ViewBag.TotalPages = (int)(jsonResult?.totalPages ?? 0);
                    ViewBag.CurrentPage = (int)(jsonResult?.currentPage ?? 1);

                    // Lưu tham số để giữ trạng thái Form trên UI
                    ViewBag.CurrentMaLoaiXe = maLoaiXe;
                    ViewBag.CurrentSearch = searchTerm;
                    ViewBag.CurrentStatus = status;
                    ViewBag.CurrentTrangThaiDangKiem = trangThaiDangKiem; // Cần thiết cho thẻ <select>

                    var dsPhuongTien = jsonResult?.data?.ToObject<List<PhuongTienModel>>() ?? new List<PhuongTienModel>();

                    return View(dsPhuongTien);
                }
                else
                {
                    ViewBag.Error = $"API trả về lỗi: {xeRes.StatusCode}";
                }
            }

            catch (Exception ex)
            {
                // Log lỗi chi tiết ra Console để biết nó chết ở dòng nào
                Console.WriteLine("Lỗi tại Action DanhSachPhuongTien: " + ex.ToString());
                ViewBag.Error = "Lỗi hệ thống: " + ex.Message;
                return View(new List<PhuongTienModel>());
            }
            return View(new List<PhuongTienModel>());
        }
        public async Task<IActionResult> ChiTietPhuongTien(int maPhuongTien)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrl}/chitietphuongtien/{maPhuongTien}";
            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var nguoiDung = JsonConvert.DeserializeObject<PhuongTienModel>(content);
                    return View(nguoiDung);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;

            }
            return View(new List<PhuongTienModel>());
        }

        public async Task<IActionResult> LichTrinhBaoTri(
            [FromQuery] int soNgayBaoTri = 15,
            [FromQuery] int soNgayDangKiem = 30,
            [FromQuery] int soNgayPhiDb = 30,
            [FromQuery] int pageBT = 1,
            [FromQuery] int pageDK = 1,
            [FromQuery] int pagePDB = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            int pageSize = 10;

            // Định nghĩa URL
            string apiBaoTriUrl = $"{apiBaseUrl}/xe-sap-den-han-bao-tri?soNgayCanhBao={soNgayBaoTri}&page={pageBT}&pageSize={pageSize}";
            string apiDangKiemUrl = $"{apiBaseUrl}/xe-sap-het-han-dang-kiem?soNgayCanhBao={soNgayDangKiem}&page={pageDK}&pageSize={pageSize}";
            string apiPhiDuongBoUrl = $"{apiBaseUrl}/xe-sap-het-han-phi-duong-bo?soNgayCanhBao={soNgayPhiDb}&page={pagePDB}&pageSize={pageSize}";

            try
            {
                // Gọi đồng thời 3 API
                var taskBT = client.GetAsync(apiBaoTriUrl);
                var taskDK = client.GetAsync(apiDangKiemUrl);
                var taskPDB = client.GetAsync(apiPhiDuongBoUrl);

                await Task.WhenAll(taskBT, taskDK, taskPDB);

                // Đọc và xử lý dữ liệu (Sử dụng hàm Helper bên dưới)
                var dataBT = await ProcessApiResponse(taskBT.Result);
                var dataDK = await ProcessApiResponse(taskDK.Result);
                var dataPDB = await ProcessApiResponse(taskPDB.Result);

                // Gán dữ liệu cho Bảo trì
                ViewBag.DsSapBaoTri = dataBT.Data;
                ViewBag.TotalItemsBT = dataBT.TotalItems;
                ViewBag.TotalPagesBT = (int)Math.Ceiling((double)dataBT.TotalItems / pageSize);

                // Gán dữ liệu cho Đăng kiểm
                ViewBag.DsSapHetDangKiem = dataDK.Data;
                ViewBag.TotalItemsDK = dataDK.TotalItems;
                ViewBag.TotalPagesDK = (int)Math.Ceiling((double)dataDK.TotalItems / pageSize);

                // Gán dữ liệu cho Phí đường bộ
                ViewBag.DsSapHetPhiDuongBo = dataPDB.Data;
                ViewBag.TotalItemsPDB = dataPDB.TotalItems;
                ViewBag.TotalPagesPDB = (int)Math.Ceiling((double)dataPDB.TotalItems / pageSize);

                // Lưu tham số filter để hiển thị lại trên Form
                ViewBag.CurrentPageBT = pageBT;
                ViewBag.CurrentPageDK = pageDK;
                ViewBag.CurrentPagePDB = pagePDB;
                ViewBag.SoNgayBaoTri = soNgayBaoTri;
                ViewBag.SoNgayDangKiem = soNgayDangKiem;
                ViewBag.SoNgayPhiDb = soNgayPhiDb;

                return View();
            }
            catch (Exception ex)
            {
                SetDefaultViewBagValues();
                ViewBag.Error = "Lỗi kết nối hệ thống: " + ex.Message;
                return View();
            }
        }

        // Hàm Helper để tránh lặp code xử lý JSON
        private async Task<(List<PhuongTienModel> Data, int TotalItems)> ProcessApiResponse(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                return (new List<PhuongTienModel>(), 0);

            var content = await response.Content.ReadAsStringAsync();
            // Sử dụng dynamic để đọc linh hoạt hoặc tạo một class ApiResponse chung
            var json = JsonConvert.DeserializeObject<dynamic>(content);

            var data = json?.data?.ToObject<List<PhuongTienModel>>() ?? new List<PhuongTienModel>();
            int total = (int)(json?.totalItems ?? 0);

            return (data, total);
        }

        private void SetDefaultViewBagValues()
        {
            ViewBag.DsSapBaoTri = new List<PhuongTienModel>();
            ViewBag.DsSapHetDangKiem = new List<PhuongTienModel>();
            ViewBag.DsSapHetPhiDuongBo = new List<PhuongTienModel>();
            ViewBag.TotalItemsBT = 0; ViewBag.TotalItemsDK = 0; ViewBag.TotalItemsPDB = 0;
            ViewBag.TotalPagesBT = 0; ViewBag.TotalPagesDK = 0; ViewBag.TotalPagesPDB = 0;
        }

        public async Task<IActionResult> DanhSachDinhMuc([FromQuery] int? maLoaiXe, [FromQuery] int page = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 1. Chuẩn bị các URL API
            // API lấy danh sách định mức (đã group) có kèm tham số lọc
            string apiDinhMucUrl = $"{apiBaseDinhMuc}/danhsachdinhmuc?maLoaiXe={maLoaiXe}&page={page}";
            // API lấy danh sách loại xe cho dropdown
            string apiLoaiXeUrl = $"{apiBaseUrl}/danhsachloaixe";

            try
            {
                // 2. Gọi song song cả 2 API để tối ưu tốc độ
                var taskDinhMuc = client.GetAsync(apiDinhMucUrl);
                var taskLoaiXe = client.GetAsync(apiLoaiXeUrl);

                await Task.WhenAll(taskDinhMuc, taskLoaiXe);

                // 3. Xử lý dữ liệu Danh sách loại xe (Dropdown)
                var resLoaiXe = await taskLoaiXe;
                if (resLoaiXe.IsSuccessStatusCode)
                {
                    var contentLoai = await resLoaiXe.Content.ReadAsStringAsync();
                    // Nếu API trả về JObject có trường "data"
                    var jsonLoai = Newtonsoft.Json.Linq.JObject.Parse(contentLoai);
                    ViewBag.DanhSachLoaiXe = jsonLoai["data"]?.ToObject<List<LoaiXeModels>>()
                                             ?? new List<LoaiXeModels>();
                }

                // 4. Xử lý dữ liệu Định mức (Dữ liệu chính của bảng)
                var resDinhMuc = await taskDinhMuc;
                if (resDinhMuc.IsSuccessStatusCode)
                {
                    var contentDinhMuc = await resDinhMuc.Content.ReadAsStringAsync();

                    // Deserialize vào List<LoaiXeModels> vì API trả về cấu trúc Grouped (LoaiXe + List HangMuc)
                    var dsDinhMucGrouped = JsonConvert.DeserializeObject<List<LoaiXeModels>>(contentDinhMuc);

                    // Lưu các giá trị hiện tại vào ViewBag để giữ trạng thái trên View
                    ViewBag.CurrentPage = page;
                    ViewBag.CurrentMaLoaiXe = maLoaiXe;

                    return View(dsDinhMucGrouped);
                }
                else
                {
                    _logger.LogWarning($"API Định mức trả về lỗi: {resDinhMuc.StatusCode}");
                    return View(new List<LoaiXeModels>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực hiện lọc định mức theo loại xe");
                ViewBag.Error = "Không thể kết nối đến máy chủ: " + ex.Message;
                return View(new List<LoaiXeModels>());
            }
        }
    }
    }