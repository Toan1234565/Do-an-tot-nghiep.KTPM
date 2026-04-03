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
        [HttpPost("tim-kho-theo-lo")]
        public async Task<IActionResult> TimKhoTheoLo([FromBody] BatchKhoRequest request)
        {
            if (request?.MaDiaChis == null || !request.MaDiaChis.Any())
                return BadRequest("Danh sách mã địa chỉ trống.");

            try
            {
                var client = _httpClientFactory.CreateClient();

                // 1. Lấy thông tin tọa độ và mã H3 của DANH SÁCH địa chỉ khách hàng
                var resToaDo = await client.PostAsJsonAsync("https://localhost:7149/api/quanlydiachi/lay-toa-do-danh-sach", request.MaDiaChis);
                if (!resToaDo.IsSuccessStatusCode) return BadRequest("Không thể lấy dữ liệu tọa độ từ Service Địa Chỉ.");

                var danhSachToaDoKhach = await resToaDo.Content.ReadFromJsonAsync<List<ToaDoResponseDto>>();
                if (danhSachToaDoKhach == null) return NotFound("Dữ liệu tọa độ trống.");

                // 2. Lấy danh sách kho đang hoạt động
                var khoBais = await _context.KhoBais
                    .AsNoTracking()
                    .Where(k => k.TrangThai == "Hoạt động")
                    .ToListAsync();

                // 3. Lấy tọa độ của TẤT CẢ các kho để tính khoảng cách Haversine
                var maDiaChiKhos = khoBais.Select(k => k.MaDiaChi).Distinct().ToList();
                var resToaDoKho = await client.PostAsJsonAsync("https://localhost:7149/api/quanlydiachi/lay-toa-do-danh-sach", maDiaChiKhos);
                var danhSachToaDoKho = await resToaDoKho.Content.ReadFromJsonAsync<List<ToaDoResponseDto>>();

                var ketQua = new Dictionary<int, object>();

                foreach (var dcKhach in danhSachToaDoKhach)
                {
                    // CHIẾN THUẬT 1: Tìm kho cùng vùng H3 (Ưu tiên số 1)
                    var khoCungVung = khoBais.FirstOrDefault(k => !string.IsNullOrEmpty(k.MaVungH3) && k.MaVungH3 == dcKhach.MaVungH3);

                    if (khoCungVung != null)
                    {
                        ketQua[dcKhach.MaDiaChi] = new
                        {
                            maKho = khoCungVung.MaKho,
                            tenKho = khoCungVung.TenKhoBai,
                            maDiaChi = khoCungVung.MaDiaChi,
                            maVungH3 = khoCungVung.MaVungH3,
                            distance = "0 km (Cùng vùng H3)"
                        };
                    }
                    else
                    {
                        // CHIẾN THUẬT 2: Tìm kho gần nhất bằng công thức Haversine (Dự phòng)
                        var khoGanNhatObj = khoBais
                            .Select(k =>
                            {
                                var toaDoK = danhSachToaDoKho?.FirstOrDefault(t => t.MaDiaChi == k.MaDiaChi);
                                double khoangCach = double.MaxValue;

                                if (toaDoK != null && dcKhach.ViDo.HasValue && dcKhach.KinhDo.HasValue && toaDoK.ViDo.HasValue && toaDoK.KinhDo.HasValue)
                                {
                                    khoangCach = TinhKhoangCach(
                                        dcKhach.ViDo.Value,
                                        dcKhach.KinhDo.Value,
                                        toaDoK.ViDo.Value,
                                        toaDoK.KinhDo.Value
                                    );
                                }
                                return new { Kho = k, Distance = khoangCach };
                            })
                            .OrderBy(x => x.Distance)
                            .FirstOrDefault();

                        if (khoGanNhatObj != null && khoGanNhatObj.Distance < double.MaxValue)
                        {
                            // Lấy thực thể Kho từ kết quả sắp xếp
                            var khoThucTe = khoGanNhatObj.Kho;

                            ketQua[dcKhach.MaDiaChi] = new
                            {
                                maKho = khoThucTe.MaKho,
                                tenKho = khoThucTe.TenKhoBai,
                                maDiaChi = khoThucTe.MaDiaChi, // Đã sửa: Không dùng biến null khoCungVung
                                maVungH3 = khoThucTe.MaVungH3,
                                distance = Math.Round(khoGanNhatObj.Distance, 2) + " km"
                            };
                        }
                    }
                }

                return Ok(ketQua);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi Batch tìm kho");
                return StatusCode(500, "Lỗi hệ thống: " + ex.Message);
            }
        }

        [HttpGet("tim-kho-gan-nhat")]
        public async Task<IActionResult> TimKhoGanNhat([FromQuery] int maDiaChi)
        {
            if (maDiaChi <= 0) return BadRequest("Mã địa chỉ không hợp lệ.");

            try
            {
                var client = _httpClientFactory.CreateClient();

                // 1. Gọi sang Service Địa Chỉ để lấy tọa độ của địa chỉ đích (đơn hàng)
                var resToaDo = await client.GetAsync($"https://localhost:7149/api/quanlydiachi/lay-toa-do/{maDiaChi}");
                if (!resToaDo.IsSuccessStatusCode) return NotFound("Không tìm thấy tọa độ địa chỉ yêu cầu.");

                var toaDoDich = await resToaDo.Content.ReadFromJsonAsync<ToaDoResponseDto>();
                if (toaDoDich == null) return NotFound("Dữ liệu tọa độ trống.");

                // 2. Lấy danh sách tất cả kho đang hoạt động
                var khoBais = await _context.KhoBais
                    .AsNoTracking()
                    .Where(k => k.TrangThai == "Hoạt động")
                    .ToListAsync();

                // 3. CHIẾN THUẬT 1: Tìm kho cùng vùng H3 (Nhanh nhất)
                var khoCungVung = khoBais.FirstOrDefault(k => !string.IsNullOrEmpty(k.MaVungH3) && k.MaVungH3 == toaDoDich.MaVungH3);
                if (khoCungVung != null)
                {
                    return Ok(new
                    {
                        MaKho = khoCungVung.MaKho,
                        TenKho = khoCungVung.TenKhoBai,
                        Distance = 0,
                        Note = "Cùng vùng H3"
                    });
                }

                // 4. CHIẾN THUẬT 2: Tính khoảng cách Haversine để tìm kho vật lý gần nhất
                // Lấy tọa độ của tất cả các kho
                var maDiaChiKhos = khoBais.Select(k => k.MaDiaChi).Distinct().ToList();
                var resToaDoKhos = await client.PostAsJsonAsync("https://localhost:7149/api/quanlydiachi/lay-toa-do-danh-sach", maDiaChiKhos);
                var danhSachToaDoKho = await resToaDoKhos.Content.ReadFromJsonAsync<List<ToaDoResponseDto>>();

                var khoGanNhat = khoBais
                    .Select(k =>
                    {
                        var tdK = danhSachToaDoKho?.FirstOrDefault(t => t.MaDiaChi == k.MaDiaChi);
                        double distance = double.MaxValue;
                        if (tdK != null && toaDoDich.ViDo.HasValue && toaDoDich.KinhDo.HasValue && tdK.ViDo.HasValue && tdK.KinhDo.HasValue)
                        {
                            distance = TinhKhoangCach(toaDoDich.ViDo.Value, toaDoDich.KinhDo.Value, tdK.ViDo.Value, tdK.KinhDo.Value);
                        }
                        return new { Kho = k, Distance = distance };
                    })
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();

                if (khoGanNhat != null && khoGanNhat.Distance < double.MaxValue)
                {
                    // Tìm dòng này trong Controller QuanLyKho của KhoApi:
                    return Ok(new
                    {
                        MaKho = khoGanNhat.Kho.MaKho,
                        TenKho = khoGanNhat.Kho.TenKhoBai,
                        Distance = Math.Round(khoGanNhat.Distance, 2).ToString(), // Thêm .ToString() ở đây
                        Note = "Tính theo khoảng cách vật lý"
                    });


                }

                return NotFound("Không tìm thấy kho phù hợp.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tìm kho gần nhất cho địa chỉ {MaDiaChi}", maDiaChi);
                return StatusCode(500, "Lỗi hệ thống khi tìm kho.");
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

        [HttpGet("MaDiaChiKho/{maKho}")]
        public async Task<IActionResult> GetMaDiaChiKho([FromRoute] int maKho)
        {
            try
            {
                var khoBai = await _context.KhoBais
                    .AsNoTracking()
                    .FirstOrDefaultAsync(k => k.MaKho == maKho);
                if (khoBai == null)
                {
                    return NotFound($"Không tìm thấy kho bãi với mã kho: {maKho}");
                }
                return Ok(new { MaDiaChi = khoBai.MaDiaChi });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi lấy mã địa chỉ của kho bãi");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi lấy mã địa chỉ của kho bãi với mã kho: {MaKho}", maKho);
                return StatusCode(500, "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.");
            }
        }
    }
}
