using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyKho.Models;
using QuanLyKho.Models1.QuanLyKho;
using QuanLyKho.Models1.QuanLyXe;

namespace QuanLyKho.ControllersAPI
{
    [Route("api/quanlykhobai")]
    [ApiController]
    public class QuanLyKho : ControllerBase
    {
        public readonly TmdtContext _context;
        public readonly ILogger<QuanLyKho> _logger;
        public readonly IMemoryCache _cache;
        private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();
        public QuanLyKho(TmdtContext context, ILogger<QuanLyKho> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }
        [HttpGet("getallkho")]
        public async Task<IActionResult> GetAllKhoBai([FromQuery] string? seach, [FromQuery] int page, [FromQuery] string? trangthai = "Tất cả", [FromQuery] string? loaikho = "Tất cả")
        {
            if (page <= 0)
            {
                page = 1;
            }
            string cachekey = $"GetAllKhoBai_{seach}_{page}_{trangthai}_{loaikho}";
            try
            {
                if (!_cache.TryGetValue(cachekey, out var cachedData))
                {
                    int pageSize = 20;
                    var query = _context.KhoBais.Include(loai => loai.MaLoaiKhoNavigation).AsNoTracking();
                    if (!string.IsNullOrEmpty(seach))
                    {
                        query = query.Where(k => k.MaKho.ToString().Contains(seach));
                    }
                    if (trangthai != "Tất cả")
                    {
                        if (trangthai == "Hoạt động")
                        {
                            query = query.Where(k => k.TrangThai == "Hoạt động");
                        }
                        else if (trangthai == "Ngừng hoạt động")
                        {
                            query = query.Where(k => k.TrangThai == "Ngừng hoạt động");
                        }
                        else if (trangthai == "Bảo trì")
                        {
                            query = query.Where(k => k.TrangThai == "Bảo trì");
                        }
                        else if (trangthai == "Đang đầy")
                        {
                            query = query.Where(k => k.TrangThai == "Đang đầy");
                        }

                    }
                    if (loaikho != "Tất cả")
                    {
                        var loaiKhoEntity = await _context.LoaiKhos
                            .AsNoTracking()
                            .FirstOrDefaultAsync(lk => lk.TenLoaiKho == loaikho);
                        if (loaiKhoEntity != null)
                        {
                            int maLoaiKho = loaiKhoEntity.MaLoaiKho;
                            query = query.Where(k => k.MaLoaiKho == maLoaiKho);
                        }
                        else
                        {
                            return NotFound($"Loại kho '{loaikho}' không tồn tại.");
                        }
                    }
                    var khoBaiList = query
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(k => new Models1.QuanLyKho.KhoBaiModels
                        {
                            MaKho = k.MaKho,
                            MaDiaChi = k.MaDiaChi,
                            TenKhoBai = k.TenKhoBai,
                            MaQuanLy = k.MaQuanLy,
                            DungTichM3 = k.DungTichM3,
                            DienTichM2 = k.DienTichM2,
                            SoDienThoaiKho = k.SoDienThoaiKho,
                            TrangThai = k.TrangThai,
                            SucChua = k.SucChua,
                            TenLoaiKho = k.MaLoaiKhoNavigation != null ? k.MaLoaiKhoNavigation.TenLoaiKho : null

                        })
                        .ToListAsync();
                    if (khoBaiList.Result.Count == 0)
                    {
                        return NotFound("Không tìm thấy kho bãi nào.");
                    }
                    cachedData = new
                    {
                        TotalItems = query.Count(),
                        TotalPages = (int)Math.Ceiling((double)query.Count() / pageSize),
                        CurrentPage = page,
                        PageSize = pageSize,
                        Data = khoBaiList.Result
                    };

                    // Lưu vào cache với thời gian hết hạn
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                        // Thêm dòng này để liên kết cache với token hủy. xóa cache khi thêm mới để tải được dữ liệu mới nhất 
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));
                    _cache.Set(cachekey, cachedData, cacheOptions);


                }
                return Ok(cachedData);
            }
            catch (SqlException ex)
            {
                // Lỗi kết nối Database
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi lấy danh sách kho bãi ");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
                // Các lỗi không xác định khác
                _logger.LogError(ex, "Lỗi hệ thống khi lấy danh sách kho bãi  với Term: {SearchTerm}", seach);
                return StatusCode(500, "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.");
            }


        }

        [HttpGet("danhsachtenkho")]
        public async Task<IActionResult> danhSachTen()
        {
            try
            {
                var dsTen = await _context.KhoBais.AsNoTracking().Select(tenKho => new KhoBaiModels
                {
                   TenKhoBai = tenKho.TenKhoBai,
                   MaKho = tenKho.MaKho
                }).ToListAsync();
                if(dsTen == null)
                {
                    return NotFound("Khong tim thay");
                }
                return Ok(dsTen);
            }catch(Exception ex)
            {
                _logger.LogError("Loi ");
                return StatusCode(503, ex.Message);
            }
        }
        [HttpGet("getallloaikho")]
        public async Task<IActionResult> GetAllLoaiKho()
        {
            try
            {
                var loaiKhoList = await _context.LoaiKhos
                    .AsNoTracking()
                    .Select(lk => new Models1.QuanLyKho.LoaiKhoModels
                    {
                        MaLoaiKho = lk.MaLoaiKho,
                        TenLoaiKho = lk.TenLoaiKho,
                        GhiChu = lk.GhiChu
                    })
                    .ToListAsync();
                if (loaiKhoList.Count == 0)
                {
                    return NotFound("Không tìm thấy loại kho nào.");
                }
                return Ok(loaiKhoList);
            }
            catch (SqlException ex)
            {
                // Lỗi kết nối Database
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi lấy danh sách loại kho");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
                // Các lỗi không xác định khác
                _logger.LogError(ex, "Lỗi hệ thống khi lấy danh sách loại kho");
                return StatusCode(500, "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.");
            }
        }

        [HttpPost("themkhomoi")]
        public async Task<IActionResult> ThemKhoMoi([FromBody] KhoBaiModels newKho)
        {
            try
            {
                var khoBaiEntity = new KhoBai
                {
                    MaDiaChi = newKho.MaDiaChi,
                    MaQuanLy = newKho.MaQuanLy,
                    DungTichM3 = newKho.DungTichM3,
                    TenKhoBai = newKho.TenKhoBai,
                    DienTichM2 = newKho.DienTichM2,
                    TrangThai = newKho.TrangThai,
                    SucChua = newKho.SucChua,
                    SoDienThoaiKho = newKho.SoDienThoaiKho,
                    MaLoaiKho = newKho.MaLoaiKho
                };
                _context.KhoBais.Add(khoBaiEntity);
                await _context.SaveChangesAsync();
                // Hủy bỏ token cũ để xóa toàn bộ các cache liên quan đến danh sách loại xe
                _resetCacheToken.Cancel();
                _resetCacheToken.Dispose();
                // Khởi tạo token mới cho các lượt cache tiếp theo
                _resetCacheToken = new CancellationTokenSource();
                return Ok(new { Message = "Thêm kho mới thành công", MaKho = khoBaiEntity.MaKho });
            }
            catch (SqlException ex)
            {
                // Lỗi kết nối Database
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi thêm kho mới");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
                // Các lỗi không xác định khác
                _logger.LogError(ex, "Lỗi hệ thống khi thêm kho mới");
                return StatusCode(500, "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.");
            }
        }
        [HttpPost("themloaikhomoi")]
        public async Task<IActionResult> ThemLoaiKhoMoi([FromBody] LoaiKhoModels newLoaiKho)
        {
            try
            {
                if (newLoaiKho == null)
                {
                    return BadRequest("Dữ liệu loại kho không hợp lệ.");
                }
                if (string.IsNullOrEmpty(newLoaiKho.TenLoaiKho))
                {
                    return BadRequest("Tên loại kho không được để trống.");
                }
                var loaiKhoEntity = new LoaiKho
                {
                    TenLoaiKho = newLoaiKho.TenLoaiKho,
                    GhiChu = newLoaiKho.GhiChu
                };
                _context.LoaiKhos.Add(loaiKhoEntity);
                await _context.SaveChangesAsync();
                // Hủy bỏ token cũ để xóa toàn bộ các cache liên quan đến danh sách loại xe
                _resetCacheToken.Cancel();
                _resetCacheToken.Dispose();
                // Khởi tạo token mới cho các lượt cache tiếp theo
                _resetCacheToken = new CancellationTokenSource();
                return Ok(new { Message = "Thêm loại kho mới thành công", MaLoaiKho = loaiKhoEntity.MaLoaiKho });
            }
            catch (SqlException ex)
            {
                // Lỗi kết nối Database
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi thêm loại kho mới");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
                // Các lỗi không xác định khác
                _logger.LogError(ex, "Lỗi hệ thống khi thêm loại kho mới");
                return StatusCode(500, "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.");
            }
        }

        [HttpGet("chitietkhobai/{maKho}")]
        public async Task<IActionResult> GetChiTietKhoBai([FromRoute] int maKho)
        {
            try
            {
                var khoBai = await _context.KhoBais

                    .AsNoTracking()
                    .Include(k => k.MaLoaiKhoNavigation)
                    .FirstOrDefaultAsync(k => k.MaKho == maKho);
                if (khoBai == null)
                {
                    return NotFound($"Không tìm thấy kho bãi với mã kho: {maKho}");
                }
                var khoBaiModel = new Models1.QuanLyKho.KhoBaiModels
                {
                    MaKho = khoBai.MaKho,
                    MaDiaChi = khoBai.MaDiaChi,
                    TenKhoBai = khoBai.TenKhoBai,
                    MaQuanLy = khoBai.MaQuanLy,
                    DungTichM3 = khoBai.DungTichM3,
                    DienTichM2 = khoBai.DienTichM2,
                    SoDienThoaiKho = khoBai.SoDienThoaiKho,
                    TrangThai = khoBai.TrangThai,
                    SucChua = khoBai.SucChua,
                    TenLoaiKho = khoBai.MaLoaiKhoNavigation != null ? khoBai.MaLoaiKhoNavigation.TenLoaiKho : null
                };
                return Ok(khoBaiModel);
            }
            catch (SqlException ex)
            {
                // Lỗi kết nối Database
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi lấy chi tiết kho bãi");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
                // Các lỗi không xác định khác
                _logger.LogError(ex, "Lỗi hệ thống khi lấy chi tiết kho bãi với mã kho: {MaKho}", maKho);
                return StatusCode(500, "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.");
            }
        }

        // lay danh sach ten kho bai (don vi lam viec)
        [HttpGet("gettenkhobai")]
        public async Task<IActionResult> GetTenKhoBai()
        {
            try
            {
                var tenKhoBaiList = await _context.KhoBais
                    .AsNoTracking()
                    .Select(k => new
                    {
                        k.TenKhoBai
                    })
                    .ToListAsync();
                if (tenKhoBaiList.Count == 0)
                {
                    return NotFound("Không tìm thấy kho bãi nào.");
                }
                return Ok(tenKhoBaiList);
            }
            catch (SqlException ex)
            {
                // Lỗi kết nối Database
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi lấy danh sách tên kho bãi");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
                // Các lỗi không xác định khác
                _logger.LogError(ex, "Lỗi hệ thống khi lấy danh sách tên kho bãi");
                return StatusCode(500, "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.");
            }
        }
    }
}
