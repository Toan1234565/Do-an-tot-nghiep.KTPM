using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims; // Thư viện băm mật khẩu
using System.Security.Cryptography; // Để dùng cho việc sinh chuỗi ngẫu nhiên an toàn hơn
using System.Text;
using QuanLyTaiKhoan.Model11._1.QuanLyTaiKhoan;
using QuanLyTaiKhoan.Models;
using QuanLyTaiKhoan.Model11Model11._1.QuanLyTaiKhoan;


namespace TaiKhoan1.ControllersAPI
{
    [Route("api/quanlytaikhoan")]
    [ApiController]
    public class QuanLyTaiKhoanAPI : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private IMemoryCache _cache;
        private readonly ILogger<QuanLyTaiKhoanAPI> _logger;

        // Constructor giữ nguyên
        public QuanLyTaiKhoanAPI(TmdtContext context, IHttpContextAccessor httpContextAccessor, IMemoryCache cache, ILogger<QuanLyTaiKhoanAPI> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _cache = cache;
            _logger = logger;
        }

        [HttpPost("taotaikhoan")]
        public async Task<IActionResult> TaoTaiKhoan([FromBody] TaiKhoanCreate taiKhoanCreate)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Trạng thái mô hình không hợp lệ để tạo tài khoản");
                return BadRequest(ModelState);
            }
            try
            {
                // 1. Kiểm tra Tên đăng nhập và Email trùng lặp (Giữ nguyên)
                if (_context.TaiKhoans.Any(tk => tk.TenDangNhap == taiKhoanCreate.TenDangNhap))
                {
                    _logger.LogWarning("Tên đăng nhập đã tồn tại: {Username}", taiKhoanCreate.TenDangNhap);
                    return Conflict(new { message = "Tên đăng nhập đã tồn tại" });
                }
                if (_context.TaiKhoans.Any(tk => tk.Email == taiKhoanCreate.Email))
                {
                    _logger.LogWarning("Email đã được sử dụng: {Email}", taiKhoanCreate.Email);
                    return Conflict(new { message = "Email đã được sử dụng" });
                }
                      
                // 3. Băm mật khẩu sử dụng BCrypt
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(taiKhoanCreate.MatKhauHash);

                // 4. Tạo và thêm Tài khoản mới
                var newTaiKhoan = new TaiKhoan
                {
                  
                    TenDangNhap = taiKhoanCreate.TenDangNhap,
                    MatKhauHash = hashedPassword, // Sử dụng mật khẩu đã băm
                    Email = taiKhoanCreate.Email,
                    SoDienThoai = taiKhoanCreate.SoDienThoai,
                    HoatDong = taiKhoanCreate.HoatDong
                };

                _context.TaiKhoans.Add(newTaiKhoan);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new account for user ID: {UserId}");

                // Trả về Mã người dùng đã sinh ra để Client biết
                return Ok(new
                {
                    message = "Tạo tài khoản thành công",
               
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account for user: {Username}", taiKhoanCreate.TenDangNhap);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo tài khoản" });
            }
        }
        [HttpPost("dangnhap")]
        public async Task<IActionResult> DangNhap([FromBody] DangNhapModel loginModel)
        {
            if(!ModelState.IsValid)
            {
                _logger.LogWarning("Trạng thái mô hình không hợp lệ để đăng nhập");
                return BadRequest(ModelState);
            }
            try
            {
                var taiKhoan = await _context.TaiKhoans
                    .FirstOrDefaultAsync(tk => tk.TenDangNhap == loginModel.TenDangNhap);
                if(taiKhoan == null)
                {
                    _logger.LogWarning("Tên đăng nhập không tồn tại: {Username}", loginModel.TenDangNhap);
                    return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng" });

                }
                // xacs thu   mat khau
                bool isPass = BCrypt.Net.BCrypt.Verify(loginModel.MatKhau, taiKhoan.MatKhauHash);
                if(!isPass)
                {
                    _logger.LogWarning("Mật khẩu không đúng cho tên đăng nhập: {Username}", loginModel.TenDangNhap);
                    return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng" });
                }   
                // kiem tra trang thai 

                if(taiKhoan.HoatDong == false)
                {
                    _logger.LogWarning("Tài khoản bị vô hiệu hóa: {Username}", loginModel.TenDangNhap);
                    return Unauthorized(new { message = "Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên." });
                }
                // Dang nhap thanh cong
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, taiKhoan.TenDangNhap),
                    new Claim(ClaimTypes.NameIdentifier, taiKhoan.MaNguoiDung.ToString() ?? string.Empty)
                };
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true, // Giữ đăng nhập qua các phiên
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) // Hết hạn sau 30 phút
                };
                // Ghi Cookie xác thực vào HttpContext
                await _httpContextAccessor.HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation("User logged in successfully: {Username}", taiKhoan.TenDangNhap);

                return Ok(new
                {
                    message = "Đăng nhập thành công",
                    username = taiKhoan.TenDangNhap,
                    userId = taiKhoan.MaNguoiDung
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user: {Username}", loginModel.TenDangNhap);
                return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống khi đăng nhập" });
            }
        }
        // Đặt Action này cùng cấp với TaoTaiKhoan trong QuanLyTaiKhoanAPI
        [HttpPost("dangxuat")]
        public async Task<IActionResult> DangXuat()
        {
            try
            {
                if (_httpContextAccessor.HttpContext != null)
                {
                    // Xóa Cookie/Session xác thực
                    await _httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    _logger.LogInformation("User logged out successfully.");

                    return Ok(new { message = "Đăng xuất thành công" });
                }
                return BadRequest(new { message = "Không thể truy cập HttpContext để đăng xuất" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout.");
                return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống khi đăng xuất" });
            }
        }
    }
}