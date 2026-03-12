using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyLoTrinhTheoDoi.Models12;
using System;

namespace QuanLyLoTrinhTheoDoi.ConTrollersAPI
{
    [Route("api/baocaosuco")]
    [ApiController]
    public class BaoCaoSuCoAPI : ControllerBase
    {

        private readonly TmdtContext _context;
        private readonly ILogger<BaoCaoSuCoAPI> _logger;
        private readonly IMemoryCache _cache;
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public BaoCaoSuCoAPI(IHttpContextAccessor httpContextAccessor, TmdtContext context, ILogger<BaoCaoSuCoAPI> logger, IMemoryCache cache)
        {

            _context = context;
            _logger = logger;
            _cache = cache;
        }

        [HttpGet("danhsachsuco")]
        public async Task<IActionResult> getSuCo(
            [FromQuery] string? Search,
            [FromQuery] string? trangThai = "Tất cả",
            [FromQuery] string? loai = "Tất cả",
            [FromQuery] int page = 1,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                // Kiểm tra logic đầu vào cơ bản
                if (page <= 0) page = 1;

                string cacheKey = $"getSuCo-{Search}-{trangThai}-{loai}-{page}-{fromDate}-{toDate}";

                if (!_cache.TryGetValue(cacheKey, out object cachedData))
                {
                    int pageSize = 10;
                    var query = _context.SuCos
                        .Include(sc => sc.MaLoaiSuCoNavigation)
                        .AsQueryable();

                    // --- BẮT ĐẦU LỌC DỮ LIỆU ---

                    if (!string.IsNullOrEmpty(Search))
                    {
                        query = query.Where(suco => suco.MoTa != null && suco.MoTa.Contains(Search));
                    }

                    if (trangThai != "Tất cả")
                    {
                        query = trangThai switch
                        {
                            "Chưa xử lý" => query.Where(sc => sc.TrangThai == "Chưa xử lý" || sc.TrangThai == null),
                            "Đã xử lý" => query.Where(sc => sc.TrangThai == "Đã xử lý"),
                            "Mới" => query.Where(sc => sc.TrangThai == "Mới"),
                            "Chờ phản hồi" => query.Where(sc => sc.TrangThai == "Chờ phản hồi"),
                            "Từ chối" => query.Where(sc => sc.TrangThai == "Từ chối"),
                            _ => query.Where(sc => sc.TrangThai == trangThai)
                        };
                    }

                    // Lọc theo loại sự cố (Dùng MaLoaiSuCo)
                    if (loai != "Tất cả" && !string.IsNullOrEmpty(loai))
                    {
                        query = query.Where(suco => suco.MaLoaiSuCo.Equals(loai));
                    }

                    if (fromDate.HasValue)
                    {
                        query = query.Where(s => s.ThoiGianBaoCao >= fromDate.Value);
                    }

                    if (toDate.HasValue)
                    {
                        var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
                        query = query.Where(s => s.ThoiGianBaoCao <= endOfDay);
                    }

                    // --- KẾT THÚC LỌC DỮ LIỆU ---

                    int totalItems = await query.CountAsync();

                    var sucoList = await query
                        .OrderByDescending(suco => suco.ThoiGianBaoCao)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(sc => new SuCoModels
                        {
                            MaSuCo = sc.MaSuCo,
                            MaLoTrinh = sc.MaLoTrinh,
                            MoTa = sc.MoTa,
                            ThoiGianBaoCao = sc.ThoiGianBaoCao,
                            ThoiGianXuLy = sc.ThoiGianXuLy,
                            TrangThai = sc.TrangThai,
                            MaLoaiSuCo = sc.MaLoaiSuCo,

                            MaLoaiSuCoNavigation = sc.MaLoaiSuCoNavigation != null ? new LoaiSuCoModels
                            {
                                TenLoaiSuCo = sc.MaLoaiSuCoNavigation.TenLoaiSuCo,
                                MucDoNghiemTrong = sc.MaLoaiSuCoNavigation.MucDoNghiemTrong,
                                GhiChu = sc.MaLoaiSuCoNavigation.GhiChu
                            } : null
                        })
                        .ToListAsync();

                    cachedData = new
                    {
                        TotalItems = totalItems,
                        PageSize = pageSize,
                        CurrentPage = page,
                        TotalPages = (int)Math.Ceiling((double)totalItems / pageSize),
                        SuCoList = sucoList
                    };

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, cachedData, cacheEntryOptions);
                }

