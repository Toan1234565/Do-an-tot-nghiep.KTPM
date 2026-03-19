using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyTaiKhoanNguoiDung;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.HamBam;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyNhanVien;
using QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan;
using System.Security.Claims;
using static QuanLyTaiKhoanNguoiDung.Models12._1234.EmailService;

namespace TaiKhoan1.ControllersAPI
{
    
    [Route("api/quanlynguoidung")]
    [ApiController]
    public class QuanLyNguoiDung : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<QuanLyNguoiDung> _logger;
        private readonly IMemoryCache _cache;
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public QuanLyNguoiDung(TmdtContext context, ILogger<QuanLyNguoiDung> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
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

        // 1. api phía web quản lý 
        [HttpGet("danhsachnguoidung")]
        [Authorize]
        public async Task<IActionResult> DanhSachNguoiDung([FromQuery] string? searchTerm, [FromQuery] int? maKho, [FromQuery] int? maChucVu, [FromQuery] int page = 1, [FromQuery] bool trangthai = true)
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
            // Tạo key dựa trên tham số query

            string cacheKey = $"ListUser_S:{searchTerm}_K:{filterMaKho}_C:{maChucVu}_P:{page}_TT:{trangthai}";

            if (!_cache.TryGetValue(cacheKey, out var cachedData))
            {
                int pageSize = 20;
                // Lọc tất cả người dùng CÓ mã chức vụ KHÁC 16
                // 1. Khởi tạo query từ bảng TaiKhoan
                var query = _context.TaiKhoans
                    .AsNoTracking() // Tối ưu: Không tracking để tăng tốc độ đọc và cache
                    .Include(tk => tk.NguoiDung)
                        .ThenInclude(nd => nd.MaChucVuNavigation)
                    .Where(tk => tk.NguoiDung != null && tk.NguoiDung.MaChucVu != 16)
                    .AsQueryable();

                // 2. Lọc theo trạng thái hoạt động (Sửa lỗi tt.Ta)
                if (trangthai != null) // Giả sử trangthai là bool?
                {
                    query = query.Where(tk => tk.HoatDong == trangthai);
                }

                // 3. Logic lọc dữ liệu từ bảng NguoiDung thông qua tk.NguoiDung
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    // Tìm theo tên hoặc tên đăng nhập để tối ưu trải nghiệm
                    query = query.Where(tk => tk.NguoiDung.HoTenNhanVien.Contains(searchTerm) || tk.TenDangNhap.Contains(searchTerm));
                }

                if (maChucVu.HasValue)
                {
                    query = query.Where(tk => tk.NguoiDung.MaChucVu == maChucVu.Value);
                }

                if (filterMaKho.HasValue)
                {
                    // Giả sử MaKho nằm trong bảng NguoiDung
                    query = query.Where(tk => tk.NguoiDung.MaKho == filterMaKho.Value);
                }

                // 4. Tính toán tổng số bản ghi (Count nên thực hiện trước khi Select để nhanh hơn)
                int totalRecords = await query.CountAsync();

                // 5. Phân trang và Mapping dữ liệu
                var danhsach = await query
                    .OrderBy(tk => tk.NguoiDung.HoTenNhanVien) // Sắp xếp theo tên người dùng
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(tk => new DanhSachNguoiDungModel
                    {
                        MaNguoiDung = tk.MaNguoiDung,                      
                        HoTenNhanVien = tk.NguoiDung.HoTenNhanVien,
                        TrangThai = tk.HoatDong, // Giả sử TrangThai là bool? trong NguoiDung
                        NgaySinh = tk.NguoiDung.NgaySinh,
                        GioiTinh = tk.NguoiDung.GioiTinh,
                        TenChucVu = tk.NguoiDung.MaChucVuNavigation != null ? tk.NguoiDung.MaChucVuNavigation.TenChucVu : null,
                        NoiSinh = tk.NguoiDung.NoiSinh,
                        DonViLamViec = tk.NguoiDung.DonViLamViec,
                        MaDiaChi = tk.NguoiDung.MaDiaChi
                    })
                    .ToListAsync();

                cachedData = new { TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize), CurrentPage = page, Data = danhsach };

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token)); // Thêm dòng này

                _cache.Set(cacheKey, cachedData, cacheOptions);
            }
            return Ok(cachedData);
        }
        [HttpGet("chitietnhanvien/{maNhanVien}")]
        public async Task<IActionResult> ThongTinChiTiet(int maNhanVien)
        {
            string cacheKey = $"UserDetail_{maNhanVien}";
            if (!_cache.TryGetValue(cacheKey, out var thongtin))
            {
                var taiKhoan = await _context.TaiKhoans
                    .Where(tk => tk.MaNguoiDung == maNhanVien)
                    .Include(tk => tk.NguoiDung).ThenInclude(nd => nd.MaChucVuNavigation)
                    .FirstOrDefaultAsync();

                if (taiKhoan?.NguoiDung == null) return NotFound(new { message = "Không tìm thấy." });

                var nd = taiKhoan.NguoiDung;
                thongtin = new NguoiDungModel
                {
                    MaNguoiDung = taiKhoan.MaNguoiDung,
                    HoTenNhanVien = nd.HoTenNhanVien,
                    Email = nd.Email,
                    SoDienThoai = nd.SoDienThoai,
                    TenChucVu = nd.MaChucVuNavigation?.TenChucVu,
                    SoCccd = !string.IsNullOrEmpty(nd.SoCccd) ? SecurityHelper.Decrypt(nd.SoCccd) : "",
                    BaoHiemXaHoi = !string.IsNullOrEmpty(nd.BaoHiemXaHoi) ? SecurityHelper.Decrypt(nd.BaoHiemXaHoi) : "",
                    SoTaiKhoan = !string.IsNullOrEmpty(nd.SoTaiKhoan) ? SecurityHelper.Decrypt(nd.SoTaiKhoan) : "",
                    TenNganHang = nd.TenNganHang,
                    DonViLamViec = nd.DonViLamViec,
                    NgaySinh = nd.NgaySinh,
                    GioiTinh = nd.GioiTinh,
                    NoiSinh = nd.NoiSinh,
                    MaDiaChi = nd.MaDiaChi
                };

                _cache.Set(cacheKey, thongtin, TimeSpan.FromMinutes(10)); // Cache 10 phút
            }
            return Ok(thongtin);
        }
        [HttpPost("themnhanvien")]
        public async Task<IActionResult> ThemNhanVien([FromBody] NguoiDungModel model)
        {
            // 1. Kiểm tra tên đăng nhập đã tồn tại chưa
            if (_context.TaiKhoans.Any(tk => tk.TenDangNhap == model.TenDangNhap))
                return Conflict(new { message = "Tên đăng nhập đã tồn tại" });
            if(_context.NguoiDungs.Any(tk => tk.Email == model.Email))
                return Conflict(new { message = "Email đã tồn tại" });
            if(_context.NguoiDungs.Any(tk => tk.SoDienThoai == model.SoDienThoai))
                return Conflict(new { message = "Số điện thoại đã tồn tại" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            if (transaction == null) return StatusCode(500, new { message = "Không thể bắt đầu giao dịch cơ sở dữ liệu." });
            if(model.TenDangNhap == null) return BadRequest(new { message = "Tên đăng nhập không được để trống." });
            if(model.MatKhau == null) return BadRequest(new { message = "Mật khẩu không được để trống." });
            if(model.HoTenNhanVien == null) return BadRequest(new { message = "Họ tên nhân viên không được để trống." });
            try
            {

                var newTaiKhoan = new TaiKhoan
                {
                    TenDangNhap = model.TenDangNhap,
                    MatKhauHash = SecurityHelper.Encrypt(model.MatKhau),                   
                    HoatDong = true
                };

                _context.TaiKhoans.Add(newTaiKhoan);
                await _context.SaveChangesAsync();


                // 3. Tạo thông tin người dùng và GÁN đối tượng TaiKhoan vào biến điều hướng

                var newNguoiDung = new NguoiDung
                {
                    MaNguoiDung = newTaiKhoan.MaNguoiDung,
                    HoTenNhanVien = model.HoTenNhanVien,                  
                    Email = model.Email,
                    SoDienThoai = model.SoDienThoai,
                    MaChucVu = model.MaChucVu,
                    NgaySinh = model.NgaySinh,
                    GioiTinh = model.GioiTinh,
                    NoiSinh = model.NoiSinh,
                    TenNganHang = model.TenNganHang,
                    MaKho = model.MaKho,
                    DonViLamViec = model.DonViLamViec,
                    SoCccd = SecurityHelper.Encrypt(model.SoCccd),
                    SoTaiKhoan = SecurityHelper.Encrypt(model.SoTaiKhoan),
                    BaoHiemXaHoi = SecurityHelper.Encrypt(model.BaoHiemXaHoi)
                };


                _context.NguoiDungs.Add(newNguoiDung);

                // Lưu một lần duy nhất cho cả 2 bảng
                await _context.SaveChangesAsync();

                // Hoàn tất lưu dữ liệu
                await transaction.CommitAsync();

                // 4. Xóa cache danh sách để frontend tải lại dữ liệu mới nhất
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new { message = "Thêm nhân viên và tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi thêm nhân viên");

                // Trả về InnerException để dễ debug nếu còn lỗi
                var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "Lỗi hệ thống: " + errorMessage });
            }
        }
        //[HttpPost("themnhanvien")]
        //public async Task<IActionResult> ThemNhanVien([FromBody] NguoiDungModel model, [FromServices] IEmailService emailService)
        //{
        //    if (_context.TaiKhoans.Any(tk => tk.TenDangNhap == model.TenDangNhap))
        //        return Conflict(new { message = "Tên đăng nhập đã tồn tại" });

        //    // TẠO MẬT KHẨU RANDOM 8 CHỮ SỐ
        //    string randomPassword = new Random().Next(10000000, 99999999).ToString();

        //    using var transaction = await _context.Database.BeginTransactionAsync();
        //    try
        //    {
        //        var newTaiKhoan = new TaiKhoan
        //        {
        //            TenDangNhap = model.TenDangNhap,
        //            // Mã hóa mật khẩu ngẫu nhiên để lưu vào DB
        //            MatKhauHash = SecurityHelper.Encrypt(randomPassword),
        //            Email = model.Email,
        //            SoDienThoai = model.SoDienThoai,
        //            HoatDong = true
        //        };

        //        _context.TaiKhoans.Add(newTaiKhoan);
        //        await _context.SaveChangesAsync();

        //        var newNguoiDung = new NguoiDung
        //        {
        //            MaNguoiDung = newTaiKhoan.MaNguoiDung,
        //            HoTenNhanVien = model.HoTenNhanVien,
        //            Email = model.Email,
        //            SoDienThoai = model.SoDienThoai,
        //            MaChucVu = model.MaChucVu,
        //            NgaySinh = model.NgaySinh,
        //            GioiTinh = model.GioiTinh,
        //            NoiSinh = model.NoiSinh,
        //            TenNganHang = model.TenNganHang,
        //            MaKho = model.MaKho,
        //            DonViLamViec = model.DonViLamViec,
        //            SoCccd = SecurityHelper.Encrypt(model.SoCccd),
        //            SoTaiKhoan = SecurityHelper.Encrypt(model.SoTaiKhoan),
        //            BaoHiemXaHoi = SecurityHelper.Encrypt(model.BaoHiemXaHoi)
        //        };

        //        _context.NguoiDungs.Add(newNguoiDung);
        //        await _context.SaveChangesAsync();
        //        await transaction.CommitAsync();

        //        // GỬI EMAIL CHỨA MẬT KHẨU GỐC (Chưa Encrypt)
        //        try
        //        {
        //            await emailService.SendAccountInfoAsync(model.Email, model.HoTenNhanVien, model.TenDangNhap, randomPassword);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError($"Lỗi gửi mail: {ex.Message}");
        //            // Không rollback transaction vì data đã lưu xong, chỉ là lỗi thông báo
        //        }

        //        _resetCacheSignal.Cancel();
        //        _resetCacheSignal = new CancellationTokenSource();

        //        return Ok(new { message = "Thêm thành công! Mật khẩu đã được gửi tới email nhân viên." });
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
        //        return StatusCode(500, new { message = "Lỗi hệ thống: " + errorMessage });
        //    }
        //}
        [HttpPut("capnhatnhanvien/{maNhanVien}")]
        public async Task<IActionResult> CapNhatNhanVien(int maNhanVien, [FromBody] NguoiDungUpdateModel model)
        {
            var existingNguoiDung = await _context.NguoiDungs.FirstOrDefaultAsync(nd => nd.MaNguoiDung == maNhanVien);
            if (existingNguoiDung == null) return NotFound(new { message = "Không tìm thấy nhân viên để cập nhật." });
            try
            {               
                existingNguoiDung.DonViLamViec = model.DonViLamViec;    
                existingNguoiDung.MaChucVu = model.MaChucVu;
                existingNguoiDung.HoTenNhanVien = model.HoTenNhanVien;
                existingNguoiDung.NgaySinh = model.NgaySinh;
                existingNguoiDung.BaoHiemXaHoi = SecurityHelper.Encrypt(model.BaoHiemXaHoi);
                
                _context.NguoiDungs.Update(existingNguoiDung);
                await _context.SaveChangesAsync();
                // Xóa cache liên quan
                _cache.Remove($"UserDetail_{maNhanVien}");
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();
                return Ok(new { message = "Cập nhật nhân viên thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi cập nhật nhân viên ID: {MaNhanVien}", maNhanVien);
                return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật." });
            }
        }
        [HttpGet("danhsachchucvu")]
        public async Task<IActionResult> DanhSachChucVu()
        {
            string cacheKey = "List_ChucVu";
            if (!_cache.TryGetValue(cacheKey, out var danhsach))
            {
                danhsach = await _context.ChucVus.Include(cacheKey => cacheKey.MaVaiTroNavigation)
                    .Select(cv => new ChucVuModel
                    {
                        TenChucVu = cv.TenChucVu,
                        MaChucVu = cv.MaChucVu,
                        TenVaiTro = cv.MaVaiTroNavigation != null ? cv.MaVaiTroNavigation.TenVaiTro : null
                    })
                    .ToListAsync();

                if (danhsach == null || ((dynamic)danhsach).Count == 0) return NotFound();

                // Lưu vào cache 30 phút
                var cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30));
                _cache.Set(cacheKey, danhsach, cacheOptions);
            }
            return Ok(danhsach);
        }
        [HttpPost("themchucvu")]
        public async Task<IActionResult> ThemChucVu([FromBody] ChucVuModel model)
        {
            if (_context.ChucVus.Any(cv => cv.TenChucVu == model.TenChucVu))
            {
                return Conflict(new { message = "Chức vụ này đã tồn tại." });
            }
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var newChucVu = new ChucVu
                {
                    TenChucVu = model.TenChucVu,
                    MaVaiTro = model.MaVaiTro,

                };
                _context.ChucVus.Add(newChucVu);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _cache.Remove("List_ChucVu");
                return Ok(new { message = "Thêm chức vụ thành công." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
            }

        }
        [HttpGet("danhsachvaitro")]
        public async Task<IActionResult> DanhSachVaiTro()
        {
            try
            {
                var vaitro = await _context.VaiTros
               .Select(vt => new { vt.MaVaiTro, vt.TenVaiTro })
               .ToListAsync();
                return Ok(vaitro);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy danh sách vai trò");
                return StatusCode(500, new { message = "Lỗi hệ thống khi lấy danh sách vai trò." });

            }
        }
        // API: Lấy thông tin chi tiết của một chức vụ theo ID       
        [HttpGet("chucvu/{id}")]
        public async Task<IActionResult> GetChucVuChiTiet(int id)
        {
            try
            {
                // Tìm chức vụ trong cơ sở dữ liệu bao gồm cả thông tin Vai trò (nếu cần)
                var chucVu = await _context.ChucVus
                    .Select(cv => new
                    {
                        cv.MaChucVu,
                        cv.TenChucVu,
                        cv.MaVaiTro
                    })
                    .FirstOrDefaultAsync(cv => cv.MaChucVu == id);

                if (chucVu == null)
                {
                    return NotFound(new { message = "Không tìm thấy chức vụ yêu cầu." });
                }

                return Ok(chucVu);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy chi tiết chức vụ ID: {ChucVuId}", id);
                return StatusCode(500, new { message = "Lỗi hệ thống khi lấy chi tiết chức vụ." });

            }
        }
        // API SỬA CHỨC VỤ
        [HttpPut("suachucvu/{id}")]
        public async Task<IActionResult> SuaChucVu(int id, [FromBody] ChucVuModel model)
        {
            var chucVu = await _context.ChucVus.FindAsync(id);
            if (chucVu == null) return NotFound(new { message = "Không tìm thấy chức vụ này." });

            // Kiểm tra nếu tên chức vụ mới đã tồn tại ở bản ghi khác
            if (_context.ChucVus.Any(cv => cv.TenChucVu == model.TenChucVu && cv.MaChucVu != id))
            {
                return Conflict(new { message = "Tên chức vụ này đã tồn tại." });
            }

            try
            {
                chucVu.TenChucVu = model.TenChucVu;
                chucVu.MaVaiTro = model.MaVaiTro;

                _context.ChucVus.Update(chucVu);
                await _context.SaveChangesAsync();
                _cache.Remove("List_ChucVu");
                return Ok(new { message = "Cập nhật chức vụ thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
            }
        }
        // API XÓA CHỨC VỤ
        [HttpDelete("xoachucvu/{id}")]
        public async Task<IActionResult> XoaChucVu(int id)
        {
            var chucVu = await _context.ChucVus.FindAsync(id);
            if (chucVu == null) return NotFound(new { message = "Không tìm thấy chức vụ để xóa." });

            // KIỂM TRA RÀNG BUỘC: Nếu có nhân viên đang giữ chức vụ này thì không cho xóa
            bool dangDuocSuDung = await _context.NguoiDungs.AnyAsync(n => n.MaChucVu == id);
            if (dangDuocSuDung)
            {
                return BadRequest(new { message = "Không thể xóa vì đang có nhân viên thuộc chức vụ này." });
            }

            try
            {
                _context.ChucVus.Remove(chucVu);
                await _context.SaveChangesAsync();
                _cache.Remove("List_ChucVu");
                return Ok(new { message = "Xóa chức vụ thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
            }
        }
        // API thực hiện tải danh sách nhân viên thuộc kho để thêm mới kho bãi(Nhân viên phụ trách quản lý kho mới này)
        [HttpGet("danhsachnhanvienkho")]
        public async Task<IActionResult> DanhSachNguoiDungKho()
        {
            string cacheKey = "ListUser_KhoDepartment";

            if (!_cache.TryGetValue(cacheKey, out var danhsach))
            {
                danhsach = await _context.NguoiDungs
                    .Where(nd => nd.MaChucVuNavigation != null &&
                                 nd.MaChucVuNavigation.MaVaiTroNavigation != null &&
                                 (nd.MaChucVuNavigation.MaVaiTroNavigation.TenVaiTro.Contains("Nhân viên kho") ||
                                  nd.MaChucVuNavigation.MaVaiTroNavigation.TenVaiTro.Contains("Quản lý chi nhánh")))
                    .OrderBy(nd => nd.HoTenNhanVien)
                    .Select(nd => new {
                        nd.MaNguoiDung,
                        // Trả về chuỗi rỗng nếu HoTenNhanVien null để tránh lỗi UI
                        HoTenNhanVien = nd.HoTenNhanVien ?? "Không rõ tên",
                        TenChucVu = nd.MaChucVuNavigation.TenChucVu ?? "Chưa xác định",
                        TenVaiTro = nd.MaChucVuNavigation.MaVaiTroNavigation.TenVaiTro
                    })
                    .ToListAsync();

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

                _cache.Set(cacheKey, danhsach, cacheOptions);
            }

            return Ok(danhsach);
        }


        
       
    }
}