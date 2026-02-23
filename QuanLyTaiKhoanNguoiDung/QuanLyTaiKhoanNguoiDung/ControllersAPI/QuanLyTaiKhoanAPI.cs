using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuanLyTaiKhoanNguoiDung;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyTaiKhoan;
using QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan;
using System.Security.Claims; // Thư viện băm mật khẩu
using System.Security.Cryptography; // Để dùng cho việc sinh chuỗi ngẫu nhiên an toàn hơn
using System.Text;


namespace TaiKhoan1.ControllersAPI
{
    [Route("api/quanlytaikhoan")]
    [ApiController]
    public class QuanLyTaiKhoanAPI : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<QuanLyTaiKhoanAPI> _logger;

        public QuanLyTaiKhoanAPI(TmdtContext context, ILogger<QuanLyTaiKhoanAPI> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("taotaikhoan")]
        public async Task<IActionResult> TaoTaiKhoan([FromBody] TaiKhoanCreate model)
        {
            if (_context.TaiKhoans.Any(tk => tk.TenDangNhap == model.TenDangNhap))
                return Conflict(new { message = "Tên đăng nhập đã tồn tại" });

            var newTaiKhoan = new TaiKhoan
            {
                TenDangNhap = model.TenDangNhap,
                MatKhauHash = BCrypt.Net.BCrypt.HashPassword(model.MatKhauHash),
                Email = model.Email,
                SoDienThoai = model.SoDienThoai,
                HoatDong = true
            };

            _context.TaiKhoans.Add(newTaiKhoan);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tài khoản {User} đã được tạo với ID {Id}", newTaiKhoan.TenDangNhap, newTaiKhoan.MaNguoiDung);
            return Ok(new { message = "Tạo tài khoản thành công", userId = newTaiKhoan.MaNguoiDung });
        }

        [HttpPost("dangnhap")]
        public async Task<IActionResult> DangNhap([FromBody] DangNhapModel login)
        {
            var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(tk => tk.TenDangNhap == login.TenDangNhap);

            if (taiKhoan == null || !BCrypt.Net.BCrypt.Verify(login.MatKhau, taiKhoan.MatKhauHash))
                return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu" });

            if (taiKhoan.HoatDong == false)
                return Unauthorized(new { message = "Tài khoản bị khóa" });

            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, taiKhoan.TenDangNhap),
            new Claim(ClaimTypes.NameIdentifier, taiKhoan.MaNguoiDung.ToString())
        };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2) }
            );

            return Ok(new { message = "Đăng nhập thành công", userId = taiKhoan.MaNguoiDung });
        }

        [HttpPost("dangxuat")]
        public async Task<IActionResult> DangXuat()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { message = "Đã đăng xuất" });
        }
        [Authorize]
        [HttpPost("doimatkhau")]
        public async Task<IActionResult> DoiMatKhau([FromBody] DoiMatKhau model)
        {
           var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if(userIdClaim == null )
            {
                return Unauthorized( new { message = "Không tìm thấy thông tin người dùng." });
            }
            int userId = int.Parse(userIdClaim.Value);
             
            // tim tai khoan trong database
            var taikhoan = await _context.TaiKhoans.FindAsync(userId);
            if(taikhoan == null)
            {
                return NotFound( new { message = "Tài khoản không tồn tại." });
            }
            bool isMatKhauCuDung = BCrypt.Net.BCrypt.Verify(model.MatKhauCu, taikhoan.MatKhauHash);
            if(!isMatKhauCuDung)
            {
                return BadRequest( new { message = "Mật khẩu cũ không đúng." });
            }
            // kiemr tra mat khau moi khong duoc trung voi mat khau cu
            if(model.MatKhauCu == model.MatKhauMoi)
            {
                return BadRequest( new { message = "Mật khẩu mới không được trùng với mật khẩu cũ." });
            }
            taikhoan.MatKhauHash = BCrypt.Net.BCrypt.HashPassword( model.MatKhauMoi);
            _context.TaiKhoans.Update(taikhoan);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Người dùng {UserId} đã đổi mật khẩu thành công", userId);

            return Ok(new { message = "Đổi mật khẩu thành công" });
        }
    }
}