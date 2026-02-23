using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace QuanLyDonHang.ControllersAPI
{
    [Route("api/thongke_dichvu")]
    [ApiController]
    public class ThongKe : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<ThongKe> _logger;
        private readonly IMemoryCache _cache;
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();
        public ThongKe(TmdtContext context, ILogger<ThongKe> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }
        [HttpGet("thongke-mucdichvu")]
        public async Task<IActionResult> ThongKeTheoMucDichVu([FromQuery] string sortOrder = "desc")
        {
            // 1. Tạo Cache Key (bao gồm cả sortOrder để tránh lấy nhầm kết quả sắp xếp khác)
            string cacheKey = $"thongke_donhang_mucdichvu_{sortOrder}";

            if (_cache.TryGetValue(cacheKey, out object cachedData))
            {
                return Ok(cachedData);
            }

            try
            {
                // 2. Tạo truy vấn cơ bản
                var query = _context.DonHangs
                    .GroupBy(dh => dh.MaLoaiDv)
                    .Select(g => new
                    {
                        MaLoaiDv = g.Key,
                        SoLuongDonHang = g.Count()
                    });

                // 3. Thực hiện sắp xếp dựa trên tham số truyền vào
                if (sortOrder.ToLower() == "asc")
                {
                    query = query.OrderBy(s => s.SoLuongDonHang); // Tăng dần
                }
                else
                {
                    query = query.OrderByDescending(s => s.SoLuongDonHang); // Giảm dần (mặc định)
                }

                var stats = await query.ToListAsync();

                // 4. Thiết lập Cache
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                _cache.Set(cacheKey, stats, cacheOptions);

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thống kê đơn hàng theo mức dịch vụ");
                return StatusCode(500, "Lỗi máy chủ khi xử lý thống kê.");
            }
        }
    }
}
