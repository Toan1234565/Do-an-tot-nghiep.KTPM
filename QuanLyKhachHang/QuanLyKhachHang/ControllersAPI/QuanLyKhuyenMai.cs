using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NetTopologySuite.Index.HPRtree;
using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1;
using QuanLyKhachHang.Models1.QuanLyKhuyenMai;
using Tmdt.Shared.Services;

namespace QuanLyKhachHang.ControllersAPI
{
    [Route("api/quanlykhuyenmai")]
    [ApiController]
    public class QuanLyKhuyenMaiController : ControllerBase // Đổi tên để tránh trùng với namespace hoặc model
    {
        private readonly ILogger<QuanLyKhuyenMaiController> _logger;
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private const string PriceRegionCachePrefix = "PriceRegionList_";
        private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();
        private readonly ISystemService _sys;

        public QuanLyKhuyenMaiController(ILogger<QuanLyKhuyenMaiController> logger, TmdtContext context, IMemoryCache memoryCache, ISystemService sys)
        {
            _logger = logger;
            _context = context;
            _cache = memoryCache;
            _sys = sys;
        }

        [HttpGet("danhsachkhuyenmai")]
        public async Task<IActionResult> DanhSachKhuyenMai(
            [FromQuery] string? search,
            [FromQuery] DateTime? bd,
            [FromQuery] DateTime? kt,
            [FromQuery] int? loaikhuyenmai,
            [FromQuery] int page = 1,
            [FromQuery] bool? isActive = true)
        {
            try
            {
                if (page < 1) page = 1;
                int pageSize = 10;

                // Tạo cache key an toàn hơn (xử lý trường hợp null)
                var cacheKey = $"{PriceRegionCachePrefix}{search}_{bd?.Ticks}_{kt?.Ticks}_{loaikhuyenmai}_{page}_{isActive}";

                if (!_cache.TryGetValue(cacheKey, out List<KhuyenMaiModels>? danhSachKhuyenMai))
                {
                    var query = _context.KhuyenMais.AsNoTracking().AsQueryable();

                    // 1. Lọc theo tên
                    if (!string.IsNullOrEmpty(search))
                    {
                        query = query.Where(km => km.TenChuongTrinh.Contains(search));
                    }

                    
                    if (bd.HasValue && bd != DateTime.MinValue)
                    {
                        query = query.Where(km => km.NgayBatDau >= bd.Value);
                    }

                    // 3. Lọc theo ngày kết thúc
                    if (kt.HasValue && kt != DateTime.MinValue)
                    {
                        query = query.Where(km => km.NgayKetThuc <= kt.Value);
                    }

                    // 4. Lọc theo loại
                    if (loaikhuyenmai.HasValue && loaikhuyenmai.Value > 0)
                    {
                        // Đảm bảo MaLoaiKm trong Database là kiểu INT
                        query = query.Where(km => km.MaLoaiKm == loaikhuyenmai.Value);
                    }

                    // 5. Lọc theo trạng thái
                    if (isActive.HasValue)
                    {
                        query = query.Where(km => km.TrangThai == isActive.Value);
                    }

                    // Thực thi Query và Map sang Model
                    danhSachKhuyenMai = await query
                        .OrderByDescending(km => km.NgayBatDau) // Nên có OrderBy khi dùng Skip/Take
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(km => new KhuyenMaiModels
                        {
                            TenChuongTrinh = km.TenChuongTrinh,
                            MaKhuyenMai = km.MaKhuyenMai,
                            CodeKhuyenMai = km.CodeKhuyenMai,
                            MaLoaiKm = km.MaLoaiKm,
                            KieuGiamGia = km.KieuGiamGia,
                            GiaTriGiam = km.GiaTriGiam,
                            GiamToiDa = km.GiamToiDa,
                            NgayBatDau = km.NgayBatDau,
                            NgayKetThuc = km.NgayKetThuc,
                            SoLuongToiDa = km.SoLuongToiDa,
                            SoLuongDaDung = km.SoLuongDaDung,
                            DonHangToiThieu = km.DonHangToiThieu,
                            TrangThai = km.TrangThai
                        })
                        .ToListAsync(); // THIẾU TRONG CODE CŨ

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));

                    _cache.Set(cacheKey, danhSachKhuyenMai, cacheEntryOptions);
                }

