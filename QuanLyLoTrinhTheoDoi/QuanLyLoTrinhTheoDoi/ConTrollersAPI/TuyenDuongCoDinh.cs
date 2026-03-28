using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyLoTrinhTheoDoi.Models12.TuyenDuongCoDinh;

namespace QuanLyLoTrinhTheoDoi.ConTrollersAPI
{
    [Route("api/tuyenduongcodinh")]
    [ApiController]
    public class TuyenDuongCoDinhController : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TuyenDuongCoDinhController> _logger;

        // Tín hiệu để làm mới cache khi dữ liệu thay đổi
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public TuyenDuongCoDinhController(
            TmdtContext context,
            IMemoryCache cache,
            ILogger<TuyenDuongCoDinhController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        [HttpGet("tuyen-duong")]
        public async Task<IActionResult> GetTuyenDuongCoDinh()
        {
            try
            {
                var cacheKey = "tuyen_duong_codinh_data";

                if (!_cache.TryGetValue(cacheKey, out List<TuyenDuongCoDinhModels>? cachedData))
                {
                    _logger.LogInformation("Cache miss: Không tìm thấy dữ liệu Tuyến Đường Cố Định. Đang lấy từ cơ sở dữ liệu.");

                    // Lấy dữ liệu từ Database
                    var data = await _context.TuyenDuongCoDinhs
                        .Select(td => new TuyenDuongCoDinhModels
                        {
                            MaTuyen = td.MaTuyen,
                            MaHopDong = td.MaHopDong,
                            MaDiaChiLay = td.MaDiaChiLay,
                            MaDiaChiGiao = td.MaDiaChiGiao,
                            KhoangCachKm = td.KhoangCachKm,
                            MaXeDinhDanh = td.MaXeDinhDanh
                        })
                        .ToListAsync();

                    // Cấu hình Cache
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(30)) // Hết hạn nếu không truy cập sau 30 phút
                        .SetAbsoluteExpiration(TimeSpan.FromHours(2))   // Hết hạn tuyệt đối sau 2 giờ
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, data, cacheEntryOptions);
                    cachedData = data;
                }
                else
                {
                    _logger.LogInformation("Cache hit: Lấy dữ liệu Tuyến Đường Cố Định từ bộ nhớ đệm.");
                }

                return Ok(cachedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Đã xảy ra lỗi khi truy vấn dữ liệu Tuyến Đường Cố Định.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi hệ thống trong quá trình xử lý yêu cầu của bạn.");
            }
        }

        // Endpoint để xóa bộ nhớ đệm thủ công
        [HttpPost("clear-cache")]
        public IActionResult ClearCache()
        {
            try
            {
                _resetCacheSignal.Cancel();
                _resetCacheSignal.Dispose();
                _resetCacheSignal = new CancellationTokenSource();

                _logger.LogInformation("Bộ nhớ đệm của Tuyến Đường Cố Định đã được làm mới.");
                return Ok("Đã làm mới bộ nhớ đệm thành công.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cố gắng làm mới bộ nhớ đệm.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Không thể làm mới bộ nhớ đệm.");
            }
        }
    }
}