using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuanLyDonHang.Models1;

namespace QuanLyDonHang.ControllersAPI
{
    [Route("api/danhmucloaihang")]
    [ApiController]
    public class DanhMucLoaiHang : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<DanhMucLoaiHang> _logger;
        private readonly IMemoryCache _cache;
        private const string ALL_LOAI_HANG_CACHE_KEY = "AllLoaiHang";

        public DanhMucLoaiHang(TmdtContext context, ILogger<DanhMucLoaiHang> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        // 1. API Lấy tất cả loại hàng (Dùng cho Dropdown trong Modal)
        [HttpGet("laytatcaloaihang")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                if (!_cache.TryGetValue(ALL_LOAI_HANG_CACHE_KEY, out List<DanhMucLoaiHangModels> listLoaiHang))
                {
                    listLoaiHang = await _context.DanhMucLoaiHangs
                        .Select(x => new DanhMucLoaiHangModels
                        {
                            MaLoaiHang = x.MaLoaiHang,
                            TenLoaiHang = x.TenLoaiHang,
                            MoTa = x.MoTa
                        }).ToListAsync();

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(1));

                    _cache.Set(ALL_LOAI_HANG_CACHE_KEY, listLoaiHang, cacheOptions);
                }
                return Ok(listLoaiHang);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy danh sách loại hàng");
                return StatusCode(500, "Lỗi hệ thống");
            }
        }

        [HttpGet("laymotloaihang")]
        public async Task<IActionResult> ChiTiet(int maloaihang)
        {
            if (maloaihang <= 0) return BadRequest("Mã không hợp lệ.");

            string cacheKey = $"LoaiHang_{maloaihang}";

            try
            {
                // Thêm dấu ? vào kiểu dữ liệu để chấp nhận null từ TryGetValue
                if (!_cache.TryGetValue(cacheKey, out DanhMucLoaiHangModels? loaiHang))
                {
                    loaiHang = await _context.DanhMucLoaiHangs
                        .Where(x => x.MaLoaiHang == maloaihang)
                        .Select(x => new DanhMucLoaiHangModels
                        {
                            MaLoaiHang = x.MaLoaiHang,
                            TenLoaiHang = x.TenLoaiHang ?? "Chưa có tên", // Xử lý null cho từng thuộc tính
                            MoTa = x.MoTa
                        }).FirstOrDefaultAsync();

                    if (loaiHang == null)
                    {
                        return NotFound($"Không tìm thấy mã {maloaihang}");
                    }

                    _cache.Set(cacheKey, loaiHang, TimeSpan.FromMinutes(30));
                }

                return Ok(loaiHang);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống");
                return StatusCode(500, "Lỗi hệ thống.");
            }
        }
    }
}