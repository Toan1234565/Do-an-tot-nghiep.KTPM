using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyKhachHang;
using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1.QuanLyBangGiaVung;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNhatKyHeThong;
using System.Data;
using Tmdt.Shared.Services;

[Route("api/quanlybangiavung")]
[ApiController]
public class QuanLyBangGiaVung : ControllerBase
{
    private readonly ILogger<QuanLyBangGiaVung> _logger;
    private readonly TmdtContext _context;
    private readonly IMemoryCache _cache;
    private readonly ISystemService _sys;
    // Key gốc để quản lý phụ thuộc cache
    private const string PriceRegionCachePrefix = "PriceRegionList_";
    private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();

    public QuanLyBangGiaVung(ILogger<QuanLyBangGiaVung> logger, TmdtContext context, IMemoryCache cache, ISystemService sys)
    {
        _logger = logger;
        _context = context;
        _cache = cache;
        _sys = sys;
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
    [HttpGet("who-am-i")]
    public IActionResult WhoAmI()
    {
        var userId = _sys.GetCurrentUserId();
        var hoTen = HttpContext.Session.GetString("HoTenNhanVien");
        return Ok(new { UserId = userId, Name = hoTen, IsAuth = User.Identity.IsAuthenticated });
    }

    [HttpGet("dsbanggia")]
    public async Task<IActionResult> LayDanhSach(
        [FromQuery] string? searchTerm,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? khuvuclay = "Tất cả",
        [FromQuery] string? khuvucgiao = "Tất cả",
        [FromQuery] string? loaitinhgia = "Tất cả",
        [FromQuery] bool? isActive = true)
    {
        // 1. Tạo Cache Key duy nhất cho bộ tham số lọc
        var cacheKey = $"{PriceRegionCachePrefix}_P{page}_{pageSize}_S{searchTerm}_KL{khuvuclay}_KG{khuvucgiao}_L{loaitinhgia}_A{isActive}";

        if (!_cache.TryGetValue(cacheKey, out object? result))
        {
            var query = _context.BangGiaVungs.AsNoTracking();

            // 2. Logic lọc (Filtering)
            if (isActive.HasValue)
            {
                query = query.Where(bg => bg.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                query = query.Where(bg => bg.KhuVucLay.Contains(searchTerm) || bg.KhuVucGiao.Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(loaitinhgia) && loaitinhgia != "Tất cả")
            {
                if (loaitinhgia == "Theo vùng") query = query.Where(bg => bg.LoaiTinhGia == 1);
                else if (loaitinhgia == "Theo km") query = query.Where(bg => bg.LoaiTinhGia == 2);
            }

            if (khuvuclay != "Tất cả") query = query.Where(bg => bg.KhuVucLay == khuvuclay);
            if (khuvucgiao != "Tất cả") query = query.Where(bg => bg.KhuVucGiao == khuvucgiao);

            // 3. Tính toán phân trang (Thực thi CountAsync trước khi Skip/Take)
            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // 4. Lấy dữ liệu trang hiện tại
            var data = await query
                .OrderByDescending(x => x.NgayCapNhat)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(bg => new BangGiaVungModels
                {
                    MaBangGia = bg.MaBangGia,
                    KhuVucLay = bg.KhuVucLay,
                    KhuVucGiao = bg.KhuVucGiao,
                    TrongLuongToiThieuKg = bg.TrongLuongToiThieuKg,
                    TrongLuongToiDaKg = bg.TrongLuongToiDaKg,
                    DonGiaCoBan = bg.DonGiaCoBan,
                    PhuPhiMoiKg = bg.PhuPhiMoiKg,
                    IsActive = bg.IsActive,
                    NgayCapNhat = bg.NgayCapNhat,
                    LoaiTinhGia = bg.LoaiTinhGia,
                    DonGiaKm = bg.DonGiaKm,
                    PhiDungDiem = bg.PhiDungDiem,
                    KmToiThieu = bg.KmToiThieu,
                    MaLoaiHang = bg.MaLoaiHang
                })
                .ToListAsync();

            // 5. Đóng gói kết quả trả về
            result = new
            {
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = page,
                PageSize = pageSize,
                Items = data
            };

            // 6. Lưu vào Cache
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                .AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));

            _cache.Set(cacheKey, result, cacheOptions);
        }

        return Ok(result);
    }
    // HÀM DÙNG CHUNG ĐỂ LÀM MỚI CACHE
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

    [Authorize]
    [HttpPost("themmoibanggia")]
    public async Task<IActionResult> ThemMoi([FromBody] BangGiaVung model)
    {
        // 1. Validation logic theo loại hình vận chuyển
        if (model.LoaiTinhGia == 2) // Hình thức tính theo Km (Hàng tấn)
        {
            if (model.DonGiaKm <= 0)
                return BadRequest(new { message = "Vận chuyển theo Km bắt buộc phải có đơn giá mỗi Km lớn hơn 0." });
            if (model.KmToiThieu == null)
            {
                return BadRequest(new { message = "Vận chuyển theo km bắt buộc có km tối thiểu." });
            }

        }
        else if (model.LoaiTinhGia == 1) // Hình thức tính theo Vùng (vd Kiện 5kg)
        {
            if (model.TrongLuongToiDaKg <= 0)
                return BadRequest(new { message = "Vận chuyển theo Vùng bắt buộc phải có mức trọng lượng tối đa." });

            if (model.DonGiaCoBan <= 0)
                return BadRequest(new { message = "Vận chuyển theo Vùng bắt buộc phải có đơn giá cơ bản." });
        }
        else
        {
            return BadRequest(new { message = "Loại hình tính giá không hợp lệ." });
        }

        // 2. Kiểm tra trùng lặp (Bao gồm thêm MaLoaiXe để tránh trùng giá giữa các loại xe khác nhau)
        bool isExist = _context.BangGiaVungs.Any(bg =>
            bg.IsActive == true &&
            bg.KhuVucLay == model.KhuVucLay &&
            bg.KhuVucGiao == model.KhuVucGiao &&
            bg.LoaiTinhGia == model.LoaiTinhGia &&
            bg.TrongLuongToiThieuKg == model.TrongLuongToiThieuKg &&
            bg.TrongLuongToiDaKg == model.TrongLuongToiDaKg &&
            bg.MaLoaiHang == model.MaLoaiHang &&
            bg.DonGiaKm == model.DonGiaKm &&
            bg.KmToiThieu == model.KmToiThieu &&
            bg.DonGiaCoBan == model.DonGiaCoBan &&
            bg.PhuPhiMoiKg == model.PhuPhiMoiKg &&
            bg.PhiDungDiem == model.PhiDungDiem
        );

        if (isExist)
        {
            return BadRequest(new { message = "Bảng giá cho khu vực, loại xe và mức trọng lượng này đã tồn tại và đang hoạt động." });
        }

        try
        {
            // Gán các giá trị mặc định nếu cần
            model.NgayCapNhat = DateTime.Now;
            model.IsActive = true;

            _context.BangGiaVungs.Add(model);
            await _context.SaveChangesAsync();

            await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý bảng giá vùng",
                    $"Thêm mới bảng giá vùng mã: {model.MaBangGia}",
                    "BangGiaVung",
                    model.MaBangGia.ToString(),
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>
                    {
                        { "Tuyến đường", $"{model.KhuVucLay} ➔ {model.KhuVucGiao}" },
                        { "Cách tính giá", model.LoaiTinhGia == 1 ? "Tính theo vùng" : "Tính theo số Km" },
                        { "Khung trọng lượng", $"{model.TrongLuongToiThieuKg}kg - {model.TrongLuongToiDaKg}kg" },
                        { "Đơn giá cơ bản", model.DonGiaCoBan?.ToString("N0") + " VNĐ" },
                        { "Phụ phí vượt cân", model.PhuPhiMoiKg?.ToString("N0") + " VNĐ/kg" },
                        { "Đơn giá/Km", model.DonGiaKm?.ToString("N0") + " VNĐ" },
                        { "Phí dừng điểm", model.PhiDungDiem?.ToString("N0") + " VNĐ" },
                        { "Km tối thiểu", model.KmToiThieu + " Km" },
                        { "Loại hàng hóa", model.MaLoaiHang }
                    }
                );

            ClearPriceRegionCache();
            return Ok(new { message = "Thêm bảng giá thành công", data = model });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thêm mới bảng giá ID: {MaBangGia}", model.MaBangGia);
            return StatusCode(500, new { message = "Lỗi máy chủ khi lưu dữ liệu", detail = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("kich-hoat-lai/{id}")]
    public async Task<IActionResult> KichHoatLai(int id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Tìm bảng giá cũ muốn mở lại
            var item = await _context.BangGiaVungs.FindAsync(id);
            if (item == null)
                return NotFound(new { message = "Không tìm thấy bảng giá" });

            if (item.IsActive == true)
                return BadRequest(new { message = "Bảng giá này hiện đã đang hoạt động rồi." });

            // 2. KIỂM TRA XUNG ĐỘT 
            // Kiểm tra xem có bảng giá nào KHÁC đang hoạt động trùng cấu hình với bảng này không
            bool isConflict = await _context.BangGiaVungs.AnyAsync(bg =>
                bg.IsActive == true &&
                bg.KhuVucLay == item.KhuVucLay &&
                bg.KhuVucGiao == item.KhuVucGiao &&
                bg.LoaiTinhGia == item.LoaiTinhGia &&
                bg.MaLoaiHang == item.MaLoaiHang &&
                bg.MaBangGia != id // Không tính chính nó
            );

            if (isConflict)
            {
                return BadRequest(new
                {
                    message = "Không thể kích hoạt lại. Đã có một bảng giá khác đang hoạt động cho tuyến đường và loại hàng này.",
                    detail = "Vui lòng vô hiệu hóa bảng giá hiện tại trước khi mở lại bản cũ."
                });
            }

            // 3. Thực hiện kích hoạt
            var duLieuCu = new Dictionary<string, object> { { "Trạng thái", "Ngừng hoạt động" } };

            item.IsActive = true;
            item.NgayCapNhat = DateTime.Now;
            item.LyDoThayDoi = "Kích hoạt lại bảng giá từ lịch sử";

            await _context.SaveChangesAsync();

            // 4. Ghi Log và Clear Cache
            await _sys.GhiLogVaResetCacheAsync(
                "Quản lý bảng giá vùng",
                $"Kích hoạt lại bảng giá mã: {item.MaBangGia}",
                "BangGiaVung",
                id.ToString(),
                duLieuCu,
                new Dictionary<string, object> {
                { "Trạng thái", "Hoạt động trở lại" },
                { "Lý do", item.LyDoThayDoi }
                }
            );

            ClearPriceRegionCache();
            await transaction.CommitAsync();

            return Ok(new { success = true, message = "Bảng giá đã được hoạt động trở lại." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi kích hoạt lại bảng giá ID: {id}", id);
            return StatusCode(500, "Lỗi hệ thống khi thực hiện kích hoạt");
        }
    }

    [Authorize] // <--- THÊM 1: Bắt buộc phải đăng nhập mới được vô hiệu hóa
    [HttpDelete("vô-hieu-hoa/{id}")]
    public async Task<IActionResult> VoHieuHoa(int id)
    {
        // Lấy ID của người đang thực hiện thao tác từ SystemService
        var nguoiThucHienId = _sys.GetCurrentUserId();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var item = await _context.BangGiaVungs.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy dữ liệu" });

            // THÊM 2: Logic bảo vệ (Nếu cần, tùy thuộc vào nghiệp vụ của bạn)
            // Ví dụ: Không cho phép vô hiệu hóa nếu bảng giá này đang là mặc định
            // if (item.IsDefault) return BadRequest(new { message = "Không thể khóa bảng giá mặc định" });

            item.IsActive = false;
            item.NgayCapNhat = DateTime.Now;
            item.LyDoThayDoi = "Ngừng áp dụng bởi người dùng";

            await _context.SaveChangesAsync();

            // GHI LOG VÀ RESET CACHE
            // Lúc này _sys sẽ tự lấy nguoiThucHienId và Tên từ Cookie đã giải mã nhờ thẻ [Authorize]
            await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý bảng giá vùng",
                    $"Vô hiệu hóa bảng giá vùng mã: {item.MaBangGia}",
                    "BangGiaVung",
                    id.ToString(),
                    new Dictionary<string, object> { { "Trạng thái", "Đang hoạt động" } },
                    new Dictionary<string, object> {
                    { "Trạng thái", "Ngừng hoạt động" },
                    { "Lý do", item.LyDoThayDoi }
                    }
                );

            ClearPriceRegionCache();
            await transaction.CommitAsync();

            return Ok(new { success = true, message = "Đã ngừng áp dụng bảng giá này." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(); // <--- Đừng quên Rollback nếu lỗi nhé!
            _logger.LogError(ex, "Lỗi khi vô hiệu hóa bởi UserID: {UserId}", nguoiThucHienId);
            return StatusCode(500, "Lỗi hệ thống khi thực hiện thao tác");
        }
    }
    [Authorize]
    [HttpPost("capnhatgiavung")]
    public async Task<IActionResult> UpdateProcess([FromBody] BangGiaVungUpdateDto request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        var nguoiThucHienId = _sys.GetCurrentUserId();
        try
        {
            // 1. Kiểm tra bản ghi gốc
            var existing = await _context.BangGiaVungs
                .FirstOrDefaultAsync(x => x.MaBangGia == request.MaBangGia && x.IsActive == true);

            if (existing == null)
                return NotFound(new { message = "Không tìm thấy bảng giá đang hoạt động hoặc bản ghi đã bị thay đổi bởi người khác." });

            // Lưu lại dữ liệu cũ để ghi Log
            var datacu = new Dictionary<string, object>
            {
                {"Khu vực", $"{existing.KhuVucLay} -> {existing.KhuVucGiao}"},
                {"Giá cơ bản", existing.DonGiaCoBan},
                {"Trạng thái", "Đang hoạt động"}
            };

            bool isNewVersion = false;
            string hanhDongLog = "";

            // TRƯỜNG HỢP 1: VÔ HIỆU HÓA
            if (request.Action == "DEACTIVATE")
            {
                existing.IsActive = false;
                existing.NgayCapNhat = DateTime.Now;
                existing.LyDoThayDoi = request.LyDoThayDoi ?? "Ngừng áp dụng hệ thống";
                hanhDongLog = "Vô hiệu hóa bảng giá";
            }
            else
            {
                // 2. PHÂN TÍCH BIẾN ĐỘNG (Versioning)
                bool isVersionNeeded =
                    existing.KhuVucLay != request.KhuVucLay ||
                    existing.KhuVucGiao != request.KhuVucGiao ||
                    existing.DonGiaCoBan != request.DonGiaCoBan ||
                    existing.PhuPhiMoiKg != request.PhuPhiMoiKg ||
                    existing.DonGiaKm != request.DonGiaKm ||
                    existing.TrongLuongToiDaKg != request.TrongLuongToiDaKg ||
                    existing.MaLoaiHang != request.MaLoaiHang;

                if (isVersionNeeded)
                {
                    isNewVersion = true;
                    hanhDongLog = "Cập nhật phiên bản giá mới";

                    // Vô hiệu hóa bản cũ
                    existing.IsActive = false;
                    existing.NgayCapNhat = DateTime.Now;
                    existing.LyDoThayDoi = $"Nâng cấp lên phiên bản mới. Lý do: {request.LyDoThayDoi}";

                    // Tạo bản mới
                    var newVersion = new BangGiaVung
                    {
                        KhuVucLay = request.KhuVucLay,
                        KhuVucGiao = request.KhuVucGiao,
                        LoaiTinhGia = request.LoaiTinhGia,
                        TrongLuongToiThieuKg = request.TrongLuongToiThieuKg,
                        TrongLuongToiDaKg = request.TrongLuongToiDaKg,
                        DonGiaCoBan = request.DonGiaCoBan,
                        PhuPhiMoiKg = request.PhuPhiMoiKg,
                        DonGiaKm = request.DonGiaKm,
                        PhiDungDiem = request.PhiDungDiem,
                        KmToiThieu = request.KmToiThieu,
                        MaLoaiHang = request.MaLoaiHang,
                        MaBangCu = existing.MaBangGia,
                        IsActive = true,
                        NgayCapNhat = DateTime.Now,
                        LyDoThayDoi = request.LyDoThayDoi
                    };
                    _context.BangGiaVungs.Add(newVersion);
                }
                else
                {
                    hanhDongLog = "Cập nhật thông tin bổ sung";
                    existing.LyDoThayDoi = request.LyDoThayDoi;
                    existing.NgayCapNhat = DateTime.Now;
                }
            }

            await _context.SaveChangesAsync();

            // 3. GHI LOG VÀ RESET CACHE TOÀN HỆ THỐNG (Sử dụng SystemService đã hoàn thiện)
            var dataMoi = new Dictionary<string, object>
        {
            {"Thao tác", hanhDongLog},
            {"Lý do", request.LyDoThayDoi}
        };

            await _sys.GhiLogVaResetCacheAsync(
                "Quản lý bảng giá vùng",
                $"{hanhDongLog}: {existing.MaBangGia}",
                "BangGiaVung",
                existing.MaBangGia.ToString(),
                datacu,
                dataMoi
            );

            await transaction.CommitAsync();

            // Xóa cache local
            ClearPriceRegionCache();

            return Ok(new
            {
                success = true,
                message = isNewVersion ? "Đã tạo phiên bản giá mới thành công" : "Cập nhật thành công",
                isNewVersion = isNewVersion
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi cập nhật bảng giá ID: {ID}", request.MaBangGia);
            return StatusCode(500, new { message = "Lỗi máy chủ", detail = ex.Message });
        }
    }
    [HttpGet("lich-su/{id}")]
    public async Task<IActionResult> LayLichSuThayDoi(int id)
    {
        try
        {
            // 1. Tìm bản ghi hiện tại dựa trên ID (MaBangGia)
            var current = await _context.BangGiaVungs.AsNoTracking()
                .FirstOrDefaultAsync(bg => bg.MaBangGia == id);

            if (current == null)
                return NotFound(new { message = "Không tìm thấy dữ liệu" });

            // --- ĐIỀU KIỆN MỚI: Nếu không có mã lịch sử cũ thì trả về danh sách trống ---
            if (current.MaBangCu == null)
            {
                return Ok(new List<object>());
                // Hoặc trả về message tùy bạn: return Ok(new { message = "Không có lịch sử thay đổi" });
            }
            // --------------------------------------------------------------------------

            // 2. Truy vấn tất cả bản ghi có liên quan trong chuỗi lịch sử
            // Lấy những bản ghi có liên quan đến MaBangGia hiện tại hoặc MaBangCu của nó
            var history = await _context.BangGiaVungs
                .AsNoTracking()
                .Where(bg =>
                    bg.MaBangGia == current.MaBangCu ||
                    bg.MaBangCu == current.MaBangGia ||
                    (bg.KhuVucLay == current.KhuVucLay && bg.KhuVucGiao == current.KhuVucGiao))
                .OrderByDescending(bg => bg.NgayCapNhat)
                .Select(bg => new
                {
                    bg.MaBangGia,
                    bg.DonGiaCoBan,
                    bg.PhuPhiMoiKg,
                    bg.TrongLuongToiThieuKg,
                    bg.TrongLuongToiDaKg,
                    bg.NgayCapNhat,
                    bg.LyDoThayDoi,
                    bg.LoaiTinhGia,
                    bg.DonGiaKm,
                    bg.KmToiThieu,
                    bg.IsActive,
                    bg.MaBangCu // Thêm trường này để dễ theo dõi ở Front-end
                })
                .ToListAsync();

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy lịch sử bảng giá id: {id}", id);
            return StatusCode(500, "Lỗi hệ thống khi truy xuất lịch sử");
        }
    }

    [HttpPost("phan-tich-dich-vu-phu-hop")]
    public async Task<IActionResult> PhanTichDichVu([FromBody] YeuCauTinhPhi request)
    {
        try
        {
            // --- 1. TÍNH TRỌNG LƯỢNG QUY ĐỔI ---
            double trongLuongTheTich = (request.TheTichTong ?? 0) / 6000.0;
            decimal trongLuongDeTinh = (decimal)Math.Max((double)(request.KhoiLuongTong ?? 0), trongLuongTheTich);

            // Ngưỡng xác định hàng nặng (thường là 1000kg hoặc tùy quy định công ty)
            bool laHangNang = trongLuongDeTinh > 1000;

            // --- 2. XÁC ĐỊNH NHÃN TUYẾN ĐƯỜNG (MAPPING LOGIC) ---
            string nhanLay = "";
            string nhanGiao = "";

            if (request.ThanhPhoLay == request.ThanhPhoGiao)
            {
                // Trường hợp NỘI TỈNH
                nhanLay = "NOI_TINH";

                // Logic check Phường/Xã để phân loại Trung Tâm hay Huyện Xã
                bool laVungXa = await KiemTraVungSauVungXa(request.ThanhPhoGiao, request.PhuongGiaoHang);
                nhanGiao = laVungXa ? "NOI_TINH_HUYEN_XA" : "NOI_TINH_TRUNG_TAM";
            }
            else
            {
                // Trường hợp LIÊN TỈNH: Lấy nhãn theo Miền
                nhanLay = LayMienTuTenTinh(request.ThanhPhoLay);
                nhanGiao = LayMienTuTenTinh(request.ThanhPhoGiao);
            }

            // --- 3. XÂY DỰNG QUERY LỌC BẢNG GIÁ ---
            // Lấy toàn bộ danh sách phù hợp với tuyến đường trước
            var tatCaBangGia = await _context.BangGiaVungs
                .AsNoTracking()
                .Where(bg => bg.IsActive == true && bg.KhuVucLay == nhanLay && bg.KhuVucGiao == nhanGiao)
                .ToListAsync();

            // --- 4. LOGIC LỌC TRỌNG LƯỢNG VÀ KHỬ TRÙNG DỮ LIỆU ---
            var danhSachLoc = tatCaBangGia
                .Where(bg =>
                {
                    // Lọc theo MaLoaiHang (Nếu request > 0 thì lọc đúng mã, nếu = 0 thì lấy mặc định/tất cả)
                    if (request.MaLoaiHang > 0 && bg.MaLoaiHang != request.MaLoaiHang) return false;

                    // Lọc theo trọng lượng
                    if (laHangNang)
                    {
                        return bg.LoaiTinhGia == 2 || (bg.LoaiTinhGia == 1 && bg.TrongLuongToiDaKg > 1000);
                    }
                    var maxTrongLuongCuaTuyen = tatCaBangGia
                    .Where(x => x.LoaiTinhGia == 1 && x.MaLoaiHang == bg.MaLoaiHang)
                    .Max(x => x.TrongLuongToiDaKg ?? 0);

                    return (trongLuongDeTinh >= (bg.TrongLuongToiThieuKg ?? 0) && trongLuongDeTinh <= (bg.TrongLuongToiDaKg ?? 0))
                           || (trongLuongDeTinh > (decimal)maxTrongLuongCuaTuyen && (bg.TrongLuongToiDaKg ?? 0) == maxTrongLuongCuaTuyen);
                })
                // QUAN TRỌNG: Khử trùng dữ liệu tại đây
                // Nếu có nhiều dòng cùng Tuyến, cùng Khoảng cân, cùng Giá -> Chỉ lấy 1 dòng đầu tiên
                .GroupBy(x => new {
                    x.KhuVucLay,
                    x.KhuVucGiao,
                    x.TrongLuongToiThieuKg,
                    x.TrongLuongToiDaKg,
                    x.DonGiaCoBan,
                    x.LoaiTinhGia,
                    x.MaLoaiHang // Nếu muốn tách biệt giá theo loại hàng thì giữ lại trường này trong GroupBy
                })
                .Select(g => g.First())
                .ToList();

            // Thay thế biến danhSachBangGia cũ bằng danhSachLoc
            var danhSachBangGia = danhSachLoc;

            // --- 5. TÍNH TOÁN CHI TIẾT VÀ TRẢ KẾT QUẢ ---
            var ketQua = danhSachBangGia.Select(bg =>
            {
                decimal tongTien = 0;
                string chiTietGia = "";
                string tenDichVu = "";

                if (bg.LoaiTinhGia == 2) // Vận tải nguyên chuyến
                {
                    tenDichVu = $"Vận tải nguyên chuyến ";
                    decimal kmThucTe = (decimal)(request.SoKm ?? 0);
                    decimal kmTinhPhi = Math.Max(kmThucTe, (decimal)(bg.KmToiThieu ?? 0));

                    tongTien = (kmTinhPhi * (bg.DonGiaCoBan ?? 0)) + (bg.PhiDungDiem ?? 0);
                    chiTietGia = $"Lộ trình: {kmTinhPhi}km x {bg.DonGiaCoBan:N0}đ/km + Phí dừng: {bg.PhiDungDiem:N0}đ";
                }
                else // Chuyển phát bưu kiện
                {
                    tenDichVu = "Chuyển phát bưu kiện";
                    decimal giaGoc = bg.DonGiaCoBan ?? 0;
                    decimal mucToiDa = (decimal)(bg.TrongLuongToiDaKg ?? 0);
                    decimal khoiLuongVuot = Math.Max(0, trongLuongDeTinh - mucToiDa);

                    decimal phuPhi = khoiLuongVuot * (bg.PhuPhiMoiKg ?? 0);
                    tongTien = giaGoc + phuPhi;

                    chiTietGia = phuPhi > 0
                        ? $"Giá mốc ({mucToiDa}kg): {giaGoc:N0}đ + Vượt mốc: {khoiLuongVuot:N2}kg x {bg.PhuPhiMoiKg:N0}đ"
                        : $"Giá trọn gói cho {trongLuongDeTinh:N2}kg: {giaGoc:N0}đ";
                }

                return new
                {
                    MaBangGia = bg.MaBangGia,
                    TenDichVu = tenDichVu,
                    LoaiHinh = bg.LoaiTinhGia,
                    TrongLuongTinhPhi = trongLuongDeTinh,
                    TongTienDuKien = tongTien,
                    MoTaGia = chiTietGia,
                    KhuVuc = $"{nhanLay} -> {nhanGiao}"
                };
            }).OrderBy(x => x.TongTienDuKien).ToList();

            return Ok(ketQua);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    // --- CÁC HÀM BỔ TRỢ (HELPER METHODS) ---

    private async Task<bool> KiemTraVungSauVungXa(string? tinh, string? phuong)
    {
        if (string.IsNullOrEmpty(tinh) || string.IsNullOrEmpty(phuong)) return false;

        // Logic: Truy vấn vào bảng danh mục địa chính nội bộ
        // Nếu Phường/Xã này được đánh dấu là 'IsRemote' hoặc 'IsHuyenXa' thì trả về true
        return await _context.DiaChis
            .AnyAsync(x => x.ThanhPho == tinh && x.Phuong == phuong);
    }

    private string LayMienTuTenTinh(string? tenTinh)
    {
        if (string.IsNullOrEmpty(tenTinh)) return "KHAC";

        var mienBac = new List<string> { "Hà Nội", "Bắc Giang", "Hải Phòng", "Quảng Ninh", "Bắc Ninh", "Vĩnh Phúc" };
        var mienNam = new List<string> { "TP. Hồ Chí Minh", "Bình Dương", "Đồng Nai", "Long An", "Cần Thơ", "Vũng Tàu" };
        var mienTrung = new List<string> { "Đà Nẵng", "Huế", "Nghệ An", "Quảng Nam", "Khánh Hòa" };

        if (mienBac.Any(t => tenTinh.Contains(t, StringComparison.OrdinalIgnoreCase))) return "MIEN_BAC";
        if (mienNam.Any(t => tenTinh.Contains(t, StringComparison.OrdinalIgnoreCase))) return "MIEN_NAM";
        if (mienTrung.Any(t => tenTinh.Contains(t, StringComparison.OrdinalIgnoreCase))) return "MIEN_TRUNG";

        return "KHAC";
    }
}
