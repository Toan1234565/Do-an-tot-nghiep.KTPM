using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyDiaChi;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhoBai;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyNhanVien;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhuongTien;

namespace QuanLyTaiKhoanNguoiDung.Controllers.QuanLykho
{
    public class QuanLyKhoBai : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TmdtContext _context;
        string apiBaseUrl = "https://localhost:7286/api/quanlykhobai";
        string apiDiaChi = "https://localhost:7149/api/quanlydiachi";
        string apiNguoiDung = "https://localhost:7022/api/quanlynguoidung";
        public QuanLyKhoBai(IHttpClientFactory httpClientFactory, TmdtContext context)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }
        public async Task<IActionResult> DanhSachKhoBai(
             string? searchTerm,
             int page = 1,
             string loaikho = "Tất cả",
             string trangthai = "Hoạt động")
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            var queryParams = new Dictionary<string, string?>
            {
                ["seach"] = searchTerm, // Lưu ý: API dùng từ "seach" (thiếu chữ r) theo code bạn viết
                ["page"] = page.ToString(),
                ["loaikho"] = loaikho,
                ["trangthai"] = trangthai
            };

            string apiUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString($"{apiBaseUrl}/getallkho", queryParams);
            string apiLoaiKhoUrl = $"{apiBaseUrl}/getallloaikho";

            try
            {
                var taskLoaiKho = client.GetAsync(apiLoaiKhoUrl);
                var taskKhoBai = client.GetAsync(apiUrl);
                await Task.WhenAll(taskLoaiKho, taskKhoBai);

                // 1. Xử lý Loại Kho
                var loaiKhoRes = await taskLoaiKho;
                if (loaiKhoRes.IsSuccessStatusCode)
                {
                    var loaiKhoData = await loaiKhoRes.Content.ReadAsStringAsync();
                    // Sửa lại Model đúng cho Loại Kho (Dùng LoaiKhoModels thay vì LoaiXeModels)
                    var loaiKhoList = JsonConvert.DeserializeObject<List<LoaiKhoModel>>(loaiKhoData);
                    ViewBag.LoaiKhoList = loaiKhoList;
                }

                // 2. Xử lý Danh sách Kho Bãi
                var khoBaiRes = await taskKhoBai;
                if (khoBaiRes.IsSuccessStatusCode)
                {
                    var khoBaiData = await khoBaiRes.Content.ReadAsStringAsync();

                    // Giải nén dynamic để lấy thông tin phân trang
                    dynamic result = JsonConvert.DeserializeObject(khoBaiData);

                    // Lấy danh sách thực sự từ thuộc tính "data" trong JSON trả về
                    var dataJson = result?.data?.ToString();
                    var khoBaiList = JsonConvert.DeserializeObject<List<QuanLyKhobaiModels>>(dataJson);

                    ViewBag.TotalPages = (int)(result?.totalPages ?? 0);
                    ViewBag.CurrentPage = (int)(result?.currentPage ?? 1);
                    ViewBag.SearchTerm = searchTerm;
                    ViewBag.Loaikho = loaikho;
                    ViewBag.Trangthai = trangthai;

                    return View(khoBaiList);
                }

                ModelState.AddModelError(string.Empty, "Không tìm thấy dữ liệu từ API.");
                return View(new List<QuanLyKhobaiModels>());
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Lỗi kết nối: {ex.Message}");
                return View(new List<QuanLyKhobaiModels>());
            }
        }


        public async Task<IActionResult> ChiTietKhoBai(int maKho)
        {
            var client = _httpClientFactory.CreateClient("MyClient");
            try
            {
                // 1. Gọi API lấy chi tiết kho bãi (Đây là API gốc, nếu lỗi thì dừng luôn)
                string apiUrl = $"{apiBaseUrl}/chitietkhobai/{maKho}";
                var reskho = await client.GetAsync(apiUrl);

                if (!reskho.IsSuccessStatusCode)
                {
                    ViewBag.Error = "Không tìm thấy thông tin kho bãi.";
                    return View(new QuanLyKhobaiModels());
                }

                var contenkho = await reskho.Content.ReadAsStringAsync();
                var khoModel = JsonConvert.DeserializeObject<QuanLyKhobaiModels>(contenkho);

                if (khoModel != null)
                {
                    // 2. Định nghĩa các hàm phụ để gọi API an toàn (Fault Tolerant)
                    // Cách này giúp nếu 1 API lỗi, nó chỉ trả về null chứ không làm crash cả hàm
                    async Task<DiaChiModel?> GetDiaChiSafe(int? maDiaChi)
                    {
                        try
                        {
                            if (maDiaChi <= 0) return null;
                            var res = await client.GetAsync($"{apiDiaChi}/chitietdiachi/{maDiaChi}");
                            if (res.IsSuccessStatusCode)
                            {
                                return JsonConvert.DeserializeObject<DiaChiModel>(await res.Content.ReadAsStringAsync());
                            }
                        }
                        catch { /* Log lỗi ở đây nếu cần */ }
                        return null;
                    }

                    async Task<NguoiDungModel?> GetNhanVienSafe(int? maQuanLy)
                    {
                        try
                        {
                            if (maQuanLy <= 0) return null;
                            var res = await client.GetAsync($"{apiNguoiDung}/chitietnhanvien/{maQuanLy}");
                            if (res.IsSuccessStatusCode)
                            {
                                return JsonConvert.DeserializeObject<NguoiDungModel>(await res.Content.ReadAsStringAsync());
                            }
                        }
                        catch { /* Log lỗi ở đây nếu cần */ }
                        return null;
                    }

                    // 3. Kích hoạt các Task chạy song song
                    var taskDiaChi = GetDiaChiSafe(khoModel.MaDiaChi);
                    var taskNguoiDung = GetNhanVienSafe(khoModel.MaQuanLy);

                    // Chờ cả 2 hoàn thành (dù thành công hay thất bại bên trong nó đã được catch)
                    await Task.WhenAll(taskDiaChi, taskNguoiDung);

                    // 4. Gán dữ liệu vào ViewBag
                    ViewBag.DiaChi = await taskDiaChi;
                    ViewBag.NguoiDung = await taskNguoiDung;
                }

                return View(khoModel);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Lỗi kết nối hệ thống: " + ex.Message;
                return View(new QuanLyKhobaiModels());
            }
        }

        public async Task<IActionResult> Map()
        {
            var client = _httpClientFactory.CreateClient("MyClient");
            try
            {
                // Gọi API danh sách địa chỉ đã được gắn Cache
                var response = await client.GetAsync($"{apiDiaChi}/danhsachdiachi");
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsStringAsync();
                    var danhSachDiaChi = JsonConvert.DeserializeObject<List<DiaChiModel>>(data);
                    // Gửi danh sách sang View để vẽ lên bản đồ
                    return View(danhSachDiaChi);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Lỗi tải bản đồ: " + ex.Message;
            }
            return View(new List<DiaChiModel>());
        }
    }
}
