using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12._1234;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyTaiXe;

namespace QuanLyTaiKhoanNguoiDung.ControllersAPI
{
    [Route("api/quanlyllichsuvipham")]
    [ApiController]
    public class QuanLyLichSuViPham : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<QuanLyLichSuViPham> _logger;
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService; // Dùng Interface chuẩn

        public QuanLyLichSuViPham(TmdtContext context,
                               ILogger<QuanLyLichSuViPham> logger,
                               IMemoryCache cache,
                               IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _emailService = emailService;
        }
        [HttpGet("LichSuViPham/{maNguoiDung}")]
        public async Task<IActionResult> GetLichSuViPham(int maNguoiDung)
        {
            // 1. Tạo khóa Cache duy nhất cho mỗi tài xế
            string cacheKey = $"ViolationHistory_{maNguoiDung}";

            try
            {
                // 2. Thử lấy dữ liệu từ Cache
                if (!_cache.TryGetValue(cacheKey, out List<LichSuViPhamModels> lichSu))
                {
                    _logger.LogInformation("Cache miss cho MaNguoiDung: {maNguoiDung}. Đang truy vấn Database...", maNguoiDung);

                    // 3. Nếu Cache không có, truy vấn từ Database
                    lichSu = await _context.LichSuViPhams
                        .Where(ls => ls.MaTaiXe == maNguoiDung)
                        .Select(ls => new LichSuViPhamModels
                        {
                            MaViPham = ls.MaViPham,
                            MaTaiXe = ls.MaTaiXe,
                            NgayViPham = ls.NgayViPham,
                            LoaiViPham = ls.LoaiViPham,
                            MoTaChiTiet = ls.MoTaChiTiet,
                            MucPhat = ls.MucPhat,
                            HinhThucXuLy = ls.HinhThucXuLy,
                            TrangThaiXuLy = ls.TrangThaiXuLy,
                            NguoiLapBienBan = ls.NguoiLapBienBan
                        })
                        .ToListAsync();

                    // 4. Nếu có dữ liệu, thiết lập cấu hình và lưu vào Cache
                    if (lichSu != null && lichSu.Any())
                    {
                        var cacheOptions = new MemoryCacheEntryOptions()
                            .SetSlidingExpiration(TimeSpan.FromMinutes(10)) // Hết hạn nếu không truy cập trong 10p
                            .SetAbsoluteExpiration(TimeSpan.FromHours(1))   // Hết hạn tuyệt đối sau 1h
                            .SetPriority(CacheItemPriority.Normal);

                        _cache.Set(cacheKey, lichSu, cacheOptions);
                    }
                }
                else
                {
                    _logger.LogInformation("Cache hit cho MaNguoiDung: {maNguoiDung}", maNguoiDung);
                }

                // 5. Trả về kết quả
                if (lichSu == null || !lichSu.Any())
                {
                    return NotFound(new { message = "Không tìm thấy lịch sử vi phạm cho người dùng này." });
                }

                return Ok(lichSu);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy lịch sử vi phạm cho MaNguoiDung: {maNguoiDung}", maNguoiDung);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xử lý yêu cầu." });
            }
        }
    }
}
