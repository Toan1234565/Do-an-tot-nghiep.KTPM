using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12._1234;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNhatKyHeThong;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhanQuyen;
using System.Security.Claims;
using TaiKhoan1.ControllersAPI;
using Tmdt.Shared.Services;

namespace QuanLyTaiKhoanNguoiDung.ControllersAPI
{
    [Route("api/quanlyphanquyen")]
    [ApiController]
    public class QuanLyPhanQuyen : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<QuanLyPhanQuyen> _logger;
        private readonly IEmailService _emailService;
        private readonly RabbitMQClient _rabbitMQ;
        private readonly PhanQuyenService _phanQuyen;
        private readonly IMemoryCache _cache;
        private readonly ISystemService _sys;

        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();
        public QuanLyPhanQuyen(TmdtContext context, ILogger<QuanLyPhanQuyen> logger, IEmailService emailService, RabbitMQClient rabbitMQ, PhanQuyenService phanQuyen, ISystemService sys, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
            this._rabbitMQ = rabbitMQ;
            _phanQuyen = phanQuyen;
            _sys = sys;
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
            // 1. Kiểm tra tồn tại chức vụ
            if (_context.ChucVus.Any(cv => cv.TenChucVu == model.TenChucVu))
            {
                return Conflict(new { message = "Chức vụ này đã tồn tại." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 2. Thêm mới chức vụ
                var newChucVu = new ChucVu
                {
                    TenChucVu = model.TenChucVu,
                    MaVaiTro = model.MaVaiTro,
                };

                _context.ChucVus.Add(newChucVu);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 3. Lấy thông tin bổ sung để ghi Log (Tên vai trò & Tên nhân viên)
                _cache.Remove("List_ChucVu");

                var currentUserId = GetCurrentUserId();
                var currentUser = await _context.NguoiDungs
                    .FirstOrDefaultAsync(nd => nd.MaNguoiDung == currentUserId);

                // TRUY VẤN TÊN VAI TRÒ TỪ MÃ VAI TRÒ
                var vaiTro = await _context.VaiTros
                    .FirstOrDefaultAsync(vt => vt.MaVaiTro == model.MaVaiTro);
                string tenVaiTro = vaiTro?.TenVaiTro ?? "Không xác định";

                // 4. Ghi Log hệ thống với đầy đủ thông tin chữ
                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý phân quyền",
                    $"Thêm mới chức vụ '{model.TenChucVu}' bởi {currentUser?.HoTenNhanVien}",
                    "ChucVu",
                    currentUser?.ToString(),
                    new Dictionary<string, object> { { "Trạng thái", "Tạo mới" } },
                    new Dictionary<string, object> {
                        { "Tên chức vụ", model.TenChucVu },
                        { "Vai trò", tenVaiTro }, // Đã chuyển từ Mã sang Tên
                        { "Người thực hiện", currentUser?.HoTenNhanVien }
                    }
                );

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
            if (await _context.ChucVus.AnyAsync(cv => cv.TenChucVu == model.TenChucVu && cv.MaChucVu != id))
            {
                return Conflict(new { message = "Tên chức vụ này đã tồn tại." });
            }

            try
            {
                // --- LẤY THÔNG TIN CŨ ĐỂ GHI LOG ---
                var vaiTroCu = await _context.VaiTros.FirstOrDefaultAsync(vt => vt.MaVaiTro == chucVu.MaVaiTro);
                string tenVaiTroCu = vaiTroCu?.TenVaiTro ?? "Không xác định";
                string tenChucVuCu = chucVu.TenChucVu;

                // --- CẬP NHẬT DỮ LIỆU MỚI ---
                chucVu.TenChucVu = model.TenChucVu;
                chucVu.MaVaiTro = model.MaVaiTro;

                _context.ChucVus.Update(chucVu);
                await _context.SaveChangesAsync();
                _cache.Remove("List_ChucVu");

                // --- LẤY THÔNG TIN MỚI (Tên vai trò mới) ---
                var vaiTroMoi = await _context.VaiTros.FirstOrDefaultAsync(vt => vt.MaVaiTro == model.MaVaiTro);
                string tenVaiTroMoi = vaiTroMoi?.TenVaiTro ?? "Không xác định";

                var currentUserId = GetCurrentUserId();
                var currentUser = await _context.NguoiDungs.FirstOrDefaultAsync(nd => nd.MaNguoiDung == currentUserId);

                // --- GHI LOG HỆ THỐNG ---
                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý phân quyền",
                    $"Sửa chức vụ '{tenChucVuCu}' thành '{model.TenChucVu}' bởi {currentUser?.HoTenNhanVien}",
                    "ChucVu",
                    currentUser?.ToString(),
                    // Dữ liệu cũ
                    new Dictionary<string, object> {
                        { "Tên chức vụ", tenChucVuCu },
                        { "Vai trò", tenVaiTroCu }
                    },
                    // Dữ liệu mới
                    new Dictionary<string, object> {
                        { "Tên chức vụ", model.TenChucVu },
                        { "Vai trò", tenVaiTroMoi },
                        { "Người thực hiện", currentUser?.HoTenNhanVien ?? "Hệ thống" }
                    }
                );

                return Ok(new { message = "Cập nhật chức vụ thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }
}