                return Ok(danhSachKhuyenMai);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách khuyến mãi");
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpGet("danhsachloai")]
        public async Task<IActionResult> LayDanhSachLoaiKhuyenMai()
        {
            try
            {
                var danhSachLoai = await _context.LoaiKhuyenMais
                    .AsNoTracking()
                    .Select(ll => new LoaiKhuyenMaiModels
                    {
                        MaLoaiKm = ll.MaLoaiKm,
                        TenLoai = ll.TenLoai,
                        MoTa = ll.MoTa,
                        IconUrl = ll.IconUrl,
                        TrangThai = ll.TrangThai
                    })
                    .ToListAsync();
                return Ok(danhSachLoai);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách loại khuyến mãi");
                return StatusCode(500, "Internal server error");
            }
        }
        
        [HttpDelete("vo-hieu-hoa/{id}")]
        public async Task<IActionResult> VoHieuHoa(int id)
        {
            try
            {
                var item = await _context.KhuyenMais.FindAsync(id);
                if(item == null) return NotFound(new { message = "Không tìm thấy dữ liệu" });
                var datacu = new Dictionary<string, object>{
                    { "Tên khuyến mãi ", item.TenChuongTrinh},
                };
                item.TrangThai = false;                         
                await _context.SaveChangesAsync();

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý khuyến mãi",
                    "Vô hiệu hóa khuyến mãi",
                    "Khuyến mãi",
                    id.ToString(),
                    datacu,
                    new Dictionary<string, object>
                    {
                        { "Trạng thái", "Ngừng hoạt động" }
                    }

                );

                ClearPriceRegionCache();
                return Ok(new { success = true, message = "Đã ngừng áp dụng bảng giá này." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi vô hiệu hóa");
                return StatusCode(500, "Lỗi hệ thống");
            }
        }
        [HttpPost("kich-hoat/{id}")]
        public async Task<IActionResult> KichHoat(int id, [FromBody] KhuyenMaiModels km)
        {
            try
            {
                // 1. Tìm đối tượng trong DB
                var item = await _context.KhuyenMais.FindAsync(id);
                if (item == null)
                    return NotFound(new { success = false, message = "Không tìm thấy chương trình khuyến mãi" });
                var datacu = new Dictionary<string, object>
                {
                    {"Tên khuyến mãi ", item.TenChuongTrinh},
                    {"Thời gian", item.NgayBatDau - item.NgayKetThuc}
                };
                // 2. Nếu đã hết hạn, yêu cầu cập nhật thời gian mới từ Body
                if (item.NgayKetThuc < DateTime.Now)
                {
                    // Kiểm tra tính hợp lệ của ngày mới
                    if (km.NgayBatDau >= km.NgayKetThuc)
                    {
                        return BadRequest(new { success = false, message = "Ngày kết thúc phải lớn hơn ngày bắt đầu" });
                    }

                    item.NgayBatDau = km.NgayBatDau;
                    item.NgayKetThuc = km.NgayKetThuc;
                }

                // 3. Cập nhật trạng thái kích hoạt
                item.TrangThai = true;

                // Lưu thay đổi
                await _context.SaveChangesAsync();

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý khuyến mãi",
                    "Kích hoạt lại khuyến mãi",
                    "Khuyến mãi",
                    "",
                    datacu,
                    new Dictionary<string, object>
                    {
                        { "Trạng thái", "Ngưng hoạt động"}
                    }
                );

                // 4. Xóa Cache để đồng bộ dữ liệu mới nhất
                ClearPriceRegionCache();

                return Ok(new { success = true, message = "Kích hoạt chương trình khuyến mãi thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi kích hoạt khuyến mãi ID: {id}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xử lý dữ liệu" });
            }
        }

