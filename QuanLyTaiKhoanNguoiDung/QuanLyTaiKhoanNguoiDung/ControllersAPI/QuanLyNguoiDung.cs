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
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNhatKyHeThong;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhanQuyen;
using QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan;
using System.Security.Claims;
using Tmdt.Shared.Services;
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
        private readonly PhanQuyenService _phanQuyen;
        private readonly ISystemService _sys;
        public QuanLyNguoiDung(TmdtContext context, ILogger<QuanLyNguoiDung> logger, IMemoryCache cache, PhanQuyenService phanQuyen, ISystemService sys)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
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

        // 1. api phía web quản lý 
        [HttpGet("danhsachnguoidung")]
        [Authorize]
        public async Task<IActionResult> DanhSachNguoiDung([FromQuery] string? searchTerm, [FromQuery] int? maKho, [FromQuery] int? maChucVu, [FromQuery] int page = 1, [FromQuery] bool trangthai = true)
        {
            // 1. Sử dụng Service để kiểm tra quyền và thông tin người dùng
            var permission = await _phanQuyen.GetUserPermissionAsync(GetCurrentUserId());

            if (permission == null)
                return Unauthorized(new { message = "Vui lòng đăng nhập." });

            if (!permission.IsQuanLyTong && !permission.IsQuanLyKho)
                return StatusCode(403, new { message = "Bạn không có quyền truy cập danh sách này." });

            // 2. Xác định mã kho cần lọc (Admin dùng maKho từ client, Quản lý kho dùng MaKho của chính mình)
            int? filterMaKho = permission.GetFinalMaKho(maKho);


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
        // thực hiện sửa thong tin nhân viên một số thông tin cơ bản như tên, ngày sinh, chức vụ, đơn vị làm việc, bảo hiểm xã hội
        [HttpPut("capnhatnhanvien/{maNhanVien}")]
        public async Task<IActionResult> CapNhatNhanVien(int maNhanVien, [FromBody] NguoiDungUpdateModel model)
        {
            var existingNguoiDung = await _context.NguoiDungs.FirstOrDefaultAsync(nd => nd.MaNguoiDung == maNhanVien);
            if (existingNguoiDung == null) return NotFound(new { message = "Không tìm thấy nhân viên để cập nhật." });
            try
            {
                // 2. Lưu lại dữ liệu cũ TRƯỚC khi gán giá trị mới để ghi Log
                var dataCu = new Dictionary<string, object> {
                    
                    { "Họ tên", existingNguoiDung.HoTenNhanVien },
                    { "Đơn vị", existingNguoiDung.DonViLamViec },
                    { "Mã chức vụ", existingNguoiDung.MaChucVu }
                };
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

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý nhân viên",
                    "Cập nhật thông tin nhân viên" + model.HoTenNhanVien,
                    "NguoiDung",
                    "0", 
                         // 1. Dữ liệu cũ
                    dataCu,
                    // 2. Dữ liệu mới
                    new Dictionary<string, object> {
                        { "Họ tên", model.HoTenNhanVien },
                        { "Đơn vị", model.DonViLamViec },
                        { "Mã chức vụ", model.MaChucVu }
                    }
                ); // <-- Thêm dấu đóng ngoặc đơn ở đây

                return Ok(new { message = "Cập nhật nhân viên thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi cập nhật nhân viên ID: {MaNhanVien}", maNhanVien);
                return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật." });
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