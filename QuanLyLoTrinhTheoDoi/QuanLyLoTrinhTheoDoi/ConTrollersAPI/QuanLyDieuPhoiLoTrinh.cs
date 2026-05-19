using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using QuanLyLoTrinhTheoDoi.Models;
using QuanLyLoTrinhTheoDoi.Models12;
using QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh;
using QuanLyLoTrinhTheoDoi.Models12.LienServer;
using QuanLyLoTrinhTheoDoi.Models12.LienServer.cs;
using QuanLyLoTrinhTheoDoi.Models12.QuanLyLoTrinh.cs;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.KhoBai;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.PhuongTien;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.TaiXe;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.Xml;
using Tmdt.Shared.Services;
using ClusterResult = QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh.ClusterResult;


namespace QuanLyLoTrinhTheoDoi.ConTrollersAPI
{
    [Route("api/dieuphoilotrinh")]
    [ApiController]
    public class QuanLyDieuPhoiLoTrinh : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyDieuPhoiLoTrinh> _logger;
        private readonly IServiceProvider _serviceProvider;
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();
        private readonly HttpClient _httpClient;
        private readonly IDiaChiService _diaChiService;
        private readonly IDonHangService _donHangService;
        private readonly INhanVienService _nhanVienService;
        private readonly IPhuongTienServiceClient _phuongTienTaiXeService;
        private readonly IKhachHangServiceClient _khachHangService;
        private readonly ISystemService _sys;

