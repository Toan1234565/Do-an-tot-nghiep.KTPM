using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyTaiKhoanNguoiDung;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12._1234;
using QuanLyTaiKhoanNguoiDung.Models12.HamBam;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNhatKyHeThong;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyNhanVien;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyTaiXe;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyPhanQuyen;
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
        private readonly IEmailService _emailService;

        public QuanLyNguoiDung(TmdtContext context, ILogger<QuanLyNguoiDung> logger, IMemoryCache cache, PhanQuyenService phanQuyen, ISystemService sys, IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _phanQuyen = phanQuyen;
            _emailService = emailService;
            _sys = sys;
        }

        // 1. api phía web quản lý 
        [HttpGet("danhsachnguoidung")]
        [Authorize]
        public async Task<IActionResult> DanhSachNguoiDung([FromQuery] string? searchTerm, [FromQuery] int? maKho, [FromQuery] int? maChucVu, [FromQuery] int page = 1, [FromQuery] bool trangthai = true)
        {         
            string cacheKey = $"ListUser_S:{searchTerm}_K:{maKho}_C:{maChucVu}_P:{page}_TT:{trangthai}";

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

                if (maKho.HasValue)
                {
                    // Giả sử MaKho nằm trong bảng NguoiDung
                    query = query.Where(tk => tk.NguoiDung.MaKho == maKho.Value);
                }

                // 4. Tính toán tổng số bản ghi (Count nên thực hiện trước khi Select để nhanh hơn)
                int totalRecords = await query.CountAsync();

                // 5. Phân trang và Mapping dữ liệu
                var danhsach = await query
                    .OrderBy(tk => tk.NguoiDung.HoTenNhanVien) // Sắp xếp theo tên người dùng
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(tk => new NguoiDungListModel
                    {
                        MaNguoiDung = tk.MaNguoiDung,                      
                        HoTenNhanVien = tk.NguoiDung.HoTenNhanVien,
                        TrangThai = tk.HoatDong, // Giả sử TrangThai là bool? trong NguoiDung
                       
                        GioiTinh = tk.NguoiDung.GioiTinh,
                        TenChucVu = tk.NguoiDung.MaChucVuNavigation != null ? tk.NguoiDung.MaChucVuNavigation.TenChucVu : null,
                        
                        DonViLamViec = tk.NguoiDung.DonViLamViec,
                        MaDiaChi = tk.NguoiDung.MaDiaChi,
                        MaChucVu = tk.NguoiDung.MaChucVu,
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
        public async Task<IActionResult> GetChiTietNhanSu(int maNhanVien)
        {
            try
            {
                // 1. Kiểm tra Cache
                var cacheKey = $"UserFullDetail_{maNhanVien}";
                if (_cache.TryGetValue(cacheKey, out NguoiDungDetailModel cachedData))
                {
                    return Ok(cachedData);
                }

                // 2. Truy vấn Database (Sử dụng Eager Loading để lấy toàn bộ quan hệ trong 1 query)
                var taiKhoan = await _context.TaiKhoans
                    .Include(tk => tk.NguoiDung)
                        .ThenInclude(nd => nd.MaChucVuNavigation)
                    .Include(tk => tk.NguoiDung)
                        .ThenInclude(nd => nd.TaiXe)
                    .FirstOrDefaultAsync(tk => tk.MaNguoiDung == maNhanVien);

                if (taiKhoan?.NguoiDung == null)
                {
                    return NotFound(new { message = "Không tìm thấy thông tin nhân sự." });
                }

                var nd = taiKhoan.NguoiDung;

                // 3. Mapping dữ liệu chung và Giải mã (Security)
                var result = new NguoiDungDetailModel
                {
                    MaNguoiDung = taiKhoan.MaNguoiDung,
                    TenDangNhap = taiKhoan.TenDangNhap,
                    HoTenNhanVien = nd.HoTenNhanVien,
                    Email = nd.Email,
                    SoDienThoai = nd.SoDienThoai,
                    TenNganHang = nd.TenNganHang,
                    DonViLamViec = nd.DonViLamViec,
                    TenChucVu = nd.MaChucVuNavigation?.TenChucVu,
                    NgaySinh = nd.NgaySinh,
                    GioiTinh = nd.GioiTinh,
                    NoiSinh = nd.NoiSinh,
                    MaChucVu = nd.MaChucVu,                 // Giải mã dữ liệu nhạy cảm
                    SoCccd = !string.IsNullOrEmpty(nd.SoCccd) ? SecurityHelper.Decrypt(nd.SoCccd) : "",
                    BaoHiemXaHoi = !string.IsNullOrEmpty(nd.BaoHiemXaHoi) ? SecurityHelper.Decrypt(nd.BaoHiemXaHoi) : "",
                    SoTaiKhoan = !string.IsNullOrEmpty(nd.SoTaiKhoan) ? SecurityHelper.Decrypt(nd.SoTaiKhoan) : ""
                };

                // 4. Nếu là tài xế, đính kèm thêm thông tin nghiệp vụ
                if (nd.MaChucVu == 16)
                {
                    result.ThongTinTaiXe = new TaiXeDetailModel
                    {
                        SoBangLai = nd.TaiXe.SoBangLai,
                        LoaiBangLai = nd.TaiXe.LoaiBangLai,
                        NgayCapBang = nd.TaiXe.NgayCapBang,
                        NgayHetHanBang = nd.TaiXe.NgayHetHanBang,
                        KinhNghiemNam = nd.TaiXe.KinhNghiemNam,
                        DiemUyTin = nd.TaiXe.DiemUyTin,
                        TrangThaiHoatDong = nd.TaiXe.TrangThaiHoatDong,
                        AnhBangLaiTruoc = nd.TaiXe.AnhBangLaiTruoc,
                        AnhBangLaiSau = nd.TaiXe.AnhBangLaiSau
                    };
                }

                // 5. Lưu vào Cache (Hết hạn sau 10 phút)
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));

                _cache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin tổng hợp cho ID: {Id}", maNhanVien);
                return StatusCode(500, "Lỗi hệ thống khi tải thông tin chi tiết.");
            }
        }

        [HttpPost("themnhanvien")]
        public async Task<IActionResult> ThemNhanVien([FromBody] NguoiDungInputModel model)
        {
            // 1. Kiểm tra dữ liệu cơ bản
            if (string.IsNullOrEmpty(model.TenDangNhap)) return BadRequest(new { message = "Tên đăng nhập không được để trống." });           
            if (string.IsNullOrEmpty(model.HoTenNhanVien)) return BadRequest(new { message = "Họ tên nhân viên không được để trống." });
            if (string.IsNullOrEmpty(model.Email)) return BadRequest(new { message = "Email không được để trống." });
            // --- BỔ SUNG: LOGIC TỰ SINH MẬT KHẨU 10 KÝ TỰ (CHỮ VÀ SỐ) ---
            var random = new Random();
            // Thêm chữ cái viết hoa và viết thường vào chuỗi nguồn
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

            string matKhauTuSinh = new string(Enumerable.Repeat(chars, 10)
                                              .Select(s => s[random.Next(s.Length)]).ToArray());
            // 2. KIỂM TRA RÀNG BUỘC TÀI XẾ (MỚI)
            if (model.MaChucVu == 16)
            {
                if (model.ThongTinTaiXe == null)
                {
                    return BadRequest(new { message = "Vui lòng nhập đầy đủ thông tin bằng lái cho tài xế." });
                }

                if (string.IsNullOrEmpty(model.ThongTinTaiXe.SoBangLai))
                    return BadRequest(new { message = "Số bằng lái không được để trống." });

                if (string.IsNullOrEmpty(model.ThongTinTaiXe.LoaiBangLai))
                    return BadRequest(new { message = "Loại bằng lái không được để trống." });

                if (model.ThongTinTaiXe.NgayHetHanBang == default)
                    return BadRequest(new { message = "Ngày hết hạn bằng lái không hợp lệ." });
            }

            // 3. Kiểm tra trùng lặp trong Database
            if (_context.TaiKhoans.Any(tk => tk.TenDangNhap == model.TenDangNhap))
                return Conflict(new { message = "Tên đăng nhập đã tồn tại" });
            if (_context.NguoiDungs.Any(nd => nd.Email == model.Email))
                return Conflict(new { message = "Email đã tồn tại" });
            if (!string.IsNullOrEmpty(model.SoDienThoai) && _context.NguoiDungs.Any(nd => nd.SoDienThoai == model.SoDienThoai))
                return Conflict(new { message = "Số điện thoại đã tồn tại" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 4. Tạo tài khoản
                var newTaiKhoan = new TaiKhoan
                {
                    TenDangNhap = model.TenDangNhap,
                    MatKhauHash = SecurityHelper.Encrypt(matKhauTuSinh),
                    HoatDong = true
                };

                _context.TaiKhoans.Add(newTaiKhoan);
                await _context.SaveChangesAsync();

                // 5. Tạo thông tin người dùng
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
                    // Mã hóa các thông tin nhạy cảm
                    SoCccd = !string.IsNullOrEmpty(model.SoCccd) ? SecurityHelper.Encrypt(model.SoCccd) : null,
                    SoTaiKhoan = !string.IsNullOrEmpty(model.SoTaiKhoan) ? SecurityHelper.Encrypt(model.SoTaiKhoan) : null,
                    BaoHiemXaHoi = !string.IsNullOrEmpty(model.BaoHiemXaHoi) ? SecurityHelper.Encrypt(model.BaoHiemXaHoi) : null
                };

                _context.NguoiDungs.Add(newNguoiDung);
                await _context.SaveChangesAsync();
                
                // 6. Lưu thông tin tài xế nếu MaChucVu = 16
                if (model.MaChucVu == 16 && model.ThongTinTaiXe != null)
                {
                    // Đảm bảo MaNguoiDung đã có từ bước tạo NguoiDung phía trên
                    var taiXe = new TaiXe
                    {
                        MaNguoiDung = newNguoiDung.MaNguoiDung,
                        SoBangLai = model.ThongTinTaiXe.SoBangLai,
                        LoaiBangLai = model.ThongTinTaiXe.LoaiBangLai,
                        NgayCapBang = model.ThongTinTaiXe.NgayCapBang,
                        NgayHetHanBang = model.ThongTinTaiXe.NgayHetHanBang,

                        // Tính kinh nghiệm (ép kiểu int cho đồng bộ model)
                        KinhNghiemNam = (int)(DateTime.Now.Year - (model.ThongTinTaiXe.NgayCapBang?.Year ?? DateTime.Now.Year)),

                        DiemUyTin = 10m, // Đã có hậu tố m cho decimal
                        TrangThaiHoatDong = "Sẵn sàng"
                    };                 
                    _context.TaiXes.Add(taiXe);
                    await _context.SaveChangesAsync();
                }

                // 7. Gửi Email thông báo
                try
                {
                    await _emailService.SendAccountInfoAsync(
                        model.Email,
                        model.HoTenNhanVien,
                        model.TenDangNhap,
                        matKhauTuSinh
                    );
                }
                catch (Exception exMail)
                {
                    _logger.LogWarning(exMail, "Tài khoản tạo thành công nhưng không gửi được Email tới: {Email}", model.Email);
                }

                await transaction.CommitAsync();

                // 8. Reset Cache (SignalR hoặc CancellationToken)
                ClearUserCache();

                return Ok(new { message = "Thêm nhân viên thành công và đã gửi thông tin đăng nhập vào Email!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi nghiêm trọng khi thêm nhân viên");
                var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "Lỗi hệ thống: " + errorMessage });
            }
        }

        [HttpPut("capnhatnhanvien/{maNhanVien}")]
        public async Task<IActionResult> CapNhatNhanVien(int maNhanVien, [FromBody] NguoiDungUpdateModel model)
        {
            // 1. Tìm nhân viên và bao gồm luôn bảng Chức vụ để lấy tên chức vụ cũ
            var existingNguoiDung = await _context.NguoiDungs
                .Include(nd => nd.MaChucVuNavigation)
                .FirstOrDefaultAsync(nd => nd.MaNguoiDung == maNhanVien);

            if (existingNguoiDung == null) return NotFound(new { message = "Không tìm thấy nhân viên." });

            try
            {
                // 2. Lấy tên chức vụ MỚI từ database dựa trên model.MaChucVu
                string tenChucVuMoi = "Chưa xác định";
                if (model.MaChucVu.HasValue)
                {
                    var cvMoi = await _context.ChucVus.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.MaChucVu == model.MaChucVu);
                    tenChucVuMoi = cvMoi?.TenChucVu ?? "Không tồn tại";
                }

                // 3. Lưu dữ liệu cũ TRƯỚC khi gán (Lấy TenChucVu thay vì Mã)
                var dataCu = new Dictionary<string, object> {
                    { "Họ tên", existingNguoiDung.HoTenNhanVien ?? "" },
                    { "Đơn vị", existingNguoiDung.DonViLamViec ?? "" },
                    { "Chức vụ", existingNguoiDung.MaChucVuNavigation?.TenChucVu ?? "Chưa có" }
                };

                // 4. Gán giá trị mới
                existingNguoiDung.DonViLamViec = model.DonViLamViec;
                existingNguoiDung.MaKho = model.MaKho;
                existingNguoiDung.MaChucVu = model.MaChucVu;
                existingNguoiDung.HoTenNhanVien = model.HoTenNhanVien;
                existingNguoiDung.NgaySinh = model.NgaySinh;

                if (!string.IsNullOrWhiteSpace(model.BaoHiemXaHoi))
                {
                    existingNguoiDung.BaoHiemXaHoi = SecurityHelper.Encrypt(model.BaoHiemXaHoi.Trim());
                }

                _context.NguoiDungs.Update(existingNguoiDung);
                await _context.SaveChangesAsync();

                ClearUserCache(maNhanVien);

                // 5. Ghi Log với Tên chức vụ mới
                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý nhân viên",
                    "Cập nhật thông tin nhân viên: " + model.HoTenNhanVien,
                    "NguoiDung",
                    maNhanVien.ToString(),
                    dataCu,
                    new Dictionary<string, object> {
                { "Họ tên", model.HoTenNhanVien },
                { "Đơn vị", model.DonViLamViec },
                { "Chức vụ", tenChucVuMoi } // Sử dụng tên đã truy vấn ở trên
                    }
                );

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

        //API Lấy thông tên tài xế để hiện ở trong giao diện theo dõi lộ trình 
        [HttpGet("lay-ten-nhan-vien/{maNguoiDung}")] // 1. Dùng HttpGet cho tác vụ đọc dữ liệu
        public async Task<IActionResult> LayTenTaiXe(int maNguoiDung)
        {
            try
            {
                // 2. Tận dụng MemoryCache: Kiểm tra xem tên tài xế này đã có trong cache chưa
                string cacheKey = $"TenTaiXe_{maNguoiDung}";
                if (_cache.TryGetValue(cacheKey, out string tenTaiXeCached))
                {
                    return Ok(new TenNhanVienModel
                    {
                        MaNguoiDung = maNguoiDung,
                        TenTaiXeThucHien = tenTaiXeCached

                    });
                }

                // 3. Tối ưu EF Core: Dùng Select để ép DB chỉ query đúng 1 cột HoTenNhanVien
                var tenTaiXeDb = await _context.TaiXes
                    .Where(tx => tx.MaNguoiDung == maNguoiDung)
                    .Select(tx => tx.MaNguoiDungNavigation.HoTenNhanVien) // Không load cả object vào RAM
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(tenTaiXeDb))
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tài xế hoặc thông tin người dùng." });
                }

                // Lưu kết quả vào Cache trong 60 phút để tái sử dụng cho các request sau
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(60));
                _cache.Set(cacheKey, tenTaiXeDb, cacheOptions);

                return Ok(new TenNhanVienModel
                {
                    MaNguoiDung = maNguoiDung,
                    TenTaiXeThucHien = tenTaiXeDb
                });
            }
            catch (Exception ex)
            {
                // 4. Ghi log chi tiết lỗi cho Dev đọc, trả lỗi chung chung cho Client
                _logger.LogError(ex, "Lỗi xảy ra khi lấy tên tài xế cho mã người dùng: {MaNguoiDung}", maNguoiDung);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống nội bộ. Vui lòng thử lại sau." });
            }
        }

        private void ClearUserCache(int? maNhanVien = null)
        {
            // 1. Xóa các cache chi tiết nếu có ID cụ thể
            if (maNhanVien.HasValue)
            {
                _cache.Remove($"UserDetail_{maNhanVien}");
                _cache.Remove($"UserFullDetail_{maNhanVien}");
                _cache.Remove($"TenTaiXe_{maNhanVien}");
            }

            // 2. Phát tín hiệu Cancel để xóa toàn bộ các cache danh sách 
            // (những cache đã đăng ký .AddExpirationToken(_resetCacheSignal.Token))
            _resetCacheSignal.Cancel();

            // 3. Khởi tạo lại Token mới để sẵn sàng cho các lượt cache tiếp theo
            _resetCacheSignal = new CancellationTokenSource();

            _logger.LogInformation("Đã xóa cache người dùng và phát tín hiệu reset danh sách.");
        }
    }
}