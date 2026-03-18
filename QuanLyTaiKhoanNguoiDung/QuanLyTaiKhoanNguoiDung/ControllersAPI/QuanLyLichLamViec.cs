using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyLichLamViec; // Đảm bảo đúng namespace của DBContext và Entities
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
        private const int PageSize = 20;
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public QuanLyLichLamViecController(TmdtContext context, ILogger<QuanLyLichLamViecController> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId)) ? userId : null;
        }

        [HttpGet("danhsachlichlamviec")]
        public async Task<IActionResult> GetAllLichLamViec([FromQuery] DateOnly thoigian, [FromQuery] int? maKho, [FromQuery] int page = 1)
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
                var cacheKey = $"LichLamViec_{thoigian}_{filterMaKho}_{page}";
                if (!_cache.TryGetValue(cacheKey, out object result))
                {
                    // Bắt đầu Query
                    var query = _context.NguoiDungs.AsNoTracking().AsQueryable();

                    // Lọc theo kho
                    if (filterMaKho.HasValue)
                        query = query.Where(nd => nd.MaKho == filterMaKho);

                    // Lọc theo ngày trực cụ thể (Sử dụng biến thoigian đã được xử lý mặc định ở trên)
                    query = query.Where(nd => nd.DangKyCaTrucs.Any(dk => dk.NgayTruc == thoigian));

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
    }
}