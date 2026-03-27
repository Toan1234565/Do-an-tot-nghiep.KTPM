using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyKho.Models;
using QuanLyKho.Models1;
using QuanLyKho.Models1.QuanLyKho;
using QuanLyKho.Models1.QuanLyXe;
using System.Net.Http;
using Tmdt.Shared.Services;

namespace QuanLyKho.ControllersAPI
{
    [Route("api/quanlykhobai")]
    [ApiController]
    public class QuanLyKho : ControllerBase
    {
        public readonly TmdtContext _context;
        public readonly ILogger<QuanLyKho> _logger;
        public readonly IMemoryCache _cache;

        // Sử dụng lock để đảm bảo thread-safe khi thao tác với biến static
        private static readonly object _cacheLock = new object();
        private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISystemService _sys;

        public QuanLyKho(TmdtContext context, ILogger<QuanLyKho> logger, IMemoryCache cache, IHttpClientFactory httpClientFactory, ISystemService sys)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _sys = sys;
        }

        // Hàm hỗ trợ reset cache an toàn
        private void ResetCache()
        {
            lock (_cacheLock)
            {
                _resetCacheToken.Cancel();
                // Bỏ lệnh Dispose() ở đây để tránh lỗi ObjectDisposedException cho các request đang chạy
                _resetCacheToken = new CancellationTokenSource();
            }
        }
        [HttpGet("check-auth-info")]
        public IActionResult CheckAuthInfo()
        {
            return Ok(new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated,
                UserClaims = User.Claims.Select(c => new { c.Type, c.Value }),
                SessionData = HttpContext.Session.Keys.ToDictionary(k => k, k => HttpContext.Session.GetString(k)),
                CookiesReceived = Request.Cookies.Select(c => new { c.Key, c.Value })
            });
        }
        [HttpGet("getallkho")]
        public async Task<IActionResult> GetAllKhoBai([FromQuery] string? seach, [FromQuery] int page, [FromQuery] string? trangthai = "Hoạt động", [FromQuery] string? loaikho = "Tất cả")
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

                    var khoBaiList = await query
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

                    if (khoBaiList.Count == 0)
                    {
                        return NotFound("Không tìm thấy kho bãi nào.");
                    }

                    cachedData = new
                    {
                        TotalItems = query.Count(),
                        TotalPages = (int)Math.Ceiling((double)query.Count() / pageSize),
                        CurrentPage = page,
                        PageSize = pageSize,
                        Data = khoBaiList
                    };

                    // Lấy token hủy một cách an toàn
                    CancellationToken token;
                    lock (_cacheLock)
                    {
                        token = _resetCacheToken.Token;
                    }

                    // Lưu vào cache với thời gian hết hạn
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                        .AddExpirationToken(new CancellationChangeToken(token));

                    _cache.Set(cachekey, cachedData, cacheOptions);
                }
                return Ok(cachedData);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi lấy danh sách kho bãi ");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
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

                if (dsTen == null || dsTen.Count == 0)
                {
                    return NotFound("Khong tim thay");
                }
                return Ok(dsTen);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách tên kho");
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
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi lấy danh sách loại kho");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
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

                // Gọi hàm ResetCache thay vì code thủ công
                ResetCache();

                var data = new Dictionary<string, object>
                {
                    { "Mã kho", khoBaiEntity.MaKho },
                    { "Tên kho", khoBaiEntity.TenKhoBai },
                    { "Chi tiết kho", $"{khoBaiEntity.DungTichM3}m³ | {khoBaiEntity.DienTichM2}m² | Sức chứa: {khoBaiEntity.SucChua}" },
                    { "TrangThai", khoBaiEntity.TrangThai }
                };

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý kho bãi",
                    "Thêm mới kho bãi",
                    "QuanLyKho",
                    "",
                    new Dictionary<string, object>(),
                    data
                );

                return Ok(new { Message = "Thêm kho mới thành công", MaKho = khoBaiEntity.MaKho });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi thêm kho mới");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
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

                // Reset cache an toàn
                ResetCache();

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý kho bãi",
                    "Thêm mới loại kho",
                    "QuanLyKho",
                    "",
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>
                    {
                        { "Mã loại kho", loaiKhoEntity.MaLoaiKho },
                        { "Tên loại kho", loaiKhoEntity.TenLoaiKho },
                        { "Ghi chú", loaiKhoEntity.GhiChu ?? "" }
                    }
                );

                return Ok(new { Message = "Thêm loại kho mới thành công", MaLoaiKho = loaiKhoEntity.MaLoaiKho });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi thêm loại kho mới");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi thêm loại kho mới");
                return StatusCode(500, "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.");
            }
        }

        [HttpPut("cap-nhat-kho/{maKho}")]
        public async Task<IActionResult> CapNhatKhoBai(int maKho, [FromBody] KhoBaiModels updateModel)
        {
            try
            {
                var khoBaiEntity = await _context.KhoBais
                    .Include(k => k.MaLoaiKhoNavigation)
                    .FirstOrDefaultAsync(k => k.MaKho == maKho);

                if (khoBaiEntity == null) return NotFound($"Không tìm thấy kho bãi mã: {maKho}");

                // --- BƯỚC 1: SAO LƯU DỮ LIỆU CŨ ---
                var oldData = new Dictionary<string, object>
                {
                    { "Tên kho", khoBaiEntity.TenKhoBai ?? "" },
                    { "Số điện thoại", khoBaiEntity.SoDienThoaiKho ?? "" },
                    { "Trạng thái", khoBaiEntity.TrangThai ?? "" },
                    { "Mã quản lý", khoBaiEntity.MaQuanLy ?? 0 },
                    { "Loại kho", khoBaiEntity.MaLoaiKhoNavigation?.TenLoaiKho ?? "" },
                    { "Thông số kỹ thuật", $"{khoBaiEntity.DungTichM3}m³ | {khoBaiEntity.DienTichM2}m² | Sức chứa: {khoBaiEntity.SucChua}" }
                };

                // Lấy thông tin loại kho mới để ghi log chính xác (nếu có cập nhật loại kho)
                string newTenLoaiKho = oldData["Loại kho"].ToString() ?? "";
                if (khoBaiEntity.MaLoaiKho != updateModel.MaLoaiKho)
                {
                    var newLoaiKho = await _context.LoaiKhos.AsNoTracking().FirstOrDefaultAsync(lk => lk.MaLoaiKho == updateModel.MaLoaiKho);
                    newTenLoaiKho = newLoaiKho?.TenLoaiKho ?? "";
                }

                // --- BƯỚC 2: CẬP NHẬT DỮ LIỆU MỚI ---
                khoBaiEntity.TenKhoBai = updateModel.TenKhoBai;
                khoBaiEntity.MaDiaChi = updateModel.MaDiaChi;
                khoBaiEntity.MaQuanLy = updateModel.MaQuanLy;
                khoBaiEntity.DungTichM3 = updateModel.DungTichM3;
                khoBaiEntity.DienTichM2 = updateModel.DienTichM2;
                khoBaiEntity.TrangThai = updateModel.TrangThai;
                khoBaiEntity.SucChua = updateModel.SucChua;
                khoBaiEntity.SoDienThoaiKho = updateModel.SoDienThoaiKho;
                khoBaiEntity.MaLoaiKho = updateModel.MaLoaiKho;

                await _context.SaveChangesAsync();

                // --- BƯỚC 3: CHUẨN BỊ DỮ LIỆU MỚI ĐỂ SO SÁNH ---
                var newData = new Dictionary<string, object>
                {
                    { "Tên kho", updateModel.TenKhoBai ?? "" },
                    { "Số điện thoại", updateModel.SoDienThoaiKho ?? "" },
                    { "Trạng thái", updateModel.TrangThai ?? "" },
                    { "Mã quản lý", updateModel.MaQuanLy ?? 0 },
                    { "Loại kho", newTenLoaiKho },
                    { "Thông số kỹ thuật", $"{updateModel.DungTichM3}m³ | {updateModel.DienTichM2}m² | Sức chứa: {updateModel.SucChua}" }
                };

                // --- BƯỚC 4: LỌC SỰ KHÁC BIỆT ---
                var (diffCu, diffMoi) = LocThayDoi.GetChanges(oldData, newData);

                // --- BƯỚC 5: GHI LOG VÀ RESET CACHE ---
                if (diffMoi.Count > 0)
                {
                    // Reset cache an toàn
                    ResetCache();

                    await _sys.GhiLogVaResetCacheAsync(
                        "Quản lý kho bãi",
                        $"Cập nhật thay đổi kho ID: {maKho}",
                        "QuanLyKho",
                        "",
                        diffCu,
                        diffMoi
                    );
                }

                return Ok(new { Success = true, Message = "Cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi cập nhật kho {MaKho}", maKho);
                return StatusCode(500, "Lỗi hệ thống.");
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
                    MaLoaiKho = khoBai.MaLoaiKho,
                    TenLoaiKho = khoBai.MaLoaiKhoNavigation != null ? khoBai.MaLoaiKhoNavigation.TenLoaiKho : null
                };

                return Ok(khoBaiModel);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi lấy chi tiết kho bãi");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
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
                        k.TenKhoBai,
                        k.MaKho
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
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi lấy danh sách tên kho bãi");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi lấy danh sách tên kho bãi");
                return StatusCode(500, "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.");
            }
        }
        [HttpGet("tim-kho-gan-nhat/{maDiaChiLayHang}")]
        public async Task<IActionResult> TimKhoGanNhat(int maDiaChiLayHang)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                // BƯỚC 1: Lấy tọa độ khách hàng
                // BƯỚC 1: Lấy tọa độ của Khách hàng
                var urlKhach = $"https://localhost:7149/api/quanlydiachi/lay-toa-do/{maDiaChiLayHang}";
                var resKhach = await client.GetAsync(urlKhach);

                if (!resKhach.IsSuccessStatusCode)
                {
                    _logger.LogError($"Lỗi gọi API tọa độ khách: {resKhach.StatusCode} tại URL: {urlKhach}");
                    // Thay vì trả về BadRequest, bạn có thể gán kho mặc định (ví dụ MaKho = 1) để đơn hàng vẫn được tạo
                    return BadRequest($"Không lấy được tọa độ khách (Mã địa chỉ: {maDiaChiLayHang}). Vui lòng kiểm tra Server Khách hàng.");
                }

                var toaDoKhach = await resKhach.Content.ReadFromJsonAsync<ToaDoDto>();

                // Kiểm tra nếu tọa độ trả về bị rỗng
                if (toaDoKhach == null || toaDoKhach.ViDo == 0 || toaDoKhach.KinhDo == 0)
                {
                    return BadRequest("Tọa độ khách hàng trong cơ sở dữ liệu đang bị trống hoặc bằng 0.");
                }
                if (!resKhach.IsSuccessStatusCode) return BadRequest("Không lấy được tọa độ khách.");
               

                // BƯỚC 2: Lấy các kho đang hoạt động
                var khoBais = await _context.KhoBais
                    .Where(k => k.TrangThai == "Hoạt động")
                    .Select(k => new { k.MaKho, k.MaDiaChi, k.TenKhoBai })
                    .ToListAsync();

                if (!khoBais.Any()) return NotFound("Không có kho nào đang hoạt động.");

                var maDiaChiKhos = khoBais.Select(k => k.MaDiaChi).Distinct().ToList();

                // BƯỚC 3: Lấy tọa độ các kho (Đã sửa URL từ hhttps thành https)
                var resKhos = await client.PostAsJsonAsync("https://localhost:7149/api/quanlydiachi/lay-toa-do-danh-sach", maDiaChiKhos);
                if (!resKhos.IsSuccessStatusCode) return BadRequest("Không lấy được tọa độ các kho.");

                var danhSachToaDoKho = await resKhos.Content.ReadFromJsonAsync<List<ToaDoResponseDto>>();

                // BƯỚC 4: Tính toán và tìm kho gần nhất
                var ketQua = khoBais.Select(k => {
                    var toaDo = danhSachToaDoKho.FirstOrDefault(t => t.MaDiaChi == k.MaDiaChi);
                    return new
                    {
                        k.MaKho,
                        k.TenKhoBai,
                        KhoangCach = (toaDo != null && toaDo.ViDo.HasValue && toaDo.KinhDo.HasValue)
                            ? TinhKhoangCach(toaDoKhach.ViDo, toaDoKhach.KinhDo, (double)toaDo.ViDo.Value, (double)toaDo.KinhDo.Value)
                            : double.MaxValue
                    };
                })
                .OrderBy(k => k.KhoangCach)
                .FirstOrDefault(k => k.KhoangCach < double.MaxValue); // Đảm bảo không lấy kho lỗi tọa độ

                if (ketQua == null) return NotFound("Không tìm thấy kho nào có tọa độ hợp lệ.");

                return Ok(new
                {
                    maKho = ketQua.MaKho,
                    tenKho = ketQua.TenKhoBai,
                    distance = Math.Round(ketQua.KhoangCach, 2) + " km"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tìm kho gần nhất");
                return StatusCode(500, "Lỗi hệ thống: " + ex.Message);
            }
        }
        private double TinhKhoangCach(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // km
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
