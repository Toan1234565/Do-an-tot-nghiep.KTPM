using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyNhatKyHeThong.Models12;
using System.Security.Claims;

namespace QuanLyNhatKyHeThong.ControllersAPI
{
    [Route("api/nhatkyhethong")]
    [ApiController]
    public class QuanLyNhatKyHeThongController : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<QuanLyNhatKyHeThongController> _logger;
        private readonly IMemoryCache _cache;

        // CancellationTokenSource nên được quản lý tập trung hoặc qua một Service
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public QuanLyNhatKyHeThongController(TmdtContext context, ILogger<QuanLyNhatKyHeThongController> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }
        private (int? UserId, string Role, string MaKho, string TenChucVu) GetUserAuthInfo()
        {
            // 1. Lấy thông tin từ Claims (Đã được Middleware JWT giải mã từ Token gửi lên)
            // Lưu ý: ClaimTypes.NameIdentifier thường tương ứng với "sub" hoặc "id" trong JWT
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Nếu bạn dùng Role mặc định của ASP.NET Core
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
                            ?? User.FindFirst("role")?.Value; // Dự phòng nếu token dùng key thường

            // Các Claim tùy chỉnh (Custom Claims)
            var maKhoClaim = User.FindFirst("MaKho")?.Value;
            var chucVuClaim = User.FindFirst("TenChucVu")?.Value;

            // 2. Chuyển đổi UserId sang kiểu int an toàn
            int? userId = null;
            if (int.TryParse(userIdClaim, out int parsedId))
            {
                userId = parsedId;
            }

            // 3. Trả về kết quả (Sử dụng giá trị mặc định để tránh lỗi null ở Frontend)
            return (
                UserId: userId,
                Role: roleClaim ?? "Guest",
                MaKho: maKhoClaim ?? "",
                TenChucVu: chucVuClaim ?? "N/A"
            );
        }

        [HttpGet("check-auth")]
        public IActionResult CheckAuth()
        {
            // Endpoint này cực kỳ hữu ích để debug xem Token gửi sang có gì
            var claims = User.Claims.Select(c => new {
                Type = c.Type,
                Value = c.Value
            }).ToList();

            return Ok(new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated,
                AuthenticationType = User.Identity?.AuthenticationType,
                UserName = User.Identity?.Name,
                DataFromFunction = GetUserAuthInfo(),
                AllClaimsInToken = claims
            });
        }
       
        [HttpGet("getall-nhatky")]
        public async Task<IActionResult> GetAllNhatKyHeThong([FromQuery] int page = 1, [FromQuery] string tendichvu = "", [FromQuery] int pageSize = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize > 100) pageSize = 100;

                var cacheKey = $"nhatky_p{page}_s{pageSize}_ten{tendichvu}";

                if (_cache.TryGetValue(cacheKey, out object? cachedResult))
                {
                    return Ok(cachedResult);
                }

                // Tạo query cơ bản
                var query = _context.NhatKyHeThongs.AsNoTracking().AsQueryable();

                // Lọc theo tên dịch vụ nếu có
                if (!string.IsNullOrWhiteSpace(tendichvu))
                {
                    // Sử dụng ToLower để tìm kiếm không phân biệt hoa thường
                    query = query.Where(nk => nk.TenDichVu.ToLower().Contains(tendichvu.ToLower()));
                }

                // Tính toán tổng số trang
                var totalRecords = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

                // Lấy dữ liệu phân trang
                var nhatKyList = await query
                    .OrderByDescending(nk => nk.ThoiGianThucHien)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(nk => new NhatKyHeThongModels
                    {
                        MaNhatKy = nk.MaNhatKy,
                        TenDichVu = nk.TenDichVu,
                        LoaiThaoTac = nk.LoaiThaoTac,
                        NguoiThucHien = nk.NguoiThucHien,
                        ThoiGianThucHien = nk.ThoiGianThucHien,
                        TrangThaiThaoTac = nk.TrangThaiThaoTac,
                        DuLieuCu = nk.DuLieuCu,
                        DuLieuMoi = nk.DuLieuMoi,
                        DiaChiIp = nk.DiaChiIp
                    })
                    .ToListAsync();

                // Đóng gói kết quả trả về
                var result = new
                {
                    data = nhatKyList,
                    totalPages = totalPages,
                    currentPage = page,
                    totalRecords = totalRecords
                };

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(2))
                    .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                _cache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi API lấy nhật ký");
                return StatusCode(500, "Lỗi hệ thống nội bộ.");
            }
        }

        // Endpoint để xóa cache khi có dữ liệu mới quan trọng
        [HttpPost("clear-cache")]
        public IActionResult ClearCache()
        {
            _resetCacheSignal.Cancel();
            _resetCacheSignal.Dispose();
            _resetCacheSignal = new CancellationTokenSource();
            return Ok(new { message = "Đã làm mới Cache thành công" });
        }
    }
}