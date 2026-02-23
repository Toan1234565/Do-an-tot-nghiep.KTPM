using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging; // Thêm using này
using QuanLyTaiKhoan.Model11._1.QuanLyTaiKhoan;
using QuanLyTaiKhoan.Models;
using System.Security.Claims; // Để dùng ClaimTypes.NameIdentifier


namespace TaiKhoan1.ControllersAPI
{
    [Route("api/quanlynguoidung")]
    [ApiController]
    public class QuanLyNguoiDung : ControllerBase
    {
        private readonly TmdtContext _context;   
        private readonly ILogger<QuanLyNguoiDung> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private IMemoryCache _cache;     
        public QuanLyNguoiDung(TmdtContext context, ILogger<QuanLyNguoiDung> logger, IHttpContextAccessor httpContextAccessor, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _cache = cache;
        }

        // Phương thức trợ giúp: Lấy ID người dùng hiện tại
        private int? GetCurrentUserId()
        {
            // Thường sử dụng ClaimTypes.NameIdentifier cho ID
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);         
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return null;
        }

        [HttpGet("thongtinnguoidung")]
        public async Task<IActionResult> ThongTinNguoiDung()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                // Nếu không có ID người dùng (chưa đăng nhập hoặc token không hợp lệ)
                return Unauthorized(new { message = "Bạn cần đăng nhập để xem thông tin." });
            }

            // 1. Định nghĩa Khóa Cache (phải là duy nhất cho mỗi người dùng)
            string cacheKey = $"UserInfo_{userId.Value}";

            // Biến để lưu trữ thông tin (là DTO/Model bạn đã tạo)
            NguoiDungModel thongtin;

            // 2. Kiểm tra trong Cache
            if (_cache.TryGetValue(cacheKey, out thongtin))
            {
                _logger.LogInformation("Lấy thông tin người dùng ID: {UserId} từ CACHE", userId.Value);
                return Ok(thongtin);
            }

            

            try
            {
                // 3. Truy vấn Database nếu không có trong Cache
                var nguoiDung = await _context.TaiKhoans
                    .Where(tk => tk.MaNguoiDung == userId.Value)
                    .Include(tk => tk.NguoiDung)
                    .ThenInclude(nd => nd.MaChucVuNavigation)
                    .FirstOrDefaultAsync();

                if (nguoiDung == null)
                {
                    _logger.LogWarning("Người dùng không tồn tại với ID: {UserId}", userId.Value);
                    return NotFound(new { message = "Người dùng không tồn tại" });
                }

                // 4. Ánh xạ dữ liệu sang Model và lưu vào Cache
                thongtin = new NguoiDungModel
                {
                    
                    HoTenNhanVien = nguoiDung.NguoiDung?.HoTenNhanVien,
                    DiaChi = nguoiDung.NguoiDung?.DiaChi,
                    Email = nguoiDung.Email,
                    SoDienThoai = nguoiDung.SoDienThoai,
                    NgaySinh = nguoiDung.NguoiDung?.NgaySinh,
                    GioiTinh = nguoiDung.NguoiDung?.GioiTinh,
                    TenChucVu = nguoiDung.NguoiDung?.MaChucVuNavigation?.TenChucVu,
                    SoCccd = nguoiDung.NguoiDung?.SoCccd,
                    NoiSinh = nguoiDung.NguoiDung?.NoiSinh,
                    SoTaiKhoan = nguoiDung.NguoiDung?.SoTaiKhoan,
                    TenNganHang = nguoiDung.NguoiDung?.TenNganHang,
                    BaoHiemXaHoi = nguoiDung.NguoiDung?.BaoHiemXaHoi
                };

                // Thiết lập tùy chọn Cache
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    // Giữ trong cache 5 phút
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    // Nếu không truy cập trong 2 phút thì xóa cache
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));

                // Lưu dữ liệu vào cache
                _cache.Set(cacheKey, thongtin, cacheEntryOptions);

                _logger.LogInformation("Lấy thông tin người dùng ID: {UserId} từ DB và lưu vào Cache", userId.Value);
                return Ok(thongtin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin người dùng ID: {UserId}", userId.Value);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin người dùng" });
            }

        }

      
    }
}