                return Ok(cachedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách sự cố");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Đã xảy ra lỗi hệ thống khi truy xuất dữ liệu.", detail = ex.Message });
            }
        }

        [HttpGet("getloaisuco")]
        public async Task<IActionResult> getLoaiSuCo()
        {
            try
            {
                var loaiSuCoList = await _context.LoaiSuCos
                    .Select(lsc => new LoaiSuCoModels
                    {
                        MaLoaiSuCo = lsc.MaLoaiSuCo,
                        TenLoaiSuCo = lsc.TenLoaiSuCo,
                        MucDoNghiemTrong = lsc.MucDoNghiemTrong,
                        GhiChu = lsc.GhiChu
                    })
                    .ToListAsync();

                return Ok(loaiSuCoList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách loại sự cố");
                return StatusCode(500, new { message = "Không thể tải danh sách loại sự cố." });
            }
        }
        [HttpGet("chitietsuco/{maSuCo}")]
        public async Task<IActionResult> GetChiTietSuCo(int maSuCo)
        {
            try
            {
                string cacheKey = $"chiTietSuCo-{maSuCo}";

                if (!_cache.TryGetValue(cacheKey, out SuCoModels? suCoDetail))
                {
                    // Truy vấn database kèm theo thông tin loại sự cố
                    var data = await _context.SuCos
                        .Include(sc => sc.MaLoaiSuCoNavigation)
                        .Include(lt => lt.MaLoTrinhNavigation)
                        .FirstOrDefaultAsync(sc => sc.MaSuCo == maSuCo);

                    if (data == null)
                    {
                        return NotFound(new { message = $"Không tìm thấy sự cố với mã {maSuCo}" });
                    }

                    // Ánh xạ sang Model trả về
                    suCoDetail = new SuCoModels
                    {
                        MaSuCo = data.MaSuCo,
                        MaLoTrinh = data.MaLoTrinh,
                        MoTa = data.MoTa,
                        ThoiGianBaoCao = data.ThoiGianBaoCao,
                        ThoiGianXuLy = data.ThoiGianXuLy,
                        TrangThai = data.TrangThai,
                        MaLoaiSuCo = data.MaLoaiSuCo,
                        UrlHinhAnhSuCo = data.UrlHinhAnhSuCo,
                        UrlVideoSuCo = data.UrlVideoSuCo,
                        ViDo = data.ViDo,
                        KinhDo = data.KinhDo,
                        DiaChiCuThe = data.DiaChiCuThe,
                        MaLoTrinhNavigation = new LoTrinhModels
                        {
                            MaLoTrinh = data.MaLoTrinhNavigation.MaLoTrinh,
                            MaTaiXeChinh = data.MaLoTrinhNavigation.MaTaiXeChinh,
                            MaTaiXePhu = data.MaLoTrinhNavigation.MaTaiXePhu,
                            MaPhuongTien = data.MaLoTrinhNavigation.MaPhuongTien,
                            ThoiGianBatDauKeHoach = data.MaLoTrinhNavigation.ThoiGianBatDauKeHoach,
                            ThoiGianBatDauThucTe = data.MaLoTrinhNavigation.ThoiGianBatDauThucTe,
                            TrangThai = data.MaLoTrinhNavigation.TrangThai
                        },
                        MaLoaiSuCoNavigation = data.MaLoaiSuCoNavigation != null ? new LoaiSuCoModels
                        {
                            TenLoaiSuCo = data.MaLoaiSuCoNavigation.TenLoaiSuCo,
                            MucDoNghiemTrong = data.MaLoaiSuCoNavigation.MucDoNghiemTrong,
                            GhiChu = data.MaLoaiSuCoNavigation.GhiChu
                        } : null
                    };

                    // Thiết lập Cache cho chi tiết sự cố
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));
                    _cache.Set(cacheKey, suCoDetail, cacheEntryOptions);
                }

                return Ok(suCoDetail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy chi tiết sự cố {maSuCo}");
                return StatusCode(500, new { message = "Lỗi hệ thống khi lấy thông tin chi tiết." });
            }
        }
        // API này cho phép admin cập nhật trạng thái mới -> đang xử lý/từ chối , người dùng cập nhật từ đang xử lý -> đã xử lý
        [HttpPut("capnhatsuco/{maSuCo}")]
        public async Task<IActionResult> CapNhatSuCo(int maSuCo, [FromBody] CapNhatSuCoDTO suCoUpdate)
        {
            try
            {
                var existingSuCo = await _context.SuCos.FindAsync(maSuCo);
                if (existingSuCo == null)
                {
                    return NotFound(new { message = $"Không tìm thấy sự cố với mã {maSuCo}" });
                }

                // Logic kiểm tra trạng thái cũ (giữ nguyên của bạn)
                if (existingSuCo.TrangThai == "Xác nhận" && suCoUpdate.TrangThai != "Xác nhận")
                {
                    return BadRequest(new { message = "Sự cố đã hoàn tất xử lý, không thể thay đổi trạng thái." });
                }

                // Cập nhật dữ liệu
                existingSuCo.TrangThai = suCoUpdate.TrangThai;

                if (suCoUpdate.TrangThai == "Từ chối")
                {
                    existingSuCo.GhiChuTuChoi = suCoUpdate.GhiChuTuChoi;
                    existingSuCo.ThoiGianXuLy = DateTime.Now;
                }
                else if (suCoUpdate.TrangThai == "Đã xử lý")
                {
                    existingSuCo.ThoiGianXuLy = DateTime.Now;
                    existingSuCo.GhiChuTuChoi = null;
                }
                else if(suCoUpdate.TrangThai == "Đang xử lý")
                {
                    existingSuCo.ThoiGianXuLy = DateTime.Now;
                    existingSuCo.GhiChuTuChoi = null;
                }

                    // LƯU VÀO DATABASE
                    await _context.SaveChangesAsync();

                // --- BẮT ĐẦU XỬ LÝ XÓA CACHE ---
                // Phát tín hiệu hủy để xóa tất cả cache cũ liên quan đến danh sách và chi tiết
                _resetCacheSignal.Cancel();

                // Khởi tạo lại Signal mới cho các lượt cache tiếp theo
                _resetCacheSignal = new CancellationTokenSource();
                // --- KẾT THÚC XỬ LÝ XÓA CACHE ---

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật thành công và đã làm mới bộ nhớ đệm.",
                    data = existingSuCo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi cập nhật sự cố {maSuCo}");
                return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // cần tạo 1 APi để tài xế có thể kháng cáo khi sự cố bị từ chối
    }
}