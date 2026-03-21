using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuanLyTaiKhoanNguoiDung;
using QuanLyTaiKhoanNguoiDung.ControllersAPI;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12._1234;
using QuanLyTaiKhoanNguoiDung.Models12.HamBam;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNhatKyHeThong;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhanQuyen;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyTaiKhoan;
using QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan;
using QuanLyTaiKhoanNguoiDung.Services;
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
        private readonly IEmailService _emailService;
        private readonly RabbitMQClient _rabbitMQ;
        private readonly PhanQuyenService _phanQuyen;
        private readonly ISystemService _sys;

        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();
        public QuanLyTaiKhoanAPI(TmdtContext context, ILogger<QuanLyTaiKhoanAPI> logger, IEmailService emailService, RabbitMQClient rabbitMQ, PhanQuyenService phanQuyen, ISystemService sys)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
            this._rabbitMQ = rabbitMQ;
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
        [HttpPost("dangnhap")]
        public async Task<IActionResult> DangNhap([FromBody] DangNhapModel login)
        {
            try
            {
                // 1. Kiểm tra đầu vào
                if (login == null || string.IsNullOrEmpty(login.TenDangNhap) || string.IsNullOrEmpty(login.MatKhau))
                {
                    return BadRequest(new { message = "Vui lòng nhập đầy đủ tài khoản và mật khẩu" });
                }

                // 2. Tìm tài khoản và JOIN sang bảng NguoiDung để lấy Email/SĐT
                // Dùng .AsNoTracking() để tăng tốc độ truy vấn vì đây là thao tác Read-only
                // ĐÚNG: Truy vấn email từ bảng liên kết NguoiDung
                var taiKhoan = await _context.TaiKhoans
                    .Include(tk => tk.NguoiDung)
                    .FirstOrDefaultAsync(tk => tk.TenDangNhap == login.TenDangNhap ||
                                              (tk.NguoiDung != null && tk.NguoiDung.Email == login.TenDangNhap));

                // 3. Kiểm tra sự tồn tại
                if (taiKhoan == null)
                {
                    return BadRequest( "Tài khoản hoặc email không tồn tại trong hệ thống" );
                }

                // 4. Kiểm tra mật khẩu
                var inputPasswordHash = SecurityHelper.Encrypt(login.MatKhau);
                if (taiKhoan.MatKhauHash != inputPasswordHash)
                {
                    return BadRequest("Mật khẩu không chính xác, vui lòng thử lại");
                }

                // 5. Kiểm tra trạng thái hoạt động
                if (taiKhoan.HoatDong == false)
                {
                    return BadRequest("Tài khoản của bạn hiện đang bị khóa. Liên hệ quản trị viên.");
                }

                // 6. Lấy thông tin an toàn từ bảng liên kết NguoiDung
                string emailNguoiDung = taiKhoan.NguoiDung?.Email ?? "";
                string hoTen = taiKhoan.NguoiDung?.HoTenNhanVien ?? taiKhoan.TenDangNhap;

                // 7. Thiết lập Claims (Xác thực Cookie)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, hoTen),                
                    new Claim(ClaimTypes.NameIdentifier, taiKhoan.MaNguoiDung.ToString()),
                    new Claim("Username", taiKhoan.TenDangNhap)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(20) }
                );

                // 8. Trả về JSON chuẩn
                return Ok(new
                {
                    message = "Đăng nhập thành công",
                    userId = taiKhoan.MaNguoiDung,
                    userName = taiKhoan.TenDangNhap       
                    
                });
            }
            catch (Exception ex)
            {
                // Nếu DB lỗi (thiếu cột...), chữ "M" sẽ xuất phát từ đây nếu không catch tốt
                return StatusCode(500, new { message ="Lỗi hệ thống khi truy vấn dữ liệu", detail = ex.Message });
            }
        }

        [Authorize] // Thêm Authorize để đảm bảo chỉ người đã đăng nhập mới gọi được
        [HttpPost("dangxuat")]
        public async Task<IActionResult> DangXuat()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("Một người dùng đã đăng xuất");
            return Ok(new { message = "Đăng xuất thành công" });
        }

        
        [HttpPost("doimatkhau/{maNguoiDung}")]
        public async Task<IActionResult> DoiMatKhau(int maNguoiDung, [FromBody] DoiMatKhau model)
        {
            // 1. Kiểm tra đầu vào
            if (model == null || string.IsNullOrWhiteSpace(model.MatKhauCu) || string.IsNullOrWhiteSpace(model.MatKhauMoi))
            {
                return BadRequest(new { message = "Vui lòng cung cấp đầy đủ mật khẩu." });
            }

            // 2. Bảo mật: Kiểm tra ID từ Claim (Cookie)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int currentUserId) || currentUserId != maNguoiDung)
            {
                return Forbid();
            }

            // 3. Tìm tài khoản
            var taikhoan = await _context.TaiKhoans.FindAsync(maNguoiDung);
            if (taikhoan == null)
            {
                return NotFound(new { message = "Tài khoản không tồn tại." });
            }

            // --- PHẦN SỬA ĐỔI QUAN TRỌNG ĐỂ GIỐNG ĐĂNG NHẬP ---
            // 4. Mã hóa mật khẩu cũ người dùng vừa nhập bằng SecurityHelper
            var encodedOldPassword = SecurityHelper.Encrypt(model.MatKhauCu);

            // So sánh với chuỗi đã lưu trong Database
            if (taikhoan.MatKhauHash != encodedOldPassword)
            {
                return BadRequest(new { message = "Mật khẩu cũ không chính xác." });
            }
            // ------------------------------------------------

            // 5. Kiểm tra trùng mật khẩu mới
            if (model.MatKhauCu == model.MatKhauMoi)
            {
                return BadRequest(new { message = "Mật khẩu mới không được giống mật khẩu cũ." });
            }

            // 6. Cập nhật mật khẩu mới (Cũng phải dùng SecurityHelper để đồng bộ)
            taikhoan.MatKhauHash = SecurityHelper.Encrypt(model.MatKhauMoi);

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Đổi mật khẩu thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu mật khẩu mới");
                return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật mật khẩu." });
            }
        }

        // thực hiện cập nhật thông tin cá nhân (không bao gồm mật khẩu) cho người dùng
        [HttpPost("sua-thong-tin/{maNguoiDung}")]
        public async Task<IActionResult> SuaThongTin(int maNguoiDung, [FromBody] SuaThongTinCaNhanUpdate model)
        {
            var existingNguoiDung = await _context.NguoiDungs.FirstOrDefaultAsync(nd => nd.MaNguoiDung == maNguoiDung);
            if (existingNguoiDung == null) return NotFound(new { message = "Không tìm thấy nhân viên để cập nhật." });
            try
            {
                existingNguoiDung.HoTenNhanVien = model.HoTenNhanVien ?? existingNguoiDung.HoTenNhanVien;
                existingNguoiDung.MaDiaChi = model.MaDiaChi ?? existingNguoiDung.MaDiaChi;
                existingNguoiDung.Email = model.Email ?? existingNguoiDung.Email;
                existingNguoiDung.SoDienThoai = model.SoDienThoai ?? existingNguoiDung.SoDienThoai;
                existingNguoiDung.NgaySinh = model.NgaySinh ?? existingNguoiDung.NgaySinh;
                existingNguoiDung.GioiTinh = model.GioiTinh ?? existingNguoiDung.GioiTinh;
                existingNguoiDung.SoCccd = model.SoCccd ?? existingNguoiDung.SoCccd;
                existingNguoiDung.NoiSinh = model.NoiSinh ?? existingNguoiDung.NoiSinh;
                existingNguoiDung.SoTaiKhoan = model.SoTaiKhoan ?? existingNguoiDung.SoTaiKhoan;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Cập nhật thông tin cá nhân thành công" });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật thông tin cá nhân");
                return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật thông tin cá nhân." });
            }
        }
        [HttpPost("quen-mat-khau")]
        public async Task<IActionResult> QuenMatKhau([FromBody] ForgotPasswordModel model)
        {
            if (string.IsNullOrEmpty(model.Email))
            {
                return BadRequest(new { message = "Vui lòng cung cấp địa chỉ Email." });
            }

            // 1. Tìm người dùng dựa trên Email qua bảng NguoiDung
            var taiKhoan = await _context.TaiKhoans
                .Include(tk => tk.NguoiDung)
                .FirstOrDefaultAsync(tk => tk.NguoiDung != null && tk.NguoiDung.Email == model.Email);

            if (taiKhoan == null)
            {
                // Để bảo mật, không nên nói rõ là email không tồn tại, 
                // nhưng ở môi trường nội bộ Logistics thì có thể báo lỗi trực tiếp.
                return NotFound(new { message = "Email này không tồn tại trên hệ thống." });
            }

            // 2. Tạo mật khẩu ngẫu nhiên (8 ký tự)
            string newPassword = Guid.NewGuid().ToString().Substring(0, 8);

            // 3. Cập nhật mật khẩu vào Database (Dùng SecurityHelper giống API Đăng nhập/Đổi MK)
            taiKhoan.MatKhauHash = SecurityHelper.Encrypt(newPassword);

            try
            {
                await _context.SaveChangesAsync();

                // 4. Gửi mail thông báo mật khẩu mới
                string hoTen = taiKhoan.NguoiDung?.HoTenNhanVien ?? "Người dùng";
                await _emailService.SendForgotPasswordEmailAsync(model.Email, hoTen, newPassword);

                return Ok(new { message = "Mật khẩu mới đã được gửi vào Email của bạn." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý quên mật khẩu cho email: {Email}", model.Email);
                return StatusCode(500, new { message = "Lỗi hệ thống khi khôi phục mật khẩu." });
            }
        }

        // API Vô hiệu hóa tài khoản
        [HttpPut("vohieuhoa/{id}")]
        public async Task<IActionResult> VoHieuHoa(int id, [FromBody] KhoaTaiKhoanRequest request)
        {
            if (!await KiemTraQuyenQuanLy())
            {
                return StatusCode(403, new { message = "Bạn không có quyền thực hiện hành động này." });
            }
            // 1. Tối ưu: Lấy tài khoản kèm thông tin người dùng để gửi mail ngay
            var tk = await _context.TaiKhoans
                .Include(t => t.NguoiDung)
                   .ThenInclude(tx=>tx.TaiXe)
                .FirstOrDefaultAsync(t => t.MaNguoiDung == id);

            if (tk == null) return NotFound(new { message = "Không tìm thấy tài khoản" });
            if (tk.HoatDong == false) return BadRequest(new { message = "Tài khoản đã bị khóa trước đó" });

            // 2. Cập nhật trạng thái
            tk.HoatDong = false;
            if (tk.NguoiDung?.TaiXe != null)
            {
                // Cập nhật trạng thái làm việc (bảng TaiXe)
                tk.NguoiDung.TaiXe.TrangThaiHoatDong = "Bị khóa";
            }

            var tenNhanVienBiKhoa = tk.NguoiDung?.HoTenNhanVien ?? tk.TenDangNhap;
            try
            {
                await _context.SaveChangesAsync();

                // 3. Gửi email thông báo (Tối ưu: Không dùng await để API phản hồi ngay lập tức)
                if (tk.NguoiDung != null && !string.IsNullOrEmpty(tk.NguoiDung.Email))
                {
                    _ = Task.Run(async () => {
                        try
                        {
                            await _emailService.SendLockAccountEmailAsync(
                                tk.NguoiDung.Email,
                                tk.NguoiDung.HoTenNhanVien ?? tk.TenDangNhap,
                                request.LyDo,
                                true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Lỗi gửi mail vô hiệu hóa: {ex.Message}");
                        }
                    });
                }

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý nhân viên",
                    "Vô hiệu hóa tài khoản" + tenNhanVienBiKhoa,
                    "TaiKhoan",
                    id.ToString(),
                    // Dữ liệu cũ
                    new Dictionary<string, object> { { "Trạng thái", "Đang hoạt động" } },
                    // Dữ liệu mới
                    new Dictionary<string, object> {
                        { "Trạng thái", "Đã bị khóa" },
                        { "Lý do", request.LyDo }
                    }
                );
                return Ok(new { message = "Vô hiệu hóa tài khoản thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi cập nhật dữ liệu", detail = ex.Message });
            }
        }

        // API Mở khóa tài khoản
        [HttpPut("mokhoa/{id}")]
        public async Task<IActionResult> MoKhoa(int id)
        {
            if (!await KiemTraQuyenQuanLy())
            {
                return StatusCode(403, new { message = "Bạn không có quyền thực hiện hành động này." });
            }
            var tk = await _context.TaiKhoans
                .Include(t => t.NguoiDung)
                .ThenInclude(tx=>tx.TaiXe)
                .FirstOrDefaultAsync(t => t.MaNguoiDung == id);

            if (tk == null) return NotFound(new { message = "Không tìm thấy tài khoản" });

            tk.HoatDong = true;
            if (tk.NguoiDung?.TaiXe != null)
            {
                // Cập nhật trạng thái làm việc (bảng TaiXe)
                tk.NguoiDung.TaiXe.TrangThaiHoatDong = "Sẵn sàng";
            }
            var tenNhanVienBiKhoa = tk.NguoiDung?.HoTenNhanVien ?? tk.TenDangNhap;
            try
            {
                await _context.SaveChangesAsync();

                // Gửi email thông báo mở khóa
                if (tk.NguoiDung != null && !string.IsNullOrEmpty(tk.NguoiDung.Email))
                {
                    _ = Task.Run(async () => {
                        try
                        {
                            await _emailService.SendLockAccountEmailAsync(
                                tk.NguoiDung.Email,
                                tk.NguoiDung.HoTenNhanVien ?? tk.TenDangNhap,
                                "Tài khoản của bạn đã được quản trị viên kích hoạt lại.",
                                false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Lỗi gửi mail mở khóa: {ex.Message}");
                        }
                    });
                }
                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý nhân viên",
                    "Vô hiệu hóa tài khoản" + tenNhanVienBiKhoa,
                    "TaiKhoan",
                    id.ToString(),
                    // Dữ liệu cũ
                    new Dictionary<string, object> { { "Trạng thái", "Đã bị khóa" } },
                    // Dữ liệu mới
                    new Dictionary<string, object> {
                        { "Trạng thái", "Đã hoạt động" }
                        
                    }
                );
                return Ok(new { message = "Mở khóa tài khoản thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi mở khóa", detail = ex.Message });
            }
        }
       
        private async Task<bool> KiemTraQuyenQuanLy()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null) return false;

            var currentUser = await _context.NguoiDungs
                .Include(nd => nd.MaChucVuNavigation)
                .FirstOrDefaultAsync(nd => nd.MaNguoiDung == currentUserId);

            string tenChucVu = currentUser?.MaChucVuNavigation?.TenChucVu ?? "";

            // Trả về true nếu là quản lý, ngược lại false
            return tenChucVu.Contains("Quản lý tổng") || tenChucVu.Contains("Quản lý chi nhánh");
        }
        // Model hỗ trợ nhận dữ liệu
        public class ForgotPasswordModel
        {
            public string? Email { get; set; }
        }
    }
}