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
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.Xml;


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

        public QuanLyDieuPhoiLoTrinh(TmdtContext context, IMemoryCache cache, IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider,
            ILogger<QuanLyDieuPhoiLoTrinh> logger, HttpClient httpClient, IKhachHangServiceClient khachHangServiceClient, IDiaChiService diaChiService, IDonHangService donHangService, INhanVienService nhanVienService, IPhuongTienServiceClient phuongTienTaiXeService)
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
                    if (response != null) dict[id] = response.TenNguoiDung;
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

                // 1. Truy vấn Database (Lấy thông tin lộ trình và bảng trung gian PhuongTienTaiXe)
                var loTrinh = await _context.LoTrinhs
                    .Include(lt => lt.ChiTietLoTrinhKienHangs)
                    .Include(lt => lt.DiemDungs)
                    .Include(lt => lt.ChiPhiLoTrinhs)
                    .Include(lt => lt.MaPtTxNavigation) // Đây là bảng PhuongTienTaiXe
                    .FirstOrDefaultAsync(lt => lt.MaLoTrinh == idLoTrinh);

                if (loTrinh == null)
                    return NotFound($"Không tìm thấy lộ trình mã: {idLoTrinh}");

                var result = new ChiTietLoTrinhModels
                {
                    MaLoTrinh = loTrinh.MaLoTrinh,
                    ThoiGianBatDauKeHoach = loTrinh.ThoiGianBatDauKeHoach,
                    ThoiGianBatDauThucTe = loTrinh.ThoiGianBatDauThucTe,
                    TrangThai = loTrinh.TrangThai,
                    TongSoDonHang = loTrinh.ChiTietLoTrinhKienHangs?.Count ?? 0,
                    TongSoDiemDung = loTrinh.DiemDungs?.Count ?? 0,
                    MaPhuongTien = loTrinh.MaPtTxNavigation?.MaPhuongTien,
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

                // B. Task Nhân viên Phụ (MaNguoiDungPhu) - MỚI BỔ SUNG
                var taskNhanVienPhu = (loTrinh.MaPtTxNavigation?.MaNguoiDungPhu != null && _nhanVienService != null)
                    ? _nhanVienService.GetTenNhanVienAsync(loTrinh.MaPtTxNavigation.MaNguoiDungPhu.Value)
                    : Task.FromResult<TenNhanVienModel?>(null);
                allTasks.Add(taskNhanVienPhu);

                // C. Task Phương tiện
                var taskPhuongTien = (loTrinh.MaPtTxNavigation?.MaPhuongTien != null && _phuongTienTaiXeService != null)
                    ? _phuongTienTaiXeService.GetChiTietPhuongTienAsync(loTrinh.MaPtTxNavigation.MaPhuongTien)
                    : Task.FromResult<PhuongTienDetailModel?>(null);
                allTasks.Add(taskPhuongTien);

                // D. Tasks Đơn hàng & Địa chỉ (Giữ nguyên logic Dictionary của bạn)
                var dictDonHangTasks = new Dictionary<int, Task<ChiTietDonHangLoTrinhModel?>>();
                foreach (var id in loTrinh.ChiTietLoTrinhKienHangs.Where(ct => ct.MaDonHang.HasValue).Select(ct => ct.MaDonHang!.Value).Distinct())
                {
                    if (_donHangService != null) { var t = _donHangService.GetChiTietDonHangAsync(id); dictDonHangTasks[id] = t; allTasks.Add(t); }
                }

                var dictDiaChiTasks = new Dictionary<int, Task<DiaChiModel?>>();
                foreach (var id in loTrinh.DiemDungs.Select(dd => dd.MaDiaChi).Distinct())
                {
                    if (_diaChiService != null) { var t = _diaChiService.GetChiTietDiaChiAsync(id); dictDiaChiTasks[id] = t; allTasks.Add(t); }
                }

                // --- BƯỚC 3: THỰC THI VÀ MAPPING ---
                try { await Task.WhenAll(allTasks); }
                catch (Exception ex) { _logger.LogWarning("Lỗi gọi liên server: {Msg}", ex.Message); }

                // Gán tên tài xế chính
                if (taskNhanVienChinh.IsCompletedSuccessfully)
                    result.TenTaiXeThucHienChinh = taskNhanVienChinh.Result?.TenNguoiDung;

                // Gán tên tài xế phụ
                if (taskNhanVienPhu.IsCompletedSuccessfully)
                    result.TenTaiXeThucHienPhu = taskNhanVienPhu.Result?.TenNguoiDung;

                // Gán thông tin phương tiện
                if (taskPhuongTien.IsCompletedSuccessfully)
                    result.ThongTinPhuongTien = taskPhuongTien.Result;

                // Map Kiện hàng và Điểm dừng
                result.ChiTietLoTrinhKienHangs = loTrinh.ChiTietLoTrinhKienHangs.Select(ct => new ChiTietLoTrinhKienHangModels
                {
                    MaDonHang = ct.MaDonHang,
                    TrangThaiTrenXe = ct.TrangThaiTrenXe,
                    ThongTinDonHang = (ct.MaDonHang.HasValue && dictDonHangTasks.TryGetValue(ct.MaDonHang.Value, out var t) && t.IsCompletedSuccessfully) ? t.Result : null
                }).ToList();

                result.DiemDungs = loTrinh.DiemDungs.OrderBy(dd => dd.ThuTuDung).Select(dd => new DiemDungModels
                {
                    MaDiaChi = dd.MaDiaChi,
                    ThuTuDung = dd.ThuTuDung,
                    LoaiDung = dd.LoaiDung,
                    DiaChi = (dictDiaChiTasks.TryGetValue(dd.MaDiaChi, out var t) && t.IsCompletedSuccessfully) ? t.Result : null
                }).ToList();

                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tại GetChiTietLoTrinh {Id}", maLoTrinh);
                return StatusCode(500, "Lỗi hệ thống");
            }
        }


        // thực hiện gắn phương tiện vói tài xế , ca và tuyến
        [HttpPost("GanPhuongTienTaiXe")]
        public async Task<IActionResult> GanPhuongTien([FromBody] GanPhuongTienRequest request)
        {
            if (request == null) return BadRequest("Dữ liệu không hợp lệ.");

            try
            {
                // 1. Kiểm tra logic nghiệp vụ: Phương tiện này đã có ai lái và đang hoạt động (IsActive) không?
                var phuongTienDangBan = await _context.PhuongTienTaiXes
                    .AnyAsync(x => x.MaPhuongTien == request.MaPhuongTien && x.IsActive == true);

                if (phuongTienDangBan)
                {
                    return BadRequest("Phương tiện này hiện đang được sử dụng bởi một tài xế khác.");
                }

                // 2. Kiểm tra logic nghiệp vụ: Tài xế này đã có xe nào đang hoạt động không? (Tùy chọn)
                var taiXeDangBan = await _context.PhuongTienTaiXes
                    .AnyAsync(x => x.MaNguoiDung == request.MaNguoiDung && x.IsActive == true);

                if (taiXeDangBan)
                {
                    return BadRequest("Tài xế này hiện đã được gán cho một phương tiện khác.");
                }

                // 3. Khởi tạo đối tượng Entity từ Request
                var assignment = new PhuongTienTaiXe
                {
                    MaPhuongTien = request.MaPhuongTien,
                    MaNguoiDung = request.MaNguoiDung,
                    MaNguoiDungPhu = request.MaNguoiDungPhu,
                    MaCa = request.MaCa,
                    LoaiTuyen = request.LoaiTuyen,
                    IsActive = true // Luôn để true khi mới gán
                };

                // 4. Lưu vào Database
                _context.PhuongTienTaiXes.Add(assignment);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Gán phương tiện cho tài xế thành công.",
                    data = new
                    {
                        Id = assignment.MaPtTx,
                        assignment.MaPhuongTien,
                        assignment.MaNguoiDung,
                        assignment.MaCa
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu dữ liệu gán phương tiện");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống nội bộ." });
            }
        }

        [HttpGet("danhsachganxe/{maPhuongTien}")]
        public async Task<IActionResult> GetDanhSachGanXe(int maPhuongTien)
        {
            try
            {
                var ganXe = await _context.PhuongTienTaiXes
                    .Where(x => x.MaPhuongTien == maPhuongTien)
                    .Select(x => new Models12.ThongTinLienServer.PhuongTienTaiXeModels
                    {
                        MaPhuongTien = x.MaPhuongTien,
                        LoaiTuyen = x.LoaiTuyen,
                        MaNguoiDung = x.MaNguoiDung,
                        MaNguoiDungPhu = x.MaNguoiDungPhu,
                        MaCa = x.MaCa,
                        IsActive = x.IsActive
                    })
                    .FirstOrDefaultAsync();
                if (ganXe == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy thông tin gán xe." });
                }
                return Ok(new { success = true, data = ganXe });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách gán xe cho MaPtTx {MaPtTx}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống nội bộ." });
            }
        }

        [HttpDelete("XoaGan/{maPhuongTien}")]
        public async Task<IActionResult> XoaGan(int maPhuongTien)
        {
            try
            {
                // Tìm bản ghi gán xe đang hoạt động của phương tiện này
                var assignment = await _context.PhuongTienTaiXes
                    .FirstOrDefaultAsync(x => x.MaPhuongTien == maPhuongTien && x.IsActive == true);

                if (assignment == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy dữ liệu gán để xóa." });
                }

                // Toán có 2 cách: Xóa hẳn (Hard Delete) hoặc Đổi IsActive = false (Soft Delete)
                // Ở đây mình ví dụ Xóa hẳn để bảng dữ liệu gọn nhẹ
                _context.PhuongTienTaiXes.Remove(assignment);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Đã xóa thông tin gán phương tiện." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa gán cho xe {MaPhuongTien}", maPhuongTien);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xóa." });
            }
        }

        [HttpPut("CapNhatGanPhuongTien")]
        public async Task<IActionResult> CapNhatGanPhuongTien([FromBody] CapNhatGanPhuongTienRequest request)
        {
            if (request == null) return BadRequest("Dữ liệu không hợp lệ.");

            try
            {
                // 1. Tìm bản ghi gán xe đang hoạt động của phương tiện này
                var assignment = await _context.PhuongTienTaiXes
                    .FirstOrDefaultAsync(x => x.MaPhuongTien == request.MaPhuongTien && x.IsActive == true);

                if (assignment == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy thông tin gán xe đang hoạt động để cập nhật." });
                }

                // 2. Kiểm tra logic: Tài xế mới (MaNguoiDung) có đang lái xe nào khác không?
                // Loại trừ chính bản ghi hiện tại ra (để nếu không đổi tài xế mà chỉ đổi ca/tuyến thì không bị lỗi)
                var taiXeDangBan = await _context.PhuongTienTaiXes
                    .AnyAsync(x => x.MaNguoiDung == request.MaNguoiDung
                                   && x.IsActive == true
                                   && x.MaPhuongTien != request.MaPhuongTien);

                if (taiXeDangBan)
                {
                    return BadRequest("Tài xế mới hiện đã được gán cho một phương tiện khác.");
                }

                // 3. Cập nhật thông tin
                assignment.MaNguoiDung = request.MaNguoiDung;
                assignment.MaNguoiDungPhu = request.MaNguoiDungPhu;
                assignment.MaCa = request.MaCa;
                assignment.LoaiTuyen = request.LoaiTuyen;
                // assignment.IsActive giữ nguyên là true

                // 4. Lưu thay đổi
                _context.PhuongTienTaiXes.Update(assignment);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật thông tin gán phương tiện thành công.",
                    data = new
                    {
                        assignment.MaPtTx,
                        assignment.MaPhuongTien,
                        assignment.MaNguoiDung,
                        assignment.MaCa
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật gán phương tiện cho xe {MaPhuongTien}", request.MaPhuongTien);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống nội bộ." });
            }
        }


        // thực hiện điều phối đơn hàng cho lộ trình tuyến và ca đã chọn
        [HttpPost("tu-dong-gom-nhom")]
        public async Task<IActionResult> TuDongGomNhomDonHang([FromBody] DieuPhoiRequest request)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TmdtContext>();
            var ptTxService = scope.ServiceProvider.GetRequiredService<IPhuongTienTaiXeService>();

            var clientDH = _httpClientFactory.CreateClient("DonHangApi");
            var clientKho = _httpClientFactory.CreateClient("KhoApi");
            var clientPT = _httpClientFactory.CreateClient("PhuongTienApi");
            var clientNS = _httpClientFactory.CreateClient("NhanSuApi");

            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. Lấy danh sách cụm đơn hàng
                var resDonHang = await clientDH.PostAsJsonAsync("api/quanlydonhang/cho-dieu-phoi", new { });
                var responseData = await resDonHang.Content.ReadFromJsonAsync<DonHangResponse>();
                if (responseData?.Clusters == null || !responseData.Clusters.Any()) return NotFound("Không có đơn hàng chờ điều phối.");

                // --- DÒNG SỬA CHÍNH: Lọc theo số lượng đơn hàng yêu cầu ---
                if (request?.Limit > 0)
                {
                    int currentTotal = 0;
                    responseData.Clusters = responseData.Clusters
                        .TakeWhile(c => {
                            bool keep = currentTotal < request.Limit;
                            currentTotal += c.SoLuongDonHang;
                            return keep;
                        }).ToList();
                }

                // --- TỐI ƯU BƯỚC 2 & 4: GOM TẤT CẢ ID ĐỊA CHỈ ĐỂ GỌI BATCH API ---
                var allAddressIds = responseData.Clusters
                    .Select(c => c.MaDiaChiLayHang > 0 ? c.MaDiaChiLayHang : c.MaDiaChiCum)
                    .Where(id => id > 0).Distinct().ToList();

                // CHỈ GỌI API KHO 1 LẦN DUY NHẤT (Batch Call)
                var resBatchKho = await clientKho.PostAsJsonAsync("api/quanlykhobai/tim-kho-theo-lo", new { MaDiaChis = allAddressIds });
                if (!resBatchKho.IsSuccessStatusCode) return BadRequest("Lỗi kết nối Service Kho.");

                var warehouseMap = await resBatchKho.Content.ReadFromJsonAsync<Dictionary<string, KhoGanNhatResponse>>();
                // Lưu ý: JSON Key thường là string khi deserialize Dictionary

                var clustersByWarehouse = new Dictionary<int, List<ClusterResult>>();
                var warehouseInfoMap = new Dictionary<int, KhoGanNhatResponse>();

                // 2. Phân loại cụm đơn hàng vào từng kho dựa trên kết quả Batch
                foreach (var c in responseData.Clusters)
                {
                    int idDiaChi = c.MaDiaChiLayHang > 0 ? c.MaDiaChiLayHang : c.MaDiaChiCum;

                    if (warehouseMap != null && warehouseMap.TryGetValue(idDiaChi.ToString(), out var kho))
                    {
                        if (!clustersByWarehouse.ContainsKey(kho.MaKho))
                        {
                            clustersByWarehouse[kho.MaKho] = new List<ClusterResult>();
                            warehouseInfoMap[kho.MaKho] = kho;
                        }
                        c.MaDiaChiLayHang = idDiaChi; // Cập nhật lại ID thực tế
                                                      // Thay vì add trực tiếp, ta khởi tạo đối tượng ClusterResult mới từ dữ liệu Api
                        clustersByWarehouse[kho.MaKho].Add(new ClusterResult
                        {
                            MaVungH3 = c.MaVungH3,
                            SoLuongDonHang = c.SoLuongDonHang,
                            TongKhoiLuong = c.TongKhoiLuong,
                            TongTheTich = c.TongTheTich,
                            DanhSachMaDonHang = c.DanhSachMaDonHang,
                            MaDiaChiLayHang = idDiaChi // Sử dụng biến idDiaChi đã xác định
                        });
                    }
                }

                var finalProcessedData = new List<object>();
                var skippedClusters = new List<object>();
                int maCaHienTai = xacDinhMaCaTheoGio(DateTime.Now);

                // 3. XỬ LÝ ĐIỀU PHỐI CHI TIẾT CHO TỪNG KHO
                // 3. XỬ LÝ ĐIỀU PHỐI CHI TIẾT CHO TỪNG KHO
                foreach (var entry in clustersByWarehouse)
                {
                    int maKhoId = entry.Key;
                    var danhSachCumCuaKho = entry.Value;
                    var khoInfo = warehouseInfoMap[maKhoId];

                    // --- BƯỚC MỚI: PHÂN NHÓM CỤM THEO ĐÍCH ĐẾN TIẾP THEO (NEXT HOP) ---
                    // Logic: Nếu đang ở kho bé -> Đích là Kho Chính Vùng.
                    // Nếu đang ở Kho Chính -> Đích là Kho Phụ (nếu cùng vùng) hoặc Kho Chính Vùng Khác.
                    var groupsByNextDestination = danhSachCumCuaKho.GroupBy(c => IdentifyNextHop(khoInfo, c.MaVungH3));

                    foreach (var group in groupsByNextDestination)
                    {
                        int maKhoDich = group.Key;
                        var danhSachCumTheoHuong = group.ToList();

                        // Lấy tài nguyên xe và tài xế của kho hiện tại
                        var xeCuaKho = await clientPT.GetFromJsonAsync<List<VehicleFreeDto>>($"api/quanlyxe/xe-san-sang-dieu-phoi?maKho={maKhoId}") ?? new();
                        var txCuaKho = await clientNS.GetFromJsonAsync<List<DriverAvailableDto>>($"api/quanlytaixe/lich-trinh-tai-xe?maKho={maKhoId}") ?? new();

                        // 4. Thuật toán Bin Packing cho nhóm hướng đi này
                        var assignments = ApplyFirstFitDecreasingBPP(danhSachCumTheoHuong, xeCuaKho);

                        var assignedClusters = assignments.SelectMany(a => a.Value).ToList();
                        var unassignedClusters = danhSachCumTheoHuong.Except(assignedClusters).ToList();

                        // PHASE 4.1: TẠO LỘ TRÌNH CHO CÁC CỤM ĐÃ CÓ XE
                        foreach (var assign in assignments)
                        {
                            var xeSelected = assign.Key;
                            var clustersInXe = assign.Value;
                            DriverAvailableDto? txChon = null;

                            var mapping = await ptTxService.GetMappingByVehicleAsync(xeSelected.MaPhuongTien, maCaHienTai);
                            if (mapping != null)
                                txChon = txCuaKho.FirstOrDefault(t => t.MaNguoiDung == mapping.MaNguoiDung);

                            if (txChon == null)
                                txChon = txCuaKho.OrderByDescending(t => t.DiemUyTin).FirstOrDefault();

                            bool isFullResource = txChon != null;
                            int? mappingPhuongTienTaiXeId = isFullResource ? (mapping?.MaPtTx) : null;

                            if (isFullResource) txCuaKho.Remove(txChon);
                            else skippedClusters.Add(new { Kho = khoInfo.TenKho, LyDo = $"Xe {xeSelected.BienSo} thiếu tài xế đi hướng Kho {maKhoDich}" });

                            var loTrinhMoi = new LoTrinh
                            {
                                MaPtTx = mappingPhuongTienTaiXeId,
                                TrangThai = isFullResource ? "Chờ khởi hành" : "Chờ điều phối thủ công",
                                ThoiGianBatDauKeHoach = DateTime.Now,
                                GhiChu = $"Tự động: {khoInfo.TenKho} -> Kho {maKhoDich}",
                                LoTrinhTuyen = true,
                                MaKhoQuanLy = maKhoId
                            };

                            context.LoTrinhs.Add(loTrinhMoi);
                            await context.SaveChangesAsync();

                            // CẬP NHẬT: Truyền thêm maKhoDich vào hàm tạo điểm dừng
                            await TaoDiemDungVaChiPhi(context, loTrinhMoi, khoInfo, maKhoDich, clustersInXe, xeSelected);

                            if (isFullResource)
                            {
                                await Task.WhenAll(
                                    clientPT.PostAsJsonAsync($"api/quanlyxe/cap-nhat-trang-thai-xe/{xeSelected.MaPhuongTien}", new { TrangThai = "Chờ khởi hành" }),
                                    clientNS.PostAsJsonAsync("api/quanlytaixe/cap-nhat-trang-thai", new { MaNguoiDung = txChon.MaNguoiDung, TrangThaiMoi = "Đang hoạt động" })
                                );
                            }

                            await clientDH.PutAsJsonAsync("api/quanlydonhang/cap-nhat-trang-thai-nhieu", new
                            {
                                DanhSachMaDonHang = clustersInXe.SelectMany(x => x.DanhSachMaDonHang).ToList(),
                                TrangThaiMoi = "Đã lên lộ trình"
                            });
                        }

                        // PHASE 4.2: LỘ TRÌNH KHUYẾT (Thiếu xe)
                        foreach (var cluster in unassignedClusters)
                        {
                            var loTrinhKhuyet = new LoTrinh
                            {
                                TrangThai = "Chờ điều phối thủ công",
                                ThoiGianBatDauKeHoach = DateTime.Now,
                                GhiChu = $"Thiếu xe đi hướng Kho {maKhoDich}",
                                LoTrinhTuyen = true,
                                MaKhoQuanLy = maKhoId
                            };
                            context.LoTrinhs.Add(loTrinhKhuyet);
                            await context.SaveChangesAsync();

                            await TaoDiemDungVaChiPhi(context, loTrinhKhuyet, khoInfo, maKhoDich, new List<ClusterResult> { cluster }, null);
                        }
                    }
                }

                await transaction.CommitAsync();
                return Ok(new
                {
                    status = skippedClusters.Any() ? "Warning" : "Success",
                    message = skippedClusters.Any() ? "Điều phối hoàn tất một phần, một số đơn hàng chưa có xe/tài xế." : "Đã điều phối toàn bộ đơn hàng.",
                    data = finalProcessedData,
                    unassignedReport = skippedClusters // Thông tin chi tiết về việc thiếu hụt
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, ex.Message);
            }
        }

        [NonAction]
        public int xacDinhMaCaTheoGio(DateTime checkTime)
        {
            var currentTime = TimeOnly.FromDateTime(checkTime);

            // Khởi tạo danh sách dựa trên dữ liệu mới bạn vừa INSERT
            var danhSachCa = new List<CaTrucConfig>
            {
                // Nhóm 1: Ca tiêu chuẩn (Ưu tiên cao nhất - Priority 1)
                new() { MaCa = 1, TenCa = "Ca Sáng tiêu chuẩn", GioBatDau = new TimeOnly(6, 0), GioKetThuc = new TimeOnly(14, 0), Priority = 1 },
                new() { MaCa = 2, TenCa = "Ca Chiều tiêu chuẩn", GioBatDau = new TimeOnly(14, 0), GioKetThuc = new TimeOnly(22, 0), Priority = 1 },
                new() { MaCa = 3, TenCa = "Ca Đêm tiêu chuẩn", GioBatDau = new TimeOnly(22, 0), GioKetThuc = new TimeOnly(6, 0), Priority = 1 },

                // Nhóm 2: Ca Full ngày (Priority 2)
                new() { MaCa = 4, TenCa = "Ca Full ngày (Hành chính)", GioBatDau = new TimeOnly(8, 0), GioKetThuc = new TimeOnly(17, 0), Priority = 2 },
                new() { MaCa = 5, TenCa = "Ca Full ngày (Vận tải nội thành)", GioBatDau = new TimeOnly(7, 0), GioKetThuc = new TimeOnly(19, 0), Priority = 2 },

                // Nhóm 3: Ca Chuyến dài (Priority 3)
                new() { MaCa = 6, TenCa = "Ca Chuyến dài (Liên tỉnh 1)", GioBatDau = new TimeOnly(5, 0), GioKetThuc = new TimeOnly(21, 0), Priority = 3 },
                new() { MaCa = 7, TenCa = "Ca Chuyến dài (Xuyên đêm)", GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(10, 0), Priority = 3 },
                new() { MaCa = 8, TenCa = "Ca Chuyến dài (Linh hoạt 24h)", GioBatDau = new TimeOnly(0, 0), GioKetThuc = new TimeOnly(23, 59, 59), Priority = 4 }
            };

            var matches = danhSachCa.Where(ca =>
            {
                if (ca.GioBatDau < ca.GioKetThuc)
                {
                    // Ca bình thường (không xuyên ngày)
                    return currentTime >= ca.GioBatDau && currentTime <= ca.GioKetThuc;
                }
                else
                {
                    // Ca xuyên đêm (Ví dụ: Ca 3 hoặc Ca Chuyến dài xuyên đêm)
                    return currentTime >= ca.GioBatDau || currentTime <= ca.GioKetThuc;
                }
            })
            .OrderBy(ca => ca.Priority) // Chọn ca tiêu chuẩn trước, ca linh hoạt sau
            .ThenBy(ca =>
            {
                // Tính độ dài ca để ưu tiên ca ngắn hơn (chi tiết hơn)
                var duration = ca.GioKetThuc.ToTimeSpan() - ca.GioBatDau.ToTimeSpan();
                return duration.Ticks < 0 ? duration.Add(TimeSpan.FromDays(1)) : duration;
            })
            .ToList();

            // Mặc định trả về MaCa 8 (Linh hoạt 24h) nếu không khớp ca nào đặc thù
            return matches.FirstOrDefault()?.MaCa ?? 8;
        }

        [NonAction] // Nếu nằm trong Controller
        private int IdentifyNextHop(KhoGanNhatResponse currentKho, string destinationH3)
        {
            // 1. Lấy mã vùng của đích đến từ mã H3 (Ví dụ: Miền Bắc là 'North', Miền Trung là 'Central'...)
            string destRegion = GetRegionFromH3(destinationH3);

            // 2. LOGIC ĐIỀU PHỐI (HUB-AND-SPOKE)

            // TRƯỜNG HỢP A: Đang ở Kho bé (Spoke) 
            // -> Luôn đẩy về Kho cha (Hub vùng) để tập kết hàng
            if (currentKho.LoaiKho == "KHO_BE")
            {
                return currentKho.MaKhoCha ?? currentKho.MaKho;
            }

            // TRƯỜNG HỢP B: Đang ở Kho chính (Regional Hub)
            if (currentKho.MaVung == destRegion)
            {
                // Nếu đã tới đúng vùng miền của khách -> Chuyển về Kho phụ (Delivery Hub) gần khách nhất
                return TimMaKhoPhuPhuHop(destinationH3);
            }
            else
            {
                // Nếu khác vùng miền (Vd: Hàng từ Bắc vào Nam) -> Chuyển tới Kho chính của vùng đích
                return TimMaKhoChinhCuaVung(destRegion);
            }
        }

        private async Task TaoDiemDungVaChiPhi(
            TmdtContext context,
            LoTrinh loTrinh,
            KhoGanNhatResponse khoInfo, // Kho nguồn
            int maKhoDich,               // ID Kho trung tâm đích
            List<ClusterResult> clusters,
            VehicleFreeDto? xe)
        {
            try
            {
                var clientKH = _httpClientFactory.CreateClient("KhachHangApi");
                var clientKho = _httpClientFactory.CreateClient("KhoApi");

                // 1. LẤY THÔNG TIN KHO ĐÍCH (KHO TRUNG TÂM)
                // Sửa dòng này trong hàm TaoDiemDungVaChiPhi
                
                var resKhoDich = await clientKho.GetFromJsonAsync<KhoGanNhatResponse>($"api/quanlykhobai/chitietkhobai/{maKhoDich}");
                if (resKhoDich == null) throw new Exception("Kho đích không tồn tại.");

                // 2. LẤY TỌA ĐỘ TẬP TRUNG
                var listIds = new List<int> { khoInfo.MaDiaChi }; // Start: Index 0
                listIds.AddRange(clusters.Select(c => c.MaDiaChiLayHang)); // Mid points
                listIds.Add(resKhoDich.MaDiaChi); // End: Index N-1

                var uniqueIds = listIds.Distinct().ToList();
                // Sửa dòng này trong hàm TaoDiemDungVaChiPhi
                var response = await clientKH.PostAsJsonAsync("/api/quanlydiachi/lay-toa-do-danh-sach", uniqueIds);
                var toaDoData = await response.Content.ReadFromJsonAsync<List<ToaDoResponseDto>>();

                if (toaDoData == null) throw new Exception("Lỗi lấy tọa độ.");

                // Map lại danh sách tọa độ theo đúng thứ tự listIds để xác định Start/End
                var orderedToaDo = listIds.Select(id => toaDoData.First(t => t.MaDiaChi == id)).ToList();

                // 3. TÍNH MA TRẬN KHOẢNG CÁCH
                int n = orderedToaDo.Count;
                long[,] distanceMatrix = new long[n, n];
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j) distanceMatrix[i, j] = 0;
                        else
                        {
                            double d = TinhKhoangCach(
                                orderedToaDo[i].ViDo ?? 0, orderedToaDo[i].KinhDo ?? 0,
                                orderedToaDo[j].ViDo ?? 0, orderedToaDo[j].KinhDo ?? 0);
                            distanceMatrix[i, j] = (long)(d * 1.3 * 1000); // Quy ra mét + bù sai số
                        }
                    }
                }

                // 4. GIẢI TSP: ĐIỂM ĐẦU (0), ĐIỂM CUỐI (n-1)
                var path = SolveTSPWithOrTools(distanceMatrix, 0, n - 1);

                // 5. LƯU ĐIỂM DỪNG & TỔNG KM
                double tongKm = 0;
                for (int i = 0; i < path.Count; i++)
                {
                    int idx = path[i];
                    if (i > 0) tongKm += (distanceMatrix[path[i - 1], idx] / 1.3) / 1000.0;

                    await context.DiemDungs.AddAsync(new DiemDung
                    {
                        MaLoTrinh = loTrinh.MaLoTrinh,
                        MaDiaChi = orderedToaDo[idx].MaDiaChi,
                        ThuTuDung = i + 1,
                        LoaiDung = (idx == 0) ? "Kho xuất phát" :
                               (idx == n - 1) ? "Kho trung tâm" : "Điểm lấy hàng",
                        EtaKeHoach = DateTime.Now.AddMinutes(30 + (i * 40))
                    });
                }
                var tatCaMaDonHang = clusters.SelectMany(c => c.DanhSachMaDonHang).ToList();

                foreach (var maDH in tatCaMaDonHang)
                {
                    // Tên bảng có thể là ChiTietLoTrinh hoặc KienHang tùy DB của bạn
                    await context.ChiTietLoTrinhKienHangs.AddAsync(new ChiTietLoTrinhKienHang
                    {
                        MaLoTrinh = loTrinh.MaLoTrinh,
                        MaDonHang = maDH,
                        TrangThaiTrenXe = "Chờ lấy hàng"
                    });
                }
                // 6. CHI PHÍ NHIÊN LIỆU (Lấy giá thực tế từ hàm bạn đã viết)
                decimal giaXang = await GetCurrentFuelPriceAsync("DO");
                decimal dinhMuc = (xe?.TaiTrongToiDaKg > 5000) ? 0.22m : 0.15m;

                await context.ChiPhiLoTrinhs.AddAsync(new ChiPhiLoTrinh
                {
                    MaLoTrinh = loTrinh.MaLoTrinh,
                    SoTien = Math.Round((decimal)tongKm * dinhMuc * giaXang, 0),
                    LoaiChiPhi = "XANG_DAU",
                    GhiChu = $"Lộ trình về kho trung tâm. Tổng: {Math.Round(tongKm, 1)}km"
                });

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }



        // 1. Sửa hàm tính khoảng cách cho đồng nhất tên gọi
        private double TinhKhoangCach(double lat1, double lon1, double lat2, double lon2)
            => TinhKhoangCachHaversine(lat1, lon1, lat2, lon2);

        // 2. Cập nhật hàm SolveTSP để nhận 3 tham số
        private List<int> SolveTSPWithOrTools(long[,] distanceMatrix, int startIndex, int endIndex)
        {
            int numLocations = distanceMatrix.GetLength(0);
            if (numLocations <= 1) return new List<int> { 0 };

            // Khởi tạo Manager với điểm bắt đầu và điểm kết thúc cụ thể
            // Tham số: số điểm, số xe (1), mảng điểm bắt đầu, mảng điểm kết thúc
            RoutingIndexManager manager = new RoutingIndexManager(
                numLocations,
                1,
                new int[] { startIndex },
                new int[] { endIndex }
            );

            RoutingModel routing = new RoutingModel(manager);

            int transitCallbackIndex = routing.RegisterTransitCallback((long fromIndex, long toIndex) => {
                var fromNode = manager.IndexToNode(fromIndex);
                var toNode = manager.IndexToNode(toIndex);
                return distanceMatrix[fromNode, toNode];
            });

            routing.SetArcCostEvaluatorOfAllVehicles(transitCallbackIndex);

            RoutingSearchParameters searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
            searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;

            Assignment solution = routing.SolveWithParameters(searchParameters);

            List<int> result = new List<int>();
            if (solution != null)
            {
                var index = routing.Start(0);
                while (routing.IsEnd(index) == false)
                {
                    result.Add(manager.IndexToNode((int)index));
                    index = solution.Value(routing.NextVar(index));
                }
                result.Add(manager.IndexToNode((int)index)); // Thêm điểm kết thúc (Kho đích)
            }
            return result;
        }

        private double TinhKhoangCachHaversine(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371; // Bán kính Trái Đất (km)
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double angle) => (Math.PI / 180) * angle;

        // 2. Thêm hàm GetCurrentFuelPriceAsync (để hết lỗi 'does not exist')
        private async Task<decimal> GetCurrentFuelPriceAsync(string fuelType)
        {
            // Mock dữ liệu: thực tế có thể gọi API Petrolimex hoặc lưu trong DB cấu hình
            if (fuelType == "DO") return 21500m;
            return 23000m;
        }

        private Dictionary<VehicleFreeDto, List<ClusterResult>> ApplyFirstFitDecreasingBPP(List<ClusterResult> clusters, List<VehicleFreeDto> vehicles)
        {
            var assignments = new Dictionary<VehicleFreeDto, List<ClusterResult>>();
            var sortedClusters = clusters.OrderByDescending(c => c.TongKhoiLuong).ToList();

            foreach (var cluster in sortedClusters)
            {
                foreach (var xe in vehicles)
                {
                    if (!assignments.ContainsKey(xe)) assignments[xe] = new List<ClusterResult>();

                    var load = assignments[xe].Sum(c => c.TongKhoiLuong);
                    if (load + cluster.TongKhoiLuong <= xe.TaiTrongToiDaKg)
                    {
                        assignments[xe].Add(cluster);
                        break;
                    }
                }
            }
            return assignments.Where(a => a.Value.Any()).ToDictionary(a => a.Key, a => a.Value);
        }

        [HttpGet("phuong-tien-tai-xe-san-sang")]
        public async Task<IActionResult> GetPhuongTienTaiXeSanSang()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TmdtContext>();

            // 1. Xác định mã ca hiện tại dựa trên giờ hệ thống
            int maCaHienTai = xacDinhMaCaTheoGio(DateTime.Now);

            // 2. Lấy danh sách gán ghép (Mapping) đang hoạt động trong ca này
            var mappingSanSang = await context.PhuongTienTaiXes
                .Where(pttx => pttx.IsActive == true && pttx.MaCa == maCaHienTai)
                .ToListAsync();

            if (!mappingSanSang.Any())
            {
                return NotFound(new
                {
                    Message = $"Không có tài xế và xe nào được gán cho ca hiện tại (Mã ca: {maCaHienTai})."
                });
            }

            // 3. (Tùy chọn) Gọi API từ các service khác để check trạng thái thực tế
            // Ví dụ: Check xem xe có đang "Bảo trì" hay Tài xế có đang "Nghỉ phép" không
            var clientPT = _httpClientFactory.CreateClient("PhuongTienApi");
            var clientNS = _httpClientFactory.CreateClient("NhanSuApi");

            var results = new List<object>();

            foreach (var item in mappingSanSang)
            {
                // Kiểm tra trạng thái xe thực tế
                var resXe = await clientPT.GetAsync($"https://localhost:7286/api/quanlyxe/check-status/{item.MaPhuongTien}");
                // Kiểm tra trạng thái tài xế thực tế
                var resTX = await clientNS.GetAsync($"https://localhost:7022/api/quanlytaixe/check-status/{item.MaNguoiDung}");

                if (resXe.IsSuccessStatusCode && resTX.IsSuccessStatusCode)
                {
                    results.Add(new
                    {
                        MaPtTx = item.MaPtTx,
                        MaPhuongTien = item.MaPhuongTien,
                        MaNguoiDung = item.MaNguoiDung,
                        MaCa = item.MaCa,
                        LoaiTuyen = item.LoaiTuyen,
                        ThongTin = "Sẵn sàng hoạt động"
                    });
                }
            }

            return Ok(new
            {
                ThoiGianKiemTra = DateTime.Now,
                MaCaHienTai = maCaHienTai,
                TongSoLuong = results.Count,
                DanhSachSanSang = results
            });
        }

        // 2.1. Phân loại vùng miền dựa trên H3 Index
        private string GetRegionFromH3(string h3Index)
        {
            if (string.IsNullOrEmpty(h3Index)) return "Unknown";

            // 1. Kiểm tra Miền Nam (Dựa trên dữ liệu thực tế đầu 886)
            if (h3Index.StartsWith("886"))
            {
                return "South";
            }

            // 2. Kiểm tra Miền Bắc (Dựa trên dữ liệu thực tế đầu 87 hoặc 882)
            // Các mã như 8714, 8715, 872e, 882d... đều thuộc khu vực phía Bắc trong DB của bạn
            if (h3Index.StartsWith("87") || h3Index.StartsWith("882"))
            {
                return "North";
            }

            // 3. Mặc định hoặc bổ sung Miền Trung (Nếu sau này có dữ liệu đầu 887 chẳng hạn)
            if (h3Index.StartsWith("887"))
            {
                return "Central";
            }

            return "North"; // Mặc định trả về North nếu không khớp (hoặc tùy logic hệ thống)
        }

        // 2.2. Tìm kho phụ gần địa chỉ khách nhất để giao hàng (Last-mile)
        private int TimMaKhoPhuPhuHop(string destinationH3)
        {
            // Query DB tìm Kho có LoaiKho = 'KHO_PHU' và có khoảng cách tới destinationH3 ngắn nhất
            // Ở đây tạm trả về một ID mặc định hoặc logic tìm kiếm
            return 10; // ID của Kho phụ cụ thể
        }

        private int TimMaKhoChinhCuaVung(string region)
        {
            // Chuyển về viết hoa để so sánh chính xác hơn
            return region?.ToUpper() switch
            {
                "NORTH" => 11, // Warehouse Bắc (Hà Nội/Bắc Giang)
                "CENTRAL" => 15, // Warehouse Trung (Đà Nẵng)
                "SOUTH" => 17, // Warehouse Nam (TP.HCM)
                _ => 11  // Mặc định trả về Kho chính Miền Bắc nếu không xác định được
            };
        }
    }
}