        public QuanLyDieuPhoiLoTrinh(TmdtContext context, IMemoryCache cache, IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider,
            ILogger<QuanLyDieuPhoiLoTrinh> logger, HttpClient httpClient, IKhachHangServiceClient khachHangServiceClient, IDiaChiService diaChiService, 
            IDonHangService donHangService, INhanVienService nhanVienService, IPhuongTienServiceClient phuongTienTaiXeService, ISystemService sys)
        { 
            _context = context;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _httpClient = httpClient;
            _serviceProvider = serviceProvider;
            _khachHangService = khachHangServiceClient;
            _diaChiService = diaChiService;
            _donHangService = donHangService;
            _nhanVienService = nhanVienService;
            _phuongTienTaiXeService = phuongTienTaiXeService;
            _sys = sys;
        }
        [HttpGet("check-auth-info")]
        public IActionResult CheckAuthInfo()
        {
            return Ok(new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated,
                UserClaims = User.Claims.Select(c => new { c.Type, c.Value }),
                SessionData = HttpContext.Session.Keys.ToDictionary(k => k, k => HttpContext.Session.GetString(k)),
                CookiesReceived = Request.Cookies.Select(c => new { c.Key, c.Value })
            });
        }

        [HttpGet("danhsachlotrinh")]
        public async Task<IActionResult> GetAllLoTrinh(
            [FromQuery] DateTime? batdau,
            [FromQuery] DateTime? ketthuc,
            [FromQuery] string? TrangThai = "Chờ khởi hành",
            [FromQuery] int? maKho = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var cacheKey = $"GetAllLoTrinh_{batdau?.ToString("yyyyMMdd")}_{ketthuc?.ToString("yyyyMMdd")}_{TrangThai}_{maKho}_{page}_{pageSize}";

                if (_cache.TryGetValue(cacheKey, out var cachedData))
                {
                    return Ok(cachedData);
                }

                // --- BƯỚC 1: LẤY DỮ LIỆU GỐC TỪ SERVER LỘ TRÌNH ---
                var query = _context.LoTrinhs.AsQueryable();

                // (Các bộ lọc giữ nguyên như cũ)
                if (batdau.HasValue) query = query.Where(lt => lt.ThoiGianBatDauKeHoach >= batdau.Value.Date);
                if (ketthuc.HasValue) query = query.Where(lt => lt.ThoiGianBatDauKeHoach <= ketthuc.Value.Date.AddDays(1).AddTicks(-1));
                if (!string.IsNullOrEmpty(TrangThai) && TrangThai != "Tất cả") query = query.Where(lt => lt.TrangThai == TrangThai);
                if (maKho.HasValue && maKho > 0) query = query.Where(lt => lt.MaKhoQuanLy == maKho);

                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                var data = await query
                    .OrderByDescending(tg => tg.ThoiGianBatDauKeHoach)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(sc => new LoTrinhModels
                    {
                        MaLoTrinh = sc.MaLoTrinh,
                        ThoiGianBatDauKeHoach = sc.ThoiGianBatDauKeHoach,
                        TrangThai = sc.TrangThai,
                        MaNguoiDung = sc.MaPtTxNavigation != null ? sc.MaPtTxNavigation.MaNguoiDung : null,
                        MaPhuongTien = sc.MaPtTxNavigation != null ? sc.MaPtTxNavigation.MaPhuongTien : null,
                        TongSoDonHang = sc.ChiTietLoTrinhKienHangs.Count(),
                        TongSoDiemDung = sc.DiemDungs.Count()
                    })
                    .ToListAsync();

                // --- BƯỚC 2: GOM ID VÀ LẤY THÔNG TIN TỪ SERVER KHÁC ---
                if (data.Any())
                {
                    // Lấy danh sách ID duy nhất để tránh gọi trùng lặp
                    var userIds = data.Where(x => x.MaNguoiDung.HasValue).Select(x => x.MaNguoiDung.Value).Distinct().ToList();
                    var vehicleIds = data.Where(x => x.MaPhuongTien.HasValue).Select(x => x.MaPhuongTien.Value).Distinct().ToList();

                    // Gọi song song 2 Task để tối ưu thời gian
                    var usersTask = LayThongTinNguoiDungBulk(userIds);
                    var vehiclesTask = LayThongTinPhuongTienBulk(vehicleIds);

                    await Task.WhenAll(usersTask, vehiclesTask);

                    var userDict = usersTask.Result;
                    var vehicleDict = vehiclesTask.Result;

                    // --- BƯỚC 3: ĐIỀN DỮ LIỆU VÀO DANH SÁCH ---
                    foreach (var item in data)
                    {
                        if (item.MaNguoiDung.HasValue && userDict.ContainsKey(item.MaNguoiDung.Value))
                            item.TenTaiXeThucHien = userDict[item.MaNguoiDung.Value];

                        if (item.MaPhuongTien.HasValue && vehicleDict.ContainsKey(item.MaPhuongTien.Value))
                            item.BienSoXe = vehicleDict[item.MaPhuongTien.Value];
                    }
                }

                var result = new { TotalItems = totalItems, TotalPages = totalPages, CurrentPage = page, Data = data };

                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi GetAllLoTrinh: {ex.Message}");
                return StatusCode(500, new { error = "Lỗi hệ thống", detail = ex.Message });
            }
        }

        // --- CÁC HÀM TRỢ GIÚP GỌI SERVER NGOÀI ---

        private async Task<Dictionary<int, string>> LayThongTinNguoiDungBulk(List<int> ids)
        {
            var dict = new Dictionary<int, string>();
            try
            {
                foreach (var id in ids)
                {
                    // Lưu ý: Nếu có API Bulk ở server Người dùng thì dùng, nếu không thì gọi lẻ trong try-catch
                    var response = await _httpClient.GetFromJsonAsync<TenNhanVienModel>($"https://localhost:7022/api/quanlynguoidung/lay-ten-nhan-vien/{id}");
                    if (response != null) dict[id] = response.TenTaiXeThucHien;
                }
            }
            catch
            {
                _logger.LogWarning("Server NguoiDung không phản hồi.");
            }
            return dict;
        }

        private async Task<Dictionary<int, string>> LayThongTinPhuongTienBulk(List<int> ids)
        {
            var dict = new Dictionary<int, string>();
            try
            {
                foreach (var id in ids)
                {
                    var response = await _httpClient.GetFromJsonAsync<PhuongTienDetailModel>($"http://server-phuongtien/api/chitietthongtinPT/{id}");
                    if (response != null) dict[id] = response.BienSo;
                }
            }
            catch
            {
                _logger.LogWarning("Server PhuongTien không phản hồi.");
            }
            return dict;
        }

        [HttpGet("chi-tiet-lo-trinh/{maLoTrinh}")]
        public async Task<IActionResult> GetChiTietLoTrinh(int? maLoTrinh)
        {
            if (!maLoTrinh.HasValue || maLoTrinh <= 0)
                return BadRequest("Mã lộ trình không hợp lệ.");

            int idLoTrinh = maLoTrinh.Value;

            try
            {
                string cacheKey = $"ChiTietLoTrinh_Full_{idLoTrinh}";
                if (_cache.TryGetValue(cacheKey, out ChiTietLoTrinhModels? cachedData))
                    return Ok(cachedData);

                // 1. Truy vấn Database (Sửa: Thêm AsSplitQuery để dập cảnh báo hiệu năng)
                var loTrinh = await _context.LoTrinhs
                    .Include(lt => lt.ChiTietLoTrinhKienHangs)
                    .Include(lt => lt.DiemDungs)
                    .Include(lt => lt.ChiPhiLoTrinhs)
                    .Include(lt => lt.MaPtTxNavigation) // Đây là bảng PhuongTienTaiXe
                    .AsSplitQuery() // <-- THÊM DÒNG NÀY ĐỂ TỐI ƯU SQL
                    .FirstOrDefaultAsync(lt => lt.MaLoTrinh == idLoTrinh);

                if (loTrinh == null)
                    return NotFound($"Không tìm thấy lộ trình mã: {idLoTrinh}");

                // Sửa: Thêm toán tử an toàn dấu hỏi chấm (?.) đề phòng dữ liệu Null
                var result = new ChiTietLoTrinhModels
                {
                    MaLoTrinh = loTrinh.MaLoTrinh,
                    ThoiGianBatDauKeHoach = loTrinh.ThoiGianBatDauKeHoach,
                    ThoiGianBatDauThucTe = loTrinh.ThoiGianBatDauThucTe,
                    TrangThai = loTrinh.TrangThai,
                    TongSoDonHang = loTrinh.ChiTietLoTrinhKienHangs?.Count ?? 0,
                    TongSoDiemDung = loTrinh.DiemDungs?.Count ?? 0,
                    MaPhuongTien = loTrinh.MaPtTxNavigation?.MaPhuongTien,
                    khoiLuong = loTrinh.TongKhoiLuongKg ?? 0.0, // Nếu null thì mặc định là 0

                    // SỬA TẠI ĐÂY: Thêm ?. để tránh lỗi Object reference not set to an instance of an object
                    MaTaiXeThucHienChinh = loTrinh.MaPtTxNavigation?.MaNguoiDung,
                    MaTaiXeThucHienPhu = loTrinh.MaPtTxNavigation?.MaNguoiDungPhu,

                    ChiPhiLoTrinhs = loTrinh.ChiPhiLoTrinhs?.Select(cp => new ChiPhiLoTrinhModels
                    {
                        SoTien = cp.SoTien,
                        LoaiChiPhi = cp.LoaiChiPhi,
                        GhiChu = cp.GhiChu
                    }).ToList() ?? new List<ChiPhiLoTrinhModels>()
                };

                // --- BƯỚC 2: CHUẨN BỊ CÁC TASK SONG SONG ---
                var allTasks = new List<Task>();

                // A. Task Nhân viên Chính (MaNguoiDung)
                var taskNhanVienChinh = (loTrinh.MaPtTxNavigation != null && _nhanVienService != null)
                    ? _nhanVienService.GetTenNhanVienAsync(loTrinh.MaPtTxNavigation.MaNguoiDung)
                    : Task.FromResult<TenNhanVienModel?>(null);
                allTasks.Add(taskNhanVienChinh);

                // B. Task Nhân viên Phụ (MaNguoiDungPhu)
                var taskNhanVienPhu = (loTrinh.MaPtTxNavigation?.MaNguoiDungPhu != null && _nhanVienService != null)
                    ? _nhanVienService.GetTenNhanVienAsync(loTrinh.MaPtTxNavigation.MaNguoiDungPhu.Value)
                    : Task.FromResult<TenNhanVienModel?>(null);
                allTasks.Add(taskNhanVienPhu);

                // C. Task Phương tiện
                var taskPhuongTien = (loTrinh.MaPtTxNavigation?.MaPhuongTien != null && _phuongTienTaiXeService != null)
                    ? _phuongTienTaiXeService.GetChiTietPhuongTienAsync(loTrinh.MaPtTxNavigation.MaPhuongTien)
                    : Task.FromResult<PhuongTienDetailModel?>(null);
                allTasks.Add(taskPhuongTien);

                // D. Tasks Đơn hàng & Địa chỉ (Thêm dấu ?. phòng hờ cho chắc chắn)
                var dictDonHangTasks = new Dictionary<int, Task<ChiTietDonHangLoTrinhModel?>>();
                if (loTrinh.ChiTietLoTrinhKienHangs != null)
                {
                    foreach (var id in loTrinh.ChiTietLoTrinhKienHangs.Where(ct => ct.MaDonHang.HasValue).Select(ct => ct.MaDonHang!.Value).Distinct())
                    {
                        if (_donHangService != null) { var t = _donHangService.GetChiTietDonHangAsync(id); dictDonHangTasks[id] = t; allTasks.Add(t); }
                    }
                }

                var dictDiaChiTasks = new Dictionary<int, Task<DiaChiModel?>>();
                if (loTrinh.DiemDungs != null)
                {
                    foreach (var id in loTrinh.DiemDungs.Select(dd => dd.MaDiaChi).Distinct())
                    {
                        if (_diaChiService != null) { var t = _diaChiService.GetChiTietDiaChiAsync(id); dictDiaChiTasks[id] = t; allTasks.Add(t); }
                    }
                }

                // --- BƯỚC 3: THỰC THI VÀ MAPPING ---
                try { await Task.WhenAll(allTasks); }
                catch (Exception ex) { _logger.LogWarning("Lỗi gọi liên server: {Msg}", ex.Message); }

                // Gán tên tài xế chính (Lưu ý: thuộc tính TenNguoiDung phải khớp với DTO nhận được từ bài trước)
                if (taskNhanVienChinh.IsCompletedSuccessfully)
                    result.TenTaiXeThucHienChinh = taskNhanVienChinh.Result?.TenTaiXeThucHien;

                // Gán tên tài xế phụ
                if (taskNhanVienPhu.IsCompletedSuccessfully)
                    result.TenTaiXeThucHienPhu = taskNhanVienPhu.Result?.TenTaiXeThucHien;
                // Gán thông tin phương tiện
                if (taskPhuongTien.IsCompletedSuccessfully)
                    result.ThongTinPhuongTien = taskPhuongTien.Result;

                // Map Kiện hàng và Điểm dừng
                result.ChiTietLoTrinhKienHangs = loTrinh.ChiTietLoTrinhKienHangs?.Select(ct => new ChiTietLoTrinhKienHangModels
                {
                    MaDonHang = ct.MaDonHang,
                    TrangThaiTrenXe = ct.TrangThaiTrenXe,
                    ThongTinDonHang = (ct.MaDonHang.HasValue && dictDonHangTasks.TryGetValue(ct.MaDonHang.Value, out var t) && t.IsCompletedSuccessfully) ? t.Result : null
                }).ToList() ?? new List<ChiTietLoTrinhKienHangModels>();

                result.DiemDungs = loTrinh.DiemDungs?.OrderBy(dd => dd.ThuTuDung).Select(dd => new DiemDungModels
                {
                    MaDiaChi = dd.MaDiaChi,
                    ThuTuDung = dd.ThuTuDung,
                    LoaiDung = dd.LoaiDung,
                    DiaChi = (dictDiaChiTasks.TryGetValue(dd.MaDiaChi, out var t) && t.IsCompletedSuccessfully) ? t.Result : null
                }).ToList() ?? new List<DiemDungModels>();

                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tại GetChiTietLoTrinh {Id}", maLoTrinh);
                return StatusCode(500, "Lỗi hệ thống");
            }
        }

        [HttpGet("danhsachganxe/{maPhuongTien}")]
        public async Task<IActionResult> GetDanhSachGanXe(
             int maPhuongTien,
             [FromQuery] int? maCa,
             [FromQuery] bool isActive = true,
             [FromServices] INhanVienService nhanVienService = null,
             [FromServices] IPhuongTienServiceClient phuongTienService = null)
        {
            try
            {
                // 1. Lấy danh sách các bản ghi gán từ Database nội bộ (Server Điều phối)
                var query = _context.PhuongTienTaiXes
                    .AsNoTracking()
                    .Where(x => x.MaPhuongTien == maPhuongTien && x.IsActive == isActive);

                if (maCa.HasValue)
                {
                    query = query.Where(x => x.MaCa == maCa.Value);
                }

                var danhSachCoBan = await query
                    .OrderByDescending(x => x.MaPtTx)
                    .ToListAsync();

                if (danhSachCoBan.Count == 0)
                {
                    return Ok(new { success = true, maCaFilter = maCa, data = new List<object>() });
                }

                // 2. Sử dụng Interface để lấy thông tin bổ trợ từ các Server khác
                // Chúng ta dùng Task.WhenAll để gọi đồng thời, tối ưu hiệu năng
                var tasks = danhSachCoBan.Select(async item =>
                {
                    // 1. Gọi Server Phương tiện
                    // Sửa: Sử dụng đúng kiểu PhuongTienDetailModel? và Task.FromResult với kiểu tương ứng
                    var xeTask = phuongTienService != null
                                 ? phuongTienService.GetChiTietPhuongTienAsync(item.MaPhuongTien)
                                 : Task.FromResult<PhuongTienDetailModel?>(null);

                    // 2. Gọi Server Nhân viên cho tài xế chính
                    var txChinhTask = nhanVienService != null
                                      ? nhanVienService.GetTenNhanVienAsync(item.MaNguoiDung)
                                      : Task.FromResult<TenNhanVienModel?>(null);

                    // 3. Gọi Server Nhân viên cho tài xế phụ
                    var txPhuTask = (item.MaNguoiDungPhu.HasValue && nhanVienService != null)
                                    ? nhanVienService.GetTenNhanVienAsync(item.MaNguoiDungPhu.Value)
                                    : Task.FromResult<TenNhanVienModel?>(null);

                    // Chờ tất cả các Task hoàn thành
                    await Task.WhenAll(xeTask, txChinhTask, txPhuTask);

                    // Lấy kết quả sau khi await
                    var xeInfo = await xeTask;
                    var txChinhInfo = await txChinhTask;
                    var txPhuInfo = await txPhuTask;

                    return new
                    {
                        maPtTx = item.MaPtTx,
                        maPhuongTien = item.MaPhuongTien,
                        bienSo = xeInfo?.BienSo ?? "N/A",
                        
                        maCa = item.MaCa,
                        loaiTuyen = item.LoaiTuyen,
                        maTaiXeChinh = item.MaNguoiDung,
                        tenTaiXeChinh = txChinhInfo?.TenTaiXeThucHien ?? "Không xác định", // Sử dụng đúng thuộc tính TenNguoiDung
                        maTaiXePhu = item.MaNguoiDungPhu,
                        tenTaiXePhu = txPhuInfo?.TenTaiXeThucHien ?? (item.MaNguoiDungPhu.HasValue ? "Không xác định" : null),
                        isActive = item.IsActive
                    };
                });

                var ketQuaFull = await Task.WhenAll(tasks);

                return Ok(new
                {
                    success = true,
                    maCaFilter = maCa,
                    tongSo = ketQuaFull.Length,
                    data = ketQuaFull
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy thông tin gán xe đầy đủ cho phương tiện {MaPhuongTien}", maPhuongTien);
                return StatusCode(500, new { success = false, message = "Lỗi khi tổng hợp dữ liệu từ các dịch vụ." });
            }
        }

        // xem thông tin phuong tiện, tài xế chính, tài xế phụ trong 1 lần gọi thông qua mã phương tiện tài xế
        [HttpGet("GetChiTietGanXeFull/{maPtTx}")]
        public async Task<IActionResult> GetChiTietGanXeFull(
            int maPtTx,
            [FromServices] INhanVienService nhanVienService,
            [FromServices] IPhuongTienServiceClient phuongTienService)
        {
            try
            {
                // 1. Lấy dữ liệu cơ bản từ database nội bộ (Server Điều phối)
                var assignment = await _context.PhuongTienTaiXes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.MaPtTx == maPtTx);

                if (assignment == null)
                    return NotFound(new { success = false, message = "Không tìm thấy bản ghi gán xe này." });

                // 2. Sử dụng Interface để gọi sang Server Phương Tiện lấy thông tin xe (Biển số, loại xe)
                var xeTask = phuongTienService.GetChiTietPhuongTienAsync(assignment.MaPhuongTien);

                // 3. Sử dụng Interface để gọi sang Server Nhân Viên lấy tên tài xế chính
                var txChinhTask = nhanVienService.GetTenNhanVienAsync(assignment.MaNguoiDung);

                // 4. Nếu có tài xế phụ, gọi lấy tên tài xế phụ
                Task<TenNhanVienModel?> txPhuTask = assignment.MaNguoiDungPhu.HasValue
                    ? nhanVienService.GetTenNhanVienAsync(assignment.MaNguoiDungPhu.Value)
                    : Task.FromResult<TenNhanVienModel?>(null);

                // Chạy song song các Task để tối ưu tốc độ
                await Task.WhenAll(xeTask, txChinhTask, txPhuTask);

                var xeInfo = await xeTask;
                var txChinhInfo = await txChinhTask;
                var txPhuInfo = await txPhuTask;

                // 5. Tổng hợp dữ liệu vào DTO
                // 5. Tổng hợp dữ liệu vào DTO (Đảm bảo tên property khớp với JS)
                var result = new
                {
                    maPtTx = assignment.MaPtTx,
                    maPhuongTien = assignment.MaPhuongTien,
                    bienSo = xeInfo?.BienSo ?? "N/A",
                    maCa = assignment.MaCa ?? 0,
                    loaiTuyen = assignment.LoaiTuyen,
                    maTaiXeChinh = assignment.MaNguoiDung,
                    tenTaiXeChinh = txChinhInfo?.TenTaiXeThucHien ?? "Không xác định",
                    maTaiXePhu = assignment.MaNguoiDungPhu,
                    tenTaiXePhu = txPhuInfo?.TenTaiXeThucHien ?? (assignment.MaNguoiDungPhu.HasValue ? "Không xác định" : null),
                    isActive = assignment.IsActive
                };

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết gán xe đầy đủ cho ID: {Id}", maPtTx);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi gọi dữ liệu liên server." });
            }
        }

        [HttpPost("GanPhuongTienTaiXe")]
        public async Task<IActionResult> GanPhuongTien(
            [FromBody] GanPhuongTienRequest request,
            [FromServices] INhanVienService nhanVienService,
            [FromServices] IPhuongTienServiceClient phuongTienService)
        {
            // Đảm bảo luôn trả về JSON để Frontend không bị lỗi Parse
            if (request == null)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            try
            {
                // 1. Lấy tất cả các ca đang hoạt động của PHƯƠNG TIỆN này
                var lichTrinhXe = await _context.PhuongTienTaiXes
                    .Where(x => x.MaPhuongTien == request.MaPhuongTien && x.IsActive == true)
                    .ToListAsync();
                // Lấy ID của người thực hiện thao tác từ hệ thống
                var nguoiThucHienId = _sys.GetCurrentUserId();
                // 2. Kiểm tra xung đột logic ca làm việc dựa trên cấu trúc mới
                if (request.MaCa == 8) // Ca đường dài
                {
                    if (lichTrinhXe.Any())
                        return BadRequest(new { success = false, message = "Xe này hiện đang bận ở một ca làm việc khác. Ca đường dài yêu cầu xe phải trống hoàn toàn cả 3 ca." });
                }
                else // Ca 1, 2 hoặc 3
                {
                    if (lichTrinhXe.Any(x => x.MaCa == 8))
                        return BadRequest(new { success = false, message = "Xe này đang thực hiện lộ trình đường dài (Ca 8), không thể gán thêm ca ngắn." });

                    if (lichTrinhXe.Any(x => x.MaCa == request.MaCa))
                        return BadRequest(new { success = false, message = $"Xe này đã có tài xế trực ca {request.MaCa} rồi." });
                }

                // 3. Kiểm tra tài xế chính có đang bận lái xe nào khác không (IsActive == true)
                bool taiXeDangBan = await _context.PhuongTienTaiXes
                    .AnyAsync(x => x.MaNguoiDung == request.MaNguoiDung && x.IsActive == true);

                if (taiXeDangBan)
                    return BadRequest(new { success = false, message = "Tài xế chính hiện đang trong một ca làm việc khác (đã được gán xe)." });

                // 4. Tạo bản ghi gán mới vào bảng trung gian
                var assignment = new PhuongTienTaiXe
                {
                    MaPhuongTien = request.MaPhuongTien,
                    MaNguoiDung = request.MaNguoiDung,
                    // Chỉ ca đường dài mới bắt buộc/cho phép có tài xế phụ
                    MaNguoiDungPhu = (request.MaCa == 8) ? request.MaNguoiDungPhu : null,
                    MaCa = request.MaCa,
                    LoaiTuyen = request.MaCa == 8 ? "Đường dài" : (request.LoaiTuyen ?? "Nội thành"),
                    IsActive = true,
                    
                };

                _context.PhuongTienTaiXes.Add(assignment);
                await _context.SaveChangesAsync();

                // 5. ĐỒNG BỘ LIÊN SERVER (Gọi sang Server Phương Tiện và Server Nhân Viên)
                // Sử dụng phương thức đã sửa: truyền MaCa và trangThai = true
                var syncTasks = new List<Task>
                {
                    // Cập nhật trạng thái bận cho Tài xế chính
                    nhanVienService.CapNhatTrangThaiTaiXeAsync(request.MaNguoiDung, true),
            
                    // Cập nhật bit ca tương ứng trên server Phương Tiện (Ca1, Ca2, hoặc Ca3)
                    phuongTienService.CapNhatTrangThaiGanXeAsync(request.MaPhuongTien, (int)request.MaCa, true)
                };

                // Nếu có tài xế phụ (thường là ca 8), cập nhật trạng thái bận cho họ luôn
                if (assignment.MaNguoiDungPhu.HasValue)
                {
                    syncTasks.Add(nhanVienService.CapNhatTrangThaiTaiXeAsync(assignment.MaNguoiDungPhu.Value, true));
                }

                // Đợi tất cả các tác vụ đồng bộ hoàn tất
                await Task.WhenAll(syncTasks);
                // --- HOÀN THIỆN PHẦN GHI NHẬT KÝ VÀ RESET CACHE ---
                await _sys.GhiLogVaResetCacheAsync(
                    "Điều phối lộ trình", // Tên chức năng/phân hệ
                    $"Gán phương tiện mã {request.MaPhuongTien} cho tài xế chính {request.MaNguoiDung} tại ca {request.MaCa}.", // Nội dung log hành động
                    "PhuongTienTaiXe", // Tên bảng đích chịu sự tác động
                    assignment.MaPtTx.ToString(), // ID của bản ghi vừa tạo (Đảm bảo thuộc tính khóa chính của bạn đúng tên này)
                    new Dictionary<string, object> { { "Trạng thái", "Chưa gán" } }, // Trạng thái cũ
                    new Dictionary<string, object> {
                        { "MaPhuongTien", assignment.MaPhuongTien },
                        { "MaNguoiDung", assignment.MaNguoiDung },
                        { "MaNguoiDungPhu", assignment.MaNguoiDungPhu },
                        { "MaCa", assignment.MaCa },
                        { "LoaiTuyen", assignment.LoaiTuyen },
                        { "IsActive", assignment.IsActive }

                    } // Trạng thái mới (Dữ liệu vừa lưu)
                );
                return Ok(new
                {
                    success = true,
                    message = "Gán phương tiện và tài xế thành công.",
                    maPhuongTien = request.MaPhuongTien,
                    caDaGan = request.MaCa
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng trong GanPhuongTien cho Xe: {Xe}, Ca: {Ca}", request.MaPhuongTien, request.MaCa);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xử lý gán phương tiện." });
            }
        }

        [HttpDelete("XoaGan/{maPhuongTien}/{maCa}")]
        public async Task<IActionResult> XoaGan(
            int maPhuongTien,
            int maCa,
            [FromServices] INhanVienService nhanVienService,
            [FromServices] IPhuongTienServiceClient phuongTienService)
        {
            // Sử dụng Transaction để đảm bảo tính toàn vẹn khi đóng cũ - tạo mới
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Tìm bản ghi gán đang hoạt động
                var assignment = await _context.PhuongTienTaiXes
                    .FirstOrDefaultAsync(x => x.MaPhuongTien == maPhuongTien && x.MaCa == maCa && x.IsActive == true);

                if (assignment == null)
                    return NotFound(new { success = false, message = "Không tìm thấy dữ liệu gán cho xe này tại ca yêu cầu." });
                // Lấy ID của người thực hiện thao tác từ hệ thống
                var nguoiThucHienId = _sys.GetCurrentUserId();
                int oldMainDriver = assignment.MaNguoiDung;
                var syncTasks = new List<Task>();
                string responseMessage = "";

                // Lưu lại dữ liệu cũ trước khi thay đổi để phục vụ ghi nhật ký
                var trangThaiCuLog = new Dictionary<string, object>
                {
                    { "MaPhuongTien", assignment.MaPhuongTien },
                    { "MaNguoiDung", assignment.MaNguoiDung },
                    { "MaNguoiDungPhu", assignment.MaNguoiDungPhu },
                    { "MaCa", assignment.MaCa },
                    { "IsActive", true }
                };

                // TẤT CẢ CÁC THẾ TRẬN ĐỀU SẼ ĐÓNG BẢN GHI CŨ ĐỂ LƯU LỊCH SỬ
                assignment.IsActive = false;
                _context.PhuongTienTaiXes.Update(assignment);

                // 2. KIỂM TRA TÀI XẾ PHỤ ĐỂ XỬ LÝ LOGIC "ĐÔN NGƯỜI"
                if (assignment.MaNguoiDungPhu.HasValue)
                {
                    // THẾ TRẬN 1: Có tài xế phụ -> Tạo bản ghi mới đôn phụ lên thay chính
                    int newMainDriver = assignment.MaNguoiDungPhu.Value;

                    var newAssignment = new PhuongTienTaiXe
                    {
                        MaPhuongTien = assignment.MaPhuongTien,
                        MaCa = assignment.MaCa,
                        MaNguoiDung = newMainDriver,  // Tài xế phụ cũ trở thành tài xế chính mới
                        MaNguoiDungPhu = null,        // Vị trí tài xế phụ trống
                        LoaiTuyen = assignment.LoaiTuyen,
                        IsActive = true               // Bản ghi mới kích hoạt hoạt động
                                                      // Thêm trường track thời gian nếu cần: CreatedAt = DateTime.UtcNow
                    };

                    await _context.PhuongTienTaiXes.AddAsync(newAssignment);

                    // Đồng bộ: Giải phóng tài xế chính cũ (về trạng thái rảnh)
                    syncTasks.Add(nhanVienService.CapNhatTrangThaiTaiXeAsync(oldMainDriver, false));

                    // Lưu ý: Không giải phóng xe trên server Phương tiện vì xe vẫn hoạt động với tài xế mới
                    responseMessage = $"Đã hủy tài xế chính cũ. Tạo bản ghi lịch sử mới: Tài xế phụ ({assignment.MaNguoiDungPhu}) được đôn lên làm tài xế chính cho xe {maPhuongTien}.";
                }
                else
                {
                    // THẾ TRẬN 2: Không có tài xế phụ -> Giải phóng toàn bộ (Chỉ đóng bản ghi cũ, không tạo mới)

                    // Đồng bộ: Giải phóng tài xế chính cũ
                    syncTasks.Add(nhanVienService.CapNhatTrangThaiTaiXeAsync(oldMainDriver, false));

                    // Đồng bộ: Giải phóng xe trên server Phương tiện (bit trạng thái ca về 0)
                    syncTasks.Add(phuongTienService.CapNhatTrangThaiGanXeAsync(maPhuongTien, maCa, false));

                    responseMessage = $"Đã giải phóng thành công xe {maPhuongTien} và tài xế tại ca {maCa} (Dữ liệu cũ đã lưu vào lịch sử).";
                }

                // Thực thi lưu xuống Database địa phương
                await _context.SaveChangesAsync();

                // Commit Transaction local trước khi gọi API đồng bộ bên ngoài
                await transaction.CommitAsync();

                // 3. Thực thi đồng bộ gọi các server liên quan qua HTTP
                if (syncTasks.Any())
                {
                    await Task.WhenAll(syncTasks);
                }
                // --- HOÀN THIỆN PHẦN GHI NHẬT KÝ CHO PHƯƠNG THỨC XÓA/HỦY GÁN ---
                await _sys.GhiLogVaResetCacheAsync(
                    "Điều phối lộ trình",
                    $"Hủy gán phương tiện xe {maPhuongTien}, ca {maCa}. Chi tiết: {responseMessage}",
                    "PhuongTienTaiXe",
                    assignment.MaPtTx.ToString(),
                    trangThaiCuLog,
                    new Dictionary<string, object> { { "ResponseMessage", responseMessage }, { "IsActive", false } }
                );
                return Ok(new { success = true, message = responseMessage });
            }
            catch (Exception ex)
            {
                // Rollback lại Db local nếu xảy ra lỗi trong khối try
                _logger.LogError(ex, "Lỗi xử lý XoaGan (Lưu lịch sử) cho Xe {MaPhuongTien}, Ca {MaCa}", maPhuongTien, maCa);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xử lý yêu cầu hủy gán lịch sử." });
            }
        }

        [HttpPut("CapNhatGanPhuongTien")]
        public async Task<IActionResult> CapNhatGanPhuongTien(
             [FromBody] CapNhatGanPhuongTienRequest request,
             [FromServices] INhanVienService nhanVienService,
             [FromServices] IPhuongTienServiceClient phuongTienService)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            // Lấy ID của người thực hiện thao tác từ hệ thống
            var nguoiThucHienId = _sys.GetCurrentUserId();

            // Sử dụng Transaction để bảo đảm cả 2 hành động đóng bản ghi cũ và tạo bản ghi mới phải cùng thành công hoặc cùng thất bại
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Tìm bản ghi gán xe đang hoạt động theo Xe và Ca
                var assignment = await _context.PhuongTienTaiXes
                    .FirstOrDefaultAsync(x => x.MaPhuongTien == request.MaPhuongTien
                                          && x.MaCa == request.MaCa
                                          && x.IsActive == true);

                if (assignment == null)
                    return NotFound(new { success = false, message = "Không tìm thấy thông tin gán đang hoạt động cho xe này ở ca đã chọn." });

                // 2. Lưu lại ID tài xế cũ để giải phóng sau khi cập nhật
                int oldMainDriver = assignment.MaNguoiDung;
                int? oldSubDriver = assignment.MaNguoiDungPhu;
                // Lưu lại trạng thái cũ chi tiết phục vụ ghi nhật ký hệ thống
                var trangThaiCuLog = new Dictionary<string, object>
                {
                    { "MaPhuongTien", assignment.MaPhuongTien },
                    { "MaNguoiDung", assignment.MaNguoiDung },
                    { "MaNguoiDungPhu", assignment.MaNguoiDungPhu },
                    { "MaCa", assignment.MaCa },
                    { "LoaiTuyen", assignment.LoaiTuyen },
                    { "IsActive", true }
                };
                // 3. Kiểm tra nếu đổi tài xế chính, thì người mới phải đang rảnh
                if (request.MaNguoiDung != oldMainDriver)
                {
                    var taiXeMoiDangBan = await _context.PhuongTienTaiXes
                        .AnyAsync(x => x.MaNguoiDung == request.MaNguoiDung && x.IsActive == true);

                    if (taiXeMoiDangBan)
                        return BadRequest(new { success = false, message = "Tài xế chính mới hiện đang bận ở một xe hoặc ca làm việc khác." });
                }

                // =========================================================================
                // 4. THỰC HIỆN CƠ CHẾ LƯU LỊCH SỬ TRÊN DATABASE ĐIỀU PHỐI
                // =========================================================================

                // 4.1. Đóng bản ghi cũ (Vô hiệu hóa trạng thái hoạt động)
                assignment.IsActive = false;
                _context.PhuongTienTaiXes.Update(assignment);

                // 4.2. Khởi tạo một bản ghi mới hoàn toàn để lưu thông tin cập nhật mới
                var newAssignment = new PhuongTienTaiXe
                {
                    MaPhuongTien = request.MaPhuongTien,
                    MaCa = request.MaCa,
                    MaNguoiDung = request.MaNguoiDung,
                    MaNguoiDungPhu = request.MaNguoiDungPhu,
                    LoaiTuyen = request.LoaiTuyen ?? assignment.LoaiTuyen, // Dự phòng lấy lại tuyến cũ nếu request truyền null
                    IsActive = true
                    
                };
                await _context.PhuongTienTaiXes.AddAsync(newAssignment);

                // Lưu toàn bộ thay đổi database xuống SQL Server
                await _context.SaveChangesAsync();

                // Xác nhận Transaction thành công cho DB nội bộ trước khi gọi các service HTTP bên ngoài
                await transaction.CommitAsync();

                // =========================================================================
                // 5. ĐỒNG BỘ LIÊN SERVER (Chỉ chạy khi DB local đã lưu an toàn)
                // =========================================================================
                var syncTasks = new List<Task>();

                // Cập nhật trạng thái bit trên Server Phương tiện
                syncTasks.Add(phuongTienService.CapNhatTrangThaiGanXeAsync(request.MaPhuongTien, (int)request.MaCa, true));

                // Xử lý logic giải phóng/khóa Tài xế chính
                if (request.MaNguoiDung != oldMainDriver)
                {
                    syncTasks.Add(nhanVienService.CapNhatTrangThaiTaiXeAsync(oldMainDriver, false)); // Giải phóng người cũ
                    syncTasks.Add(nhanVienService.CapNhatTrangThaiTaiXeAsync(request.MaNguoiDung, true)); // Khóa người mới
                }

                // Xử lý logic giải phóng/khóa Tài xế phụ
                if (request.MaNguoiDungPhu != oldSubDriver)
                {
                    if (oldSubDriver.HasValue)
                        syncTasks.Add(nhanVienService.CapNhatTrangThaiTaiXeAsync(oldSubDriver.Value, false));

                    if (request.MaNguoiDungPhu.HasValue)
                        syncTasks.Add(nhanVienService.CapNhatTrangThaiTaiXeAsync(request.MaNguoiDungPhu.Value, true));
                }

                // Chờ tất cả các tiến trình đồng bộ API hoàn tất
                await Task.WhenAll(syncTasks);
                // --- HOÀN THIỆN PHẦN GHI NHẬT KÝ CHO PHƯƠNG THỨC CẬP NHẬT ---
                await _sys.GhiLogVaResetCacheAsync(
                    "Điều phối lộ trình",
                    $"Cập nhật thông tin gán xe {request.MaPhuongTien}, ca {request.MaCa}. Thay đổi tài xế điều khiển.",
                    "PhuongTienTaiXe",
                    newAssignment.MaPtTx.ToString(), // ID bản ghi mới vừa sinh ra
                    trangThaiCuLog,
                    new Dictionary<string, object> {
                { "MaPhuongTien", newAssignment.MaPhuongTien },
                { "MaNguoiDung", newAssignment.MaNguoiDung },
                { "MaNguoiDungPhu", newAssignment.MaNguoiDungPhu },
                { "MaCa", newAssignment.MaCa },
                { "LoaiTuyen", newAssignment.LoaiTuyen },
                { "IsActive", true }
                    }
                );
                return Ok(new
                {
                    success = true,
                    message = "Cập nhật thông tin gán xe thành công. Bản ghi cũ đã được lưu vào lịch sử hệ thống.",
                    maPhuongTien = request.MaPhuongTien,
                    maCa = request.MaCa
                });
            }
            catch (Exception ex)
            {
                // Khi xảy ra bất kỳ lỗi gì trong khối try, Transaction tự động hủy bỏ (Rollback) các thay đổi chưa được Commit
                _logger.LogError(ex, "Lỗi khi cập nhật gán cho xe {MaPhuongTien}, Ca {MaCa}", request.MaPhuongTien, request.MaCa);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi cập nhật dữ liệu dữ liệu lịch sử." });
            }
        }

        [HttpGet("GetChiTietGanXeByPhuongTienCa/{maPhuongTien}/{maCa}")]
        public async Task<IActionResult> GetChiTietGanXeByPhuongTienCa(
            int maPhuongTien,
            int maCa,
            [FromServices] INhanVienService nhanVienService,
            [FromServices] IPhuongTienServiceClient phuongTienService)
        {
            try
            {
                // 1. Tìm bản ghi gán xe ĐANG HOẠT ĐỘNG (IsActive == true) trong DB nội bộ dựa theo mã xe và mã ca
                var assignment = await _context.PhuongTienTaiXes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.MaPhuongTien == maPhuongTien && x.MaCa == maCa && x.IsActive == true);

                // Nếu không tìm thấy, trả về thông báo lỗi dạng JSON cho JS xử lý công việc trực quan hơn
                if (assignment == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy thông tin gán đang hoạt động cho xe mã {maPhuongTien} ở ca {maCa}."
                    });
                }

                // 2. Sử dụng Interface để gọi sang Server Phương Tiện lấy thông tin xe (Biển số, loại xe)
                var xeTask = phuongTienService.GetChiTietPhuongTienAsync(assignment.MaPhuongTien);

                // 3. Sử dụng Interface để gọi sang Server Nhân Viên lấy tên tài xế chính
                var txChinhTask = nhanVienService.GetTenNhanVienAsync(assignment.MaNguoiDung);

                // 4. Nếu có tài xế phụ, gọi lấy tên tài xế phụ sang Server Nhân Viên
                Task<TenNhanVienModel?> txPhuTask = assignment.MaNguoiDungPhu.HasValue
                    ? nhanVienService.GetTenNhanVienAsync(assignment.MaNguoiDungPhu.Value)
                    : Task.FromResult<TenNhanVienModel?>(null);

                // Kích hoạt chạy song song 3 API liên kết cùng 1 lúc để rút ngắn thời gian chờ xử lý dữ liệu
                await Task.WhenAll(xeTask, txChinhTask, txPhuTask);

                var xeInfo = await xeTask;
                var txChinhInfo = await txChinhTask;
                var txPhuInfo = await txPhuTask;

                // 5. Tổng hợp dữ liệu kết quả thành một Object DTO đồng bộ cấu trúc CamelCase để JavaScript dễ đọc
                var result = new
                {
                    maPtTx = assignment.MaPtTx,
                    maPhuongTien = assignment.MaPhuongTien,
                    bienSo = xeInfo?.BienSo ?? "N/A",
                    maCa = assignment.MaCa ?? 0,
                    loaiTuyen = assignment.LoaiTuyen,
                    maTaiXeChinh = assignment.MaNguoiDung,
                    tenTaiXeChinh = txChinhInfo?.TenTaiXeThucHien ?? "Không xác định",
                    maTaiXePhu = assignment.MaNguoiDungPhu,
                    tenTaiXePhu = txPhuInfo?.TenTaiXeThucHien ?? (assignment.MaNguoiDungPhu.HasValue ? "Không xác định" : null),
                    isActive = assignment.IsActive
                };

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết gán xe cho Xe: {Xe}, Ca: {Ca}", maPhuongTien, maCa);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi truy vấn dữ liệu gán xe liên server." });
            }
        }
    }

}