        [HttpPost("taomoikhuyenmai")]
        public async Task<IActionResult> TaoMoi([FromBody] KhuyenMaiModels model)
        {
            if (!ModelState.IsValid) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            // 1. Dùng hàm validate chung
            var error = ValidateKhuyenMai(model);
            if (error != null) return BadRequest(new { success = false, message = error });

            try
            {
                // 2. Check trùng Code nhanh bằng AnyAsync
                if (await _context.KhuyenMais.AnyAsync(x => x.CodeKhuyenMai == model.CodeKhuyenMai))
                    return BadRequest(new { success = false, message = "Mã khuyến mãi này đã tồn tại." });

                // 3. Sử dụng tính năng Map tự động của EF (nếu cùng kiểu dữ liệu)
                var newEntry = new KhuyenMai();
                _context.Entry(newEntry).CurrentValues.SetValues(model);

                newEntry.SoLuongDaDung = 0;
                newEntry.TrangThai = true;

                _context.KhuyenMais.Add(newEntry);
                await _context.SaveChangesAsync();

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý khuyến mãi",
                    "Thêm khuyến mãi",
                    "Khuyến mãi",
                    "",
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>
                    {
                        { "Mã Code", model.CodeKhuyenMai },
                        { "Tên chương trình", model.TenChuongTrinh },
                        { "Loại KM", model.MaLoaiKmNavigation?.TenLoai ?? "N/A" },
                        { "Hình thức", model.KieuGiamGia == "Percentage" ? "Giảm theo %" : "Giảm tiền mặt" },
                        { "Giá trị giảm", model.GiaTriGiam + (model.KieuGiamGia == "Percentage" ? "%" : " VNĐ") },
                        { "Giảm tối đa", model.GiamToiDa?.ToString("N0") + " VNĐ" },
                        { "Đơn tối thiểu", model.DonHangToiThieu?.ToString("N0") + " VNĐ" },
                        { "Số lượng", model.SoLuongToiDa ?? 0 },
                        { "Thời gian", $"{model.NgayBatDau?.ToString("dd/MM/yyyy")} - {model.NgayKetThuc?.ToString("dd/MM/yyyy")}" }
                    }
                );

                ClearPriceRegionCache();
                return Ok(new { success = true, message = "Tạo mới thành công!", id = newEntry.MaKhuyenMai });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tạo mới KM");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        [HttpPost("capnhatkhuyenmai/{id}")]
        public async Task<IActionResult> CapNhat(int id, [FromBody] KhuyenMaiModels km)
        {
            if (!ModelState.IsValid) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            var error = ValidateKhuyenMai(km);
            if (error != null) return BadRequest(new { success = false, message = error });

            try
            {
                // 1. Lấy dữ liệu cũ để so sánh hoặc lấy thông tin Navigation (như TenLoai)
                var existing = await _context.KhuyenMais
                    .Include(x => x.MaLoaiKmNavigation)
                    .FirstOrDefaultAsync(x => x.MaKhuyenMai == id);

                if (existing == null) return NotFound(new { success = false, message = "Không tìm thấy chương trình." });

                // Lưu lại dữ liệu cũ trước khi ghi đè để log (nếu cần so sánh)
                // 1. Lưu lại dữ liệu cũ (Đầy đủ các trường để đối chiếu)
                var dataCu = new Dictionary<string, object>
                {
                    { "Tên chương trình", existing.TenChuongTrinh },
                    { "Số lượng", existing.SoLuongToiDa ?? 0 },
                    { "Giá trị giảm", existing.GiaTriGiam },
                    { "Giảm tối đa", existing.GiamToiDa ?? 0 },
                    { "Đơn tối thiểu", existing.DonHangToiThieu ?? 0 },
                    { "Loại KM", existing.MaLoaiKmNavigation?.TenLoai ?? "N/A" },
                    { "Thời gian", $"{existing.NgayBatDau?.ToString("dd/MM/yyyy")} - {existing.NgayKetThuc?.ToString("dd/MM/yyyy")}" }
                };

                // 2. Kiểm tra trùng mã code
                if (await _context.KhuyenMais.AnyAsync(x => x.CodeKhuyenMai == km.CodeKhuyenMai && x.MaKhuyenMai != id))
                    return BadRequest(new { success = false, message = "Mã code đã được sử dụng bởi chương trình khác." });

                // 3. Cập nhật dữ liệu
                _context.Entry(existing).CurrentValues.SetValues(km);
                existing.TrangThai = true;
                existing.MaKhuyenMai = id;

                await _context.SaveChangesAsync();

                // 1. Tạo dictionary chứa toàn bộ dữ liệu mới (để so sánh)
                var dataMoiToanBo = new Dictionary<string, object>
                {
                    { "Tên chương trình", existing.TenChuongTrinh },
                    { "Số lượng", existing.SoLuongToiDa ?? 0 },
                    { "Giá trị giảm", existing.GiaTriGiam },
                    { "Giảm tối đa", existing.GiamToiDa ?? 0 },
                    { "Đơn tối thiểu", existing.DonHangToiThieu ?? 0 },
                    { "Loại KM", existing.MaLoaiKmNavigation?.TenLoai ?? "N/A" },
                    { "Thời gian", $"{existing.NgayBatDau?.ToString("dd/MM/yyyy")} - {existing.NgayKetThuc?.ToString("dd/MM/yyyy")}" }
                };

                // 2. Lọc ra chỉ những trường bị thay đổi
                var (diffCu, diffMoi) = LocThayDoi.GetChanges(dataCu, dataMoiToanBo);

                // 3. Nếu có thay đổi thì mới ghi log (hoặc ghi log với dữ liệu đã lọc)
                if (diffMoi.Count > 0)
                {
                    // Thêm tên chương trình vào để dễ nhận diện trong danh sách log tổng
                    if (!diffMoi.ContainsKey("Tên chương trình"))
                        diffMoi.Add("Tên chương trình (Update)", existing.TenChuongTrinh);

                    await _sys.GhiLogVaResetCacheAsync(
                        "Quản lý khuyến mãi",
                        "Cập nhật khuyến mãi",
                        "Khuyến mãi",
                        id.ToString(),
                        diffCu,  // Chỉ chứa các giá trị cũ của trường bị sửa
                        diffMoi  // Chỉ chứa các giá trị mới của trường bị sửa
                    );
                }

                return Ok(new { success = true, message = "Cập nhật thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        private string? ValidateKhuyenMai(KhuyenMaiModels model)
        {
            if (model.NgayBatDau >= model.NgayKetThuc)
                return "Ngày bắt đầu phải trước ngày kết thúc.";

            if (model.GiamToiDa == null)
                return "Giá trị giảm tối đa không được để trống.";

            if (model.KieuGiamGia == "Phần trăm" && model.GiaTriGiam > 100)
                model.GiaTriGiam = 100;

            if (model.GiaTriGiam < 0)
                model.GiaTriGiam = 0;

            if(model.SoLuongToiDa == null || model.SoLuongToiDa < 0)
                return "Số lượng tối đa phải là số nguyên dương.";
            if(model.GiamToiDa < model.GiaTriGiam)
                return "Giá trị giảm tối đa phải lớn hơn hoặc bằng giá trị giảm.";
            return null; // Không có lỗi
        }
        private void ClearPriceRegionCache()
        {
            // Hủy token cũ -> Tất cả cache gắn với token này sẽ tự động bị xóa
            if (!_resetCacheToken.IsCancellationRequested)
            {
                _resetCacheToken.Cancel();
                _resetCacheToken.Dispose();
            }
            // Tạo token mới cho các lượt cache tiếp theo
            _resetCacheToken = new CancellationTokenSource();
            _logger.LogInformation("Đã làm mới toàn bộ Cache Bảng giá vùng.");
        }

        [HttpGet("khuyen-mai-kha-dung")]
        public async Task<IActionResult> GetAvailablePromotions([FromQuery] decimal tongTienDonHang, [FromQuery] int maKhachHang)
        {
            try
            {
                // Log để kiểm tra dữ liệu thực tế nhận được từ Frontend
                Console.WriteLine($"🔍 Checking Promo: Money={tongTienDonHang}, CustomerID={maKhachHang}");

                var now = DateTime.Now;

                // 1. Lấy lịch sử sử dụng mã của khách hàng này trước
                var lichSuDungIds = await _context.LichSuDungMas
                    .Where(ls => ls.MaKhachHang == maKhachHang)
                    .Select(ls => ls.MaKhuyenMai)
                    .ToListAsync();

                // 2. Query danh sách khuyến mãi đang hoạt động (Lọc các điều kiện cứng tại Database)
                var danhSachKm = await _context.KhuyenMais
                    .AsNoTracking()
                    .Where(km => km.TrangThai == true &&
                                 km.NgayBatDau <= now &&
                                 km.NgayKetThuc >= now)
                    .ToListAsync();

                // 3. Lọc logic mềm tại Memory (tránh lỗi ép kiểu SQL hoặc so sánh null phức tạp)
                var result = danhSachKm
                    .Where(km =>
                        // Điều kiện số lượng
                        (km.SoLuongToiDa == null || km.SoLuongDaDung < km.SoLuongToiDa) &&
                        // Điều kiện giá trị đơn hàng tối thiểu
                        (km.DonHangToiThieu == null || tongTienDonHang >= km.DonHangToiThieu) &&
                        // Điều kiện chưa từng sử dụng (nếu mỗi người chỉ dùng 1 lần)
                        !lichSuDungIds.Contains(km.MaKhuyenMai)
                    )
                    .Select(km => new
                    {
                        km.MaKhuyenMai,
                        CodeKhuyenMai = km.CodeKhuyenMai, // Đảm bảo đúng tên để FE nhận diện
                        km.TenChuongTrinh,
                        km.KieuGiamGia,
                        km.GiaTriGiam,
                        km.GiamToiDa,
                        km.DonHangToiThieu,
                        MoTa = km.KieuGiamGia == "Phần trăm"
                            ? $"Giảm {km.GiaTriGiam}% (Tối đa {km.GiamToiDa:N0}đ)"
                            : $"Giảm trực tiếp {km.GiaTriGiam:N0}đ",
                        DaSuDung = false // Vì đã lọc !lichSuDungIds ở trên nên tất cả trả về đều là chưa dùng
                    })
                    .ToList();

                Console.WriteLine($"✅ Found {result.Count} valid promotions.");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy danh sách khuyến mãi khả dụng");
                return StatusCode(500, new { message = "Lỗi hệ thống khi tải khuyến mãi" });
            }
        }
        [HttpPost("ap-dung")]
        public async Task<IActionResult> ApplyPromotion([FromBody] ApplyPromotionRequest request)
        {
            try
            {
                var now = DateTime.Now;

                // 1. Tìm mã khuyến mãi (Include luôn lịch sử nếu cần check số lần dùng của User)
                var promotion = await _context.KhuyenMais
                    .FirstOrDefaultAsync(km => km.CodeKhuyenMai == request.Code && km.TrangThai == true);

                if (promotion == null)
                    return NotFound(new { success = false, message = "Mã giảm giá không tồn tại hoặc đã bị vô hiệu hóa." });

                // 2. Kiểm tra thời gian hiệu lực
                if (promotion.NgayBatDau > now)
                    return BadRequest(new { success = false, message = "Chương trình khuyến mãi chưa bắt đầu." });

                if (promotion.NgayKetThuc < now)
                    return BadRequest(new { success = false, message = "Mã giảm giá đã hết hạn sử dụng." });

                // 3. Kiểm tra tổng lượt dùng của hệ thống
                if (promotion.SoLuongToiDa.HasValue && promotion.SoLuongDaDung >= promotion.SoLuongToiDa)
                    return BadRequest(new { success = false, message = "Mã giảm giá đã hết lượt sử dụng trên hệ thống." });

                // 4. Kiểm tra điều kiện đơn hàng tối thiểu
                if (promotion.DonHangToiThieu.HasValue && request.TongTienDonHang < promotion.DonHangToiThieu)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Đơn hàng tối thiểu phải từ {promotion.DonHangToiThieu:N0}đ để sử dụng mã này."
                    });
                }

                // 5. Kiểm tra giới hạn: Mỗi khách hàng chỉ được dùng 1 lần (Dựa trên LichSuDungMa)
                var daDung = await _context.LichSuDungMas.AnyAsync(ls =>
                    ls.MaKhachHang == request.MaKhachHang &&
                    ls.MaKhuyenMai == promotion.MaKhuyenMai);

                if (daDung)
                    return BadRequest(new { success = false, message = "Bạn đã sử dụng mã này cho một đơn hàng khác rồi." });

                // 6. Tính toán số tiền giảm
                decimal soTienGiam = 0;
                if (promotion.KieuGiamGia == "Phần trăm")
                {
                    // Tính % giảm
                    soTienGiam = request.TongTienDonHang * (promotion.GiaTriGiam / 100m);

                    // Nếu có giá trị giảm tối đa (GiamToiDa), thì không được vượt quá số đó
                    if (promotion.GiamToiDa.HasValue && soTienGiam > promotion.GiamToiDa.Value)
                    {
                        soTienGiam = promotion.GiamToiDa.Value;
                    }
                }
                else // Giảm theo số tiền cố định
                {
                    soTienGiam = promotion.GiaTriGiam;
                }

                // Không cho phép số tiền giảm lớn hơn tổng giá trị đơn hàng
                if (soTienGiam > request.TongTienDonHang)
                    soTienGiam = request.TongTienDonHang;

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        MaKhuyenMai = promotion.MaKhuyenMai,
                        Code = promotion.CodeKhuyenMai,
                        TenChuongTrinh = promotion.TenChuongTrinh,
                        SoTienGiam = soTienGiam,
                        TongTienSauGiam = request.TongTienDonHang - soTienGiam
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi áp dụng khuyến mãi");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xử lý mã giảm giá" });
            }
        }
        [HttpGet("validate-voucher/{code}")]
        public async Task<IActionResult> ValidateVoucher(string code)
        {
            // Giả sử bạn có bảng KhuyenMai
            var voucher = await _context.KhuyenMais
                .FirstOrDefaultAsync(k => k.CodeKhuyenMai == code && k.TrangThai == true && k.NgayKetThuc > DateTime.Now);

            if (voucher == null) return NotFound("Mã giảm giá không tồn tại hoặc hết hạn.");

            return Ok(new
            {
                MaGiamGia = voucher.CodeKhuyenMai,
                GiaTriGiam = voucher.GiaTriGiam,
                LoaiGiam = voucher.MaLoaiKmNavigation.MoTa // 1: % , 2: Tiền mặt
            });
        }
        // Request DTO (Data Transfer Object)
        public class ApplyPromotionRequest
        {
            public string Code { get; set; } = null!;
            public decimal TongTienDonHang { get; set; }
            public int MaKhachHang { get; set; }
        }


    }
}