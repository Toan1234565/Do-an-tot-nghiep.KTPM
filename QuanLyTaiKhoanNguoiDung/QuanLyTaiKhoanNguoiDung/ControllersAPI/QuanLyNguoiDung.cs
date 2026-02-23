using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyTaiKhoanNguoiDung;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.HamBam;
using QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan;
using System.Security.Claims;

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
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId)) ? userId : null;
        }
        // 1. api phía web quản lý 
        [HttpGet("danhsachnguoidung")]
        public async Task<IActionResult> DanhSachNguoiDung([FromQuery] string? searchTerm, [FromQuery] int? maChucVu, [FromQuery] int page = 1, [FromQuery] string? donvi = "Tất cả")
        {
            // Tạo key dựa trên tham số query
            string cacheKey = $"ListUser_S:{searchTerm}_C:{maChucVu}_P:{page}_D:{donvi}";

            if (!_cache.TryGetValue(cacheKey, out var cachedData))
            {
                int pageSize = 20;
                var query = _context.NguoiDungs.AsQueryable();

                // Logic lọc dữ liệu...
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(nd => nd.HoTenNhanVien.Contains(searchTerm));
                }
                if (maChucVu.HasValue)
                {
                    query = query.Where(nd => nd.MaChucVu == maChucVu.Value);
                }
                if (!string.IsNullOrEmpty(donvi) && donvi != "Tất cả")
                {
                    query = query.Where(nd => nd.DonViLamViec == donvi);
                }
                int totalRecords = await query.CountAsync();
                var danhsach = await query
                    .Include(nd => nd.MaChucVuNavigation)
                    .OrderBy(nd => nd.HoTenNhanVien)
                    .Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(nd => new NguoiDungModel
                    {
                        MaNguoiDung = nd.MaNguoiDung,
                        HoTenNhanVien = nd.HoTenNhanVien,
                        NgaySinh = nd.NgaySinh,
                        GioiTinh = nd.GioiTinh,
                        TenChucVu = nd.MaChucVuNavigation != null ? nd.MaChucVuNavigation.TenChucVu : null,
                        NoiSinh = nd.NoiSinh,
                        DonViLamViec = nd.DonViLamViec,
                        MaDiaChi = nd.MaDiaChi
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
                    Email = taiKhoan.Email,
                    SoDienThoai = taiKhoan.SoDienThoai,
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {

                var newTaiKhoan = new TaiKhoan
                {
                    TenDangNhap = model.TenDangNhap,
                    MatKhauHash = SecurityHelper.Encrypt(model.MatKhau),
                    Email = model.Email,
                    SoDienThoai = model.SoDienThoai,
                    HoatDong = true
                };

                _context.TaiKhoans.Add(newTaiKhoan);
                await _context.SaveChangesAsync();


                // 3. Tạo thông tin người dùng và GÁN đối tượng TaiKhoan vào biến điều hướng

                var newNguoiDung = new NguoiDung
                {
                    MaNguoiDung = newTaiKhoan.MaNguoiDung,
                    HoTenNhanVien = model.HoTenNhanVien,
                    DonViLamViec = model.DonViLamViec,
                    Email = model.Email,
                    SoDienThoai = model.SoDienThoai,
                    MaChucVu = model.MaChucVu,
                    NgaySinh = model.NgaySinh,
                    GioiTinh = model.GioiTinh,
                    NoiSinh = model.NoiSinh,
                    TenNganHang = model.TenNganHang,
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


        //3. api phía android
        [HttpGet("thongtinnguoidung")]
        public async Task<IActionResult> ThongTinNguoiDung()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Bạn cần đăng nhập." });

            string cacheKey = $"UserInfo_{userId.Value}";
            if (_cache.TryGetValue(cacheKey, out NguoiDungModel thongtin)) return Ok(thongtin);

            try
            {
                var taiKhoan = await _context.TaiKhoans
                    .Where(tk => tk.MaNguoiDung == userId.Value)
                    .Include(tk => tk.NguoiDung)
                    .ThenInclude(nd => nd.MaChucVuNavigation)
                    .FirstOrDefaultAsync();

                if (taiKhoan?.NguoiDung == null)
                    return NotFound(new { message = "Thông tin cá nhân chưa được tạo." });

                var nd = taiKhoan.NguoiDung;
                thongtin = new NguoiDungModel
                {
                    MaNguoiDung = taiKhoan.MaNguoiDung,
                    HoTenNhanVien = nd.HoTenNhanVien,                   
                    Email = taiKhoan.Email,
                    SoDienThoai = taiKhoan.SoDienThoai,
                    NgaySinh = nd.NgaySinh,
                    GioiTinh = nd.GioiTinh,
                    TenChucVu = nd.MaChucVuNavigation?.TenChucVu,
                    NoiSinh = nd.NoiSinh,
                    // Giải mã an toàn: Nếu NULL thì trả về rỗng, tránh lỗi 500
                    SoCccd = !string.IsNullOrEmpty(nd.SoCccd) ? SecurityHelper.Decrypt(nd.SoCccd) : "",
                    BaoHiemXaHoi = !string.IsNullOrEmpty(nd.BaoHiemXaHoi) ? SecurityHelper.Decrypt(nd.BaoHiemXaHoi) : "",
                    SoTaiKhoan = !string.IsNullOrEmpty(nd.SoTaiKhoan) ? SecurityHelper.Decrypt(nd.SoTaiKhoan) : "",
                    TenNganHang = nd.TenNganHang,
                    DonViLamViec = nd.DonViLamViec
                };

                _cache.Set(cacheKey, thongtin, TimeSpan.FromMinutes(10));
                return Ok(thongtin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy thông tin ID: {UserId}", userId);
                return StatusCode(500, new { message = "Lỗi hệ thống khi đọc dữ liệu." });
            }
        }
        [HttpPut("capnhatthongtin")]
        public async Task<IActionResult> CapNhatThongTin([FromBody] NguoiDungModel model)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                var existing = await _context.NguoiDungs.FirstOrDefaultAsync(nd => nd.MaNguoiDung == userId);
                if (existing == null) return NotFound(new { message = "Dữ liệu không tồn tại để cập nhật." });
                existing.HoTenNhanVien = model.HoTenNhanVien;
                existing.DonViLamViec = model.DonViLamViec;
                existing.SoDienThoai = model.SoDienThoai;
                existing.Email = model.Email;
                existing.NgaySinh = model.NgaySinh;
                existing.GioiTinh = model.GioiTinh;
                existing.NoiSinh = model.NoiSinh;
                existing.DonViLamViec = model.DonViLamViec;
                existing.MaChucVu = model.MaChucVu;
                existing.TenNganHang = model.TenNganHang;
                existing.SoCccd = SecurityHelper.Encrypt(model.SoCccd);
                existing.SoTaiKhoan = SecurityHelper.Encrypt(model.SoTaiKhoan);
                existing.BaoHiemXaHoi = SecurityHelper.Encrypt(model.BaoHiemXaHoi);

                _context.NguoiDungs.Update(existing);
                await _context.SaveChangesAsync();

                _cache.Remove($"UserInfo_{userId}");
                return Ok(new { message = "Cập nhật dữ liệu thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi cập nhật ID: {UserId}", userId);
                return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật." });
            }
        }

        //3. api chung
       
    }
}