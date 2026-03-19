using MailKit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyLichLamViec; // Đảm bảo đúng namespace của DBContext và Entities
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNhatKyHeThong;
using QuanLyTaiKhoanNguoiDung.Services;
using System.Net.Http;
using System.Security.Claims;

namespace QuanLyTaiKhoanNguoiDung.ControllersAPI
{
    [Route("api/quanlylichlamviec")]
    [ApiController]
    public class QuanLyLichLamViecController : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<QuanLyLichLamViecController> _logger;
        private readonly IMemoryCache _cache;
        private readonly AISchedulingService _aiService; 
        private const int PageSize = 20;
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly RabbitMQClient _rabbitMQ;

        public QuanLyLichLamViecController(TmdtContext context, ILogger<QuanLyLichLamViecController> logger, IMemoryCache cache, IHttpClientFactory httpClientFactory, RabbitMQClient rabbitMQ)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _aiService = new AISchedulingService();
            _httpClientFactory = httpClientFactory;
            _rabbitMQ = rabbitMQ;
        }

        private int? GetCurrentUserId()
        {
            // 1. Thử lấy từ Claims (Cookie Authentication)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }

            // 2. Dự phòng: Thử lấy từ Session (Nếu Cookie bị lỗi nhưng Session còn)
            var sessionUserId = HttpContext.Session.GetString("MaNguoiDung");
            if (!string.IsNullOrEmpty(sessionUserId) && int.TryParse(sessionUserId, out int sUserId))
            {
                return sUserId;
            }

            return null;
        }

        [HttpGet("danhsachlichlamviec")]
        public async Task<IActionResult> GetAllLichLamViec([FromQuery] DateOnly thoigian, [FromQuery] int? maKho = 11, [FromQuery] string? trangthai ="Đã duyệt" , [FromQuery] int page = 1)
        {
            try
            {
                // 1. Lấy và kiểm tra ID người dùng hiện tại
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized(new { message = "Vui lòng đăng nhập." });

                // Lấy thông tin người dùng đang thực hiện request để check Vai trò và Kho
                var currentUser = await _context.NguoiDungs
                    .Include(nd => nd.MaChucVuNavigation)
                    .ThenInclude(cv => cv.MaVaiTroNavigation)
                    .FirstOrDefaultAsync(nd => nd.MaNguoiDung == currentUserId);

                if (currentUser == null)
                    return Unauthorized(new { message = "Người dùng không tồn tại." });

                string tenVaiTro = currentUser.MaChucVuNavigation?.TenChucVu ?? "";

                // 2. Xác định quyền (Bạn có thể điều chỉnh chuỗi này cho khớp chính xác với DB của bạn)
                bool isQuanLyTong = tenVaiTro.Contains("Quản lý tổng") || tenVaiTro.Contains("Admin");
                bool isQuanLyKho = tenVaiTro.Contains("Quản lý chi nhánh") || tenVaiTro.Contains("Quản lý kho");

                // Nếu không có cả 2 quyền trên thì từ chối truy cập (Forbidden)
                if (!isQuanLyTong && !isQuanLyKho)
                {
                    return StatusCode(403, new { message = "Bạn không có quyền truy cập danh sách này." });
                }

                // 3. Xử lý logic lọc mã Kho
                int? filterMaKho = maKho; // Mặc định dùng tham số từ client (dành cho quản lý tổng)

                if (isQuanLyKho && !isQuanLyTong)
                {
                    // Nếu chỉ là quản lý kho, BẮT BUỘC ép mã kho thành mã kho của người đang đăng nhập
                    // Ngăn chặn việc họ đổi tham số maKho trên URL để xem kho khác
                    filterMaKho = currentUser.MaKho;
                }

                // 4. Cache xử lý
                var cacheKey = $"LichLamViec_{thoigian}_{filterMaKho}_{page}_{trangthai}";
                if (!_cache.TryGetValue(cacheKey, out object result))
                {
                    // Bắt đầu Query
                    var query = _context.NguoiDungs.AsNoTracking().AsQueryable();

                    // Lọc theo kho
                    if (filterMaKho.HasValue)
                        query = query.Where(nd => nd.MaKho == filterMaKho);

                    // Lọc theo ngày trực cụ thể (Sử dụng biến thoigian đã được xử lý mặc định ở trên)
                    query = query.Where(nd => nd.DangKyCaTrucs.Any(dk => dk.NgayTruc == thoigian));

                    if(trangthai?.GetHashCode() != 0) // Nếu có tham số trạng thái, thì mới lọc theo trạng thái
                    {
                        query = query.Where(nd => nd.DangKyCaTrucs.Any(dk => dk.NgayTruc == thoigian && dk.TrangThai == trangthai));
                    }
                    int totalItems = await query.CountAsync();
                    int totalPages = (int)Math.Ceiling(totalItems / (double)PageSize);

                    var data = await query
                        .OrderBy(nd => nd.MaNguoiDung)
                        .Skip((page - 1) * PageSize)
                        .Take(PageSize)
                        .Select(nd => new DangKyCaTrucModels
                        {
                            MaNguoiDung = nd.MaNguoiDung,
                            HoTenNhanVien = nd.HoTenNhanVien,
                            MaDangKy = nd.DangKyCaTrucs
                                        .Where(dk => dk.NgayTruc == thoigian)                                    
                                        .Select(dk => dk.MaDangKy)
                                        .FirstOrDefault(),
                            NgayTruc = thoigian, // Trả về chính ngày đang truy vấn
                            TrangThai = nd.DangKyCaTrucs
                                        .Where(dk => dk.NgayTruc == thoigian)
                                        .Select(dk => dk.TrangThai)
                                        .FirstOrDefault(),
                            MaCaNavigation = nd.DangKyCaTrucs
                                        .Where(dk => dk.NgayTruc == thoigian)
                                        .Select(dk => new CaLamViecModels
                                        {
                                            MaCa = dk.MaCaNavigation.MaCa,
                                            TenCa = dk.MaCaNavigation.TenCa,
                                            GioBatDau = dk.MaCaNavigation.GioBatDau,
                                            GioKetThuc = dk.MaCaNavigation.GioKetThuc
                                        })
                                        .FirstOrDefault()
                        })
                        .ToListAsync();

                    result = new { totalItems, totalPages, currentPage = page, data, queryDate = thoigian };

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, result, cacheOptions);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách lịch làm việc");
                return StatusCode(500, new { message = "Lỗi hệ thống nội bộ." });
            }
        
        }
        [HttpGet("check-auth")]
        public IActionResult CheckAuth()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            return Ok(new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated,
                UserName = User.Identity?.Name,
                UserIdFromFunction = GetCurrentUserId(),
                AllClaims = claims
            });
        }
        [HttpGet("thongke-dangky")]
        public async Task<IActionResult> GetThongKe([FromQuery] int? month, [FromQuery] int? year, [FromQuery] int? maKho)
        {
            try
            {
                // 1. Lấy và kiểm tra ID người dùng hiện tại
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized(new { message = "Vui lòng đăng nhập." });

                // Lấy thông tin người dùng đang thực hiện request để check Vai trò và Kho
                var currentUser = await _context.NguoiDungs
                    .Include(nd => nd.MaChucVuNavigation)
                    .ThenInclude(cv => cv.MaVaiTroNavigation)
                    .FirstOrDefaultAsync(nd => nd.MaNguoiDung == currentUserId);

                if (currentUser == null)
                    return Unauthorized(new { message = "Người dùng không tồn tại." });

                string tenVaiTro = currentUser.MaChucVuNavigation?.TenChucVu ?? "";

                // 2. Xác định quyền (Bạn có thể điều chỉnh chuỗi này cho khớp chính xác với DB của bạn)
                bool isQuanLyTong = tenVaiTro.Contains("Quản lý tổng") || tenVaiTro.Contains("Admin");
                bool isQuanLyKho = tenVaiTro.Contains("Quản lý chi nhánh") || tenVaiTro.Contains("Quản lý kho");

                // --- PHÂN QUYỀN VÀ THIẾT LẬP MẶC ĐỊNH MÃ KHO ---
                int? finalMaKho;
                if (isQuanLyTong)
                {
                    // Nếu là Admin: Ưu tiên mã kho truyền vào, nếu không truyền (null) thì mặc định lấy kho 11
                    finalMaKho = maKho ?? 11;
                }
                else
                {
                    // Nếu là Quản lý kho: Luôn lấy mã kho của chính họ
                    finalMaKho = currentUser.MaKho;
                }

                // --- LOGIC THỐNG KÊ ---
                int targetMonth = month ?? DateTime.Now.Month;
                int targetYear = year ?? DateTime.Now.Year;

                // Xử lý ngày tháng an toàn hơn với DateOnly
                var startDate = new DateOnly(targetYear, targetMonth, 1);
                var endDate = startDate.AddMonths(1);

                var query = _context.DangKyCaTrucs
                    .Include(dk => dk.MaNguoiDungNavigation) // Include để lọc theo MaKho của nhân viên
                    .Include(dk => dk.MaCaNavigation)
                    .AsNoTracking()
                    .AsQueryable();

                // Lọc theo thời gian
                query = query.Where(dk => dk.NgayTruc >= startDate && dk.NgayTruc < endDate);

                // Lọc theo mã kho (finalMaKho lúc này chắc chắn có giá trị nếu là Admin hoặc Quản lý kho)
                if (finalMaKho.HasValue)
                {
                    query = query.Where(dk => dk.MaNguoiDungNavigation.MaKho == finalMaKho);
                }

                var statistics = await query
                    .GroupBy(dk => new { dk.NgayTruc, dk.MaCaNavigation.TenCa })
                    .Select(g => new {
                        Ngay = g.Key.NgayTruc.Day,
                        TenCa = g.Key.TenCa ?? "Không xác định",
                        SoLuong = g.Count()
                    })
                    .OrderBy(x => x.Ngay)
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    isAdmin = isQuanLyTong,
                    currentMaKho = finalMaKho,
                    targetTime = $"{targetMonth}/{targetYear}",
                    data = statistics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi API Thống kê");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tải biểu đồ." });
            }
        }
        [HttpGet("danhsach-choduyet-ai")]
        public async Task<IActionResult> GetLichChoDuyetVoiAI([FromQuery] DateOnly ngayCanXem, [FromQuery] int? maKho)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null) return Unauthorized(new { message = "Vui lòng đăng nhập." });

                var currentUser = await _context.NguoiDungs
                    .Include(nd => nd.MaChucVuNavigation)
                    .FirstOrDefaultAsync(nd => nd.MaNguoiDung == currentUserId);

                if (currentUser == null) return Unauthorized(new { message = "Người dùng không tồn tại." });

                // Phân quyền
                string tenVaiTro = currentUser.MaChucVuNavigation?.TenChucVu ?? "";
                bool isQuanLyTong = tenVaiTro.Contains("Quản lý tổng") || tenVaiTro.Contains("Admin");
                int? filterMaKho = isQuanLyTong ? maKho : currentUser.MaKho;

                // 3. Xác định khoảng thời gian 15 ngày
                DateOnly ngayKetThuc = ngayCanXem.AddDays(14); // 15 ngày bao gồm cả ngày bắt đầu
                const int SO_LUONG_DINH_MUC = 3;

                // 4. Lấy danh sách chờ duyệt trong 15 ngày
                var query = _context.DangKyCaTrucs
                    .Include(dk => dk.MaCaNavigation)
                    .Include(dk => dk.MaNguoiDungNavigation)
                    .Where(dk => dk.NgayTruc >= ngayCanXem && dk.NgayTruc <= ngayKetThuc && dk.TrangThai == "Chờ duyệt");

                if (filterMaKho.HasValue)
                    query = query.Where(dk => dk.MaNguoiDungNavigation.MaKho == filterMaKho);

                var danhSachCho = await query.ToListAsync();

                // 5. Lấy lịch sử (7 ngày trước ngày bắt đầu xem)
                var listIds = danhSachCho.Select(x => x.MaNguoiDung).Distinct().ToList();
                var lichSu = await _context.DangKyCaTrucs
                    .Where(dk => listIds.Contains(dk.MaNguoiDung) &&
                                 dk.NgayTruc >= ngayCanXem.AddDays(-7) &&
                                 dk.NgayTruc <= ngayKetThuc) // Lấy đến ngày kết thúc để AI tính toán độ mệt mỏi cộng dồn
                    .ToListAsync();

                // 6. AI Chấm điểm (Sắp xếp theo ngày tăng dần, sau đó điểm giảm dần)
                var results = danhSachCho.Select(dk => {
                    // Chỉ lấy lịch sử TRƯỚC ngày trực cụ thể của bản ghi đó để AI đánh giá
                    var lichSuTruocDo = lichSu.Where(ls => ls.MaNguoiDung == dk.MaNguoiDung && ls.NgayTruc < dk.NgayTruc).ToList();
                    return _aiService.AnalyzeShift(dk, lichSuTruocDo);
                })
                .OrderBy(r => r.NgayTruc) // Hiện ngày gần nhất lên trước
                .ThenByDescending(r => r.AI_Score) // Trong cùng ngày, ai điểm cao xếp trên
                .ToList();

                return Ok(new
                {
                    success = true,
                    tuNgay = ngayCanXem,
                    denNgay = ngayKetThuc,
                    data = results,
                    count = results.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi AI Scheduling 15 ngày");
                return StatusCode(500, "Lỗi máy chủ.");
            }
        }
        [HttpPost("duyet-tat-ca-ai")]
        public async Task<IActionResult> ApproveAllByAI([FromBody] ApproveAIRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUser = await _context.NguoiDungs
                    .Include(nd => nd.MaChucVuNavigation)
                    .FirstOrDefaultAsync(nd => nd.MaNguoiDung == currentUserId);

                if (currentUser == null) return Unauthorized();

                string tenVaiTro = currentUser.MaChucVuNavigation?.TenChucVu ?? "";
                bool isQuanLyTong = tenVaiTro.Contains("Quản lý tổng") || tenVaiTro.Contains("Admin");
                int? filterMaKho = isQuanLyTong ? request.MaKho : currentUser.MaKho;

                const int SO_LUONG_DINH_MUC = 3;
                var allApprovedIds = new List<int>();
                int daysProcessed = 0;

                // Vòng lặp duyệt cho 10 ngày tiếp theo
                for (int i = 0; i < 10; i++)
                {
                    DateOnly currentTargetDate = request.NgayCanDuyet.AddDays(i);

                    // 1. Lấy danh sách chờ duyệt của ngày hiện tại trong vòng lặp
                    var query = _context.DangKyCaTrucs
                        .Include(dk => dk.MaCaNavigation)
                        .Include(dk => dk.MaNguoiDungNavigation)
                        .Where(dk => dk.NgayTruc == currentTargetDate && dk.TrangThai == "Chờ duyệt");

                    if (filterMaKho.HasValue)
                        query = query.Where(dk => dk.MaNguoiDungNavigation.MaKho == filterMaKho);

                    var danhSachCho = await query.ToListAsync();

                    // Nếu ngày này không có dữ liệu, bỏ qua và sang ngày tiếp theo (theo yêu cầu của Toán)
                    if (!danhSachCho.Any()) continue;

                    // 2. Lấy lịch sử 7 ngày TRƯỚC ngày đang xét để AI tính toán
                    var listIds = danhSachCho.Select(x => x.MaNguoiDung).Distinct().ToList();
                    var lichSu = await _context.DangKyCaTrucs
                        .AsNoTracking()
                        .Where(dk => listIds.Contains(dk.MaNguoiDung) &&
                                     dk.NgayTruc >= currentTargetDate.AddDays(-7) &&
                                     dk.NgayTruc < currentTargetDate)
                        .ToListAsync();

                    // 3. Phân nhóm theo ca và để AI chọn
                    var groupedByCa = danhSachCho.GroupBy(x => x.MaCa);
                    foreach (var group in groupedByCa)
                    {
                        var topCandidates = group.Select(dk => {
                            var ls = lichSu.Where(l => l.MaNguoiDung == dk.MaNguoiDung).ToList();
                            return _aiService.AnalyzeShift(dk, ls);
                        })
                        .OrderByDescending(r => r.AI_Score)
                        .Take(SO_LUONG_DINH_MUC)
                        .Select(r => r.MaDangKy)
                        .ToList();

                        allApprovedIds.AddRange(topCandidates);
                    }
                    daysProcessed++;
                }

                if (!allApprovedIds.Any())
                    return Ok(new { success = false, message = "Không tìm thấy dữ liệu chờ duyệt trong 10 ngày tới." });

                // 4. Thực hiện cập nhật trạng thái hàng loạt
                var recordsToApprove = await _context.DangKyCaTrucs
                    .Where(x => allApprovedIds.Contains(x.MaDangKy))
                    .ToListAsync();

                foreach (var record in recordsToApprove)
                {
                    record.TrangThai = "Đã duyệt";
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 5. Reset Cache
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new
                {
                    success = true,
                    message = $"AI đã quét 10 ngày và duyệt thành công {recordsToApprove.Count} đơn tại {daysProcessed} ngày có dữ liệu.",
                    totalApproved = recordsToApprove.Count,
                    totalDaysWithData = daysProcessed
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi duyệt chuỗi 10 ngày bằng AI");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xử lý duyệt hàng loạt." });
            }
        }

        [HttpPost("cap-nhat-trangthai-ai/{maDangKy}")]
        public async Task<IActionResult> CapNhatTrangThai(int maDangKy, [FromBody] DangKyCaLamViecUpdate request)
        {
            try
            {
                // 1. Kiểm tra đăng nhập
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null) return Unauthorized(new { message = "Vui lòng đăng nhập." });

                var currentUser = await _context.NguoiDungs
                    .Include(nd => nd.MaChucVuNavigation)
                    .FirstOrDefaultAsync(nd => nd.MaNguoiDung == currentUserId);

                if (currentUser == null) return Unauthorized(new { message = "Người dùng không tồn tại." });

                // 2. Kiểm tra bản ghi đăng ký
                var dangKy = await _context.DangKyCaTrucs
                    .Include(dk => dk.MaNguoiDungNavigation)
                    .FirstOrDefaultAsync(dk => dk.MaDangKy == maDangKy);

                if (dangKy == null) return NotFound(new { message = "Không tìm thấy yêu cầu đăng ký này." });

                // --- LOGIC MỚI: Kiểm tra ngày thực hiện ---
                // Chỉ cho phép sửa khi ngày cần sửa (NgayTruc) > ngày thực hiện (Hôm nay)
                var today = DateOnly.FromDateTime(DateTime.Now);
                if (dangKy.NgayTruc <= today)
                {
                    return BadRequest(new { message = "Lịch trực đã hoặc đang diễn ra, không thể thay đổi trạng thái." });
                }

                // 3. Kiểm tra phân quyền
                string tenVaiTro = currentUser.MaChucVuNavigation?.TenChucVu ?? "";
                bool isQuanLyTong = tenVaiTro.Contains("Quản lý tổng") || tenVaiTro.Contains("Admin");
                bool isQuanLyKho = tenVaiTro.Contains("Quản lý chi nhánh") || tenVaiTro.Contains("Quản lý kho");

                if (!isQuanLyTong && !isQuanLyKho)
                    return StatusCode(403, new { message = "Bạn không có quyền thực hiện chức năng này." });

                // Nếu là quản lý kho, chỉ được duyệt nhân viên thuộc kho của mình
                if (!isQuanLyTong && dangKy.MaNguoiDungNavigation.MaKho != currentUser.MaKho)
                {
                    return StatusCode(403, new { message = "Bạn không có quyền duyệt lịch của nhân viên thuộc kho khác." });
                }
                var trangThaiCu = dangKy.TrangThai;
                // 4. Cập nhật trạng thái
                // Sử dụng luôn đối tượng 'dangKy' đã Include ở trên để tối ưu performance
                dangKy.TrangThai = request.TrangThai;

                await _context.SaveChangesAsync();

                var log = new LogMessage
                {
                    TenDichVu = "Quản lý lịch làm việc",
                    LoaiThaoTac = "Cập nhật trạng thái lịch làm việc",
                    TenBangLienQuan = "DangKyCaTruc",
                    MaDoiTuong = maDangKy.ToString(),
                    DuLieuCu = new { TrangThai = trangThaiCu },
                    DuLieuMoi = new { TrangThai = request.TrangThai },
                    NguoiThucHien = currentUser.HoTenNhanVien, // Tên người thực hiện (Admin/Quản lý)
                    DiaChiIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    TrangThaiThaoTac = true
                };

                // Bắn thông tin sang RabbitMQ (Không dùng await nếu không muốn bắt người dùng chờ log xong)
                // Nhưng tốt nhất nên dùng await để đảm bảo tính ổn định
                await _rabbitMQ.SendLogAsync(log);
                // 5. Xử lý Cache Signal (nếu có dùng)
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new
                {
                    success = true,
                    message = $"Đã cập nhật trạng thái sang '{request.TrangThai}' thành công.",
                    maDangKy = maDangKy
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi cập nhật trạng thái mã {maDangKy}");
                return StatusCode(500, new { message = "Lỗi hệ thống nội bộ." });
            }
        }
        // Request Model
        public class ApproveAIRequest
        {
            public DateOnly NgayCanDuyet { get; set; }
            public int? MaKho { get; set; }
        }
    }
}