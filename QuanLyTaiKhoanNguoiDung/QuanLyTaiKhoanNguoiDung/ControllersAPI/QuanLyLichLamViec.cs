using MailKit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyLichLamViec; // Đảm bảo đúng namespace của DBContext và Entities
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNhatKyHeThong;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhanQuyen;
using QuanLyTaiKhoanNguoiDung.Services;
using System.Net.Http;
using System.Security;
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
        private readonly PhanQuyenService _phanQuyen;
        private readonly ISystemService _sys;
        public QuanLyLichLamViecController(TmdtContext context, ILogger<QuanLyLichLamViecController> logger, IMemoryCache cache, IHttpClientFactory httpClientFactory, RabbitMQClient rabbitMQ, PhanQuyenService phanQuyen, ISystemService sys)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _aiService = new AISchedulingService();
            _httpClientFactory = httpClientFactory;
            _rabbitMQ = rabbitMQ;
            _phanQuyen = phanQuyen;
            _sys = sys;
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
                // 1. Sử dụng Service để kiểm tra quyền và thông tin người dùng
                var permission = await _phanQuyen.GetUserPermissionAsync(GetCurrentUserId());

                if (permission == null)
                    return Unauthorized(new { message = "Vui lòng đăng nhập." });

                if (!permission.IsQuanLyTong && !permission.IsQuanLyKho)
                    return StatusCode(403, new { message = "Bạn không có quyền truy cập danh sách này." });

                // 2. Xác định mã kho cần lọc (Admin dùng maKho từ client, Quản lý kho dùng MaKho của chính mình)
                int? filterMaKho = permission.GetFinalMaKho(maKho);

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
        
        [HttpGet("thongke-dangky")]
        public async Task<IActionResult> GetThongKe([FromQuery] int? month, [FromQuery] int? year, [FromQuery] int? maKho)
        {
            try
            {
                // 1.Lấy thông tin phân quyền từ Class Service đã tách
               var permission = await _phanQuyen.GetUserPermissionAsync(GetCurrentUserId());

                if (permission == null)
                    return Unauthorized(new { message = "Vui lòng đăng nhập." });

                if (!permission.IsQuanLyTong && !permission.IsQuanLyKho)
                    return StatusCode(403, new { message = "Bạn không có quyền truy cập." });

                // 2. Lấy mã kho dựa trên quyền (Logic đã nằm trong class UserPermission)
                int? finalMaKho = permission.GetFinalMaKho(maKho);

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
                    isAdmin = permission.IsQuanLyTong,
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
                // 1. Sử dụng Service để kiểm tra quyền và thông tin người dùng
                var permission = await _phanQuyen.GetUserPermissionAsync(GetCurrentUserId());

                if (permission == null)
                    return Unauthorized(new { message = "Vui lòng đăng nhập." });

                if (!permission.IsQuanLyTong && !permission.IsQuanLyKho)
                    return StatusCode(403, new { message = "Bạn không có quyền truy cập danh sách này." });

                // 2. Xác định mã kho cần lọc (Admin dùng maKho từ client, Quản lý kho dùng MaKho của chính mình)
                int? filterMaKho = permission.GetFinalMaKho(maKho);

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
            // Kiểm tra tính hợp lệ của Request
            if (request == null || request.NgayCanDuyet == default)
                return BadRequest("Dữ liệu đầu vào không hợp lệ.");
           
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUser = await _context.NguoiDungs
                    .Include(nd => nd.MaChucVuNavigation)
                    .FirstOrDefaultAsync(nd => nd.MaNguoiDung == currentUserId);
                // 1. Sử dụng Service để kiểm tra quyền và thông tin người dùng
                var permission = await _phanQuyen.GetUserPermissionAsync(GetCurrentUserId());

                if (permission == null)
                    return Unauthorized(new { message = "Vui lòng đăng nhập." });

                if (!permission.IsQuanLyTong && !permission.IsQuanLyKho)
                    return StatusCode(403, new { message = "Bạn không có quyền truy cập danh sách này." });

                // 2. Xác định mã kho cần lọc (Admin dùng maKho từ client, Quản lý kho dùng MaKho của chính mình)
                int? filterMaKho = permission.GetFinalMaKho(request.MaKho);               
                DateOnly start = request.NgayCanDuyet;
                DateOnly end = request.NgayCanDuyet.AddDays(9);
                DateOnly historyStart = start.AddDays(-7);

                // 2. Truy vấn Database 1 lần duy nhất
                // Bao gồm cả dữ liệu cũ (để tính điểm AI) và dữ liệu mới (đang chờ duyệt)
                var allData = await _context.DangKyCaTrucs
                    .Include(dk => dk.MaCaNavigation)
                    .Include(dk => dk.MaNguoiDungNavigation)
                    .Where(dk => dk.NgayTruc >= historyStart && dk.NgayTruc <= end)
                    .Where(dk => !filterMaKho.HasValue || dk.MaNguoiDungNavigation.MaKho == filterMaKho)
                    .ToListAsync();

                var allApprovedIds = new List<int>();

                // 3. Vòng lặp xử lý logic AI cho 10 ngày
                for (int i = 0; i < 10; i++)
                {
                    DateOnly currentTargetDate = start.AddDays(i);

                    // Lấy danh sách đang chờ duyệt của ngày hiện tại
                    var danhSachCho = allData
                        .Where(dk => dk.NgayTruc == currentTargetDate && dk.TrangThai == "Chờ duyệt")
                        .ToList();

                    if (!danhSachCho.Any()) continue;

                    // Lấy lịch sử 7 ngày trước đó của ngày currentTargetDate (đã duyệt hoặc đã trực)
                    var lichSu = allData
                        .Where(dk => dk.NgayTruc < currentTargetDate && dk.NgayTruc >= currentTargetDate.AddDays(-7))
                        .ToList();

                    // Nhóm theo ca để duyệt theo định mức của từng ca
                    var groupedByCa = danhSachCho.GroupBy(x => x.MaCa);
                    foreach (var group in groupedByCa)
                    {
                        // Giả sử mỗi ca lấy tối đa 3 người dựa trên điểm AI
                        var topCandidates = group.Select(dk => {
                            var userHistory = lichSu.Where(l => l.MaNguoiDung == dk.MaNguoiDung).ToList();
                            return _aiService.AnalyzeShift(dk, userHistory);
                        })
                        .Where(r => r != null)
                        .OrderByDescending(r => r.AI_Score)
                        .Take(3) // Có thể thay bằng biến cấu hình định mức
                        .Select(r => r.MaDangKy);

                        allApprovedIds.AddRange(topCandidates);
                    }
                }

                // 4. Cập nhật trạng thái
                var recordsToUpdate = allData.Where(x => allApprovedIds.Contains(x.MaDangKy)).ToList();
                foreach (var r in recordsToUpdate)
                {
                    r.TrangThai = "Đã duyệt";                    
                }

                if (recordsToUpdate.Any())
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                }
                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý lịch làm việc",
                    "Duyệt lịch tự động bằng AI",
                    "DangKyCaTruc",
                    "0", // Chuyển thành chuỗi nếu tham số là string
                         // 1. Dữ liệu cũ
                    new Dictionary<string, object> {
                        { "Trạng thái trước", "Chờ duyệt" },
                        { "Tổng số bản ghi xử lý", recordsToUpdate.Count }
                    }, // <-- Thêm dấu đóng ngoặc nhọn ở đây
                       // 2. Dữ liệu mới
                    new Dictionary<string, object> {
                        { "Trạng thái sau", "Đã duyệt" },
                        { "Danh sách ID được duyệt", string.Join(", ", allApprovedIds) },
                        { "Phạm vi", $"10 ngày kể từ {start:dd/MM/yyyy}" }
                    }
                ); // <-- Thêm dấu đóng ngoặc đơn ở đây
                return Ok(new
                {
                    success = true,
                    message = $"Đã duyệt thành công {recordsToUpdate.Count} bản ghi bằng AI.",
                    totalApproved = recordsToUpdate.Count
                });
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                // Log lỗi ở đây (ví dụ: _logger.LogError(e, "Lỗi khi duyệt AI"))
                return StatusCode(500, $"Lỗi hệ thống: {e.Message}");
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
                var tenNhanVien = dangKy.MaNguoiDungNavigation.HoTenNhanVien;                
                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý lịch làm việc",
                    "Cập nhật trạng thái lịch của nhân viên" + tenNhanVien,
                    "DangKyCaTruc",
                    maDangKy.ToString(),
                    // Dữ liệu cũ
                    new Dictionary<string, object> { { "Trạng thái", trangThaiCu } },
                    // Dữ liệu mới
                    new Dictionary<string, object> {
                        { "Trạng thái", request.TrangThai }
                       
                    }
                );
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