using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyKhachHang;
using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1.QuanLyBangGiaVung;

[Route("api/quanlybangiavung")]
[ApiController]
public class QuanLyBangGiaVung : ControllerBase
{
    private readonly ILogger<QuanLyBangGiaVung> _logger;
    private readonly TmdtContext _context;
    private readonly IMemoryCache _cache;

    // Key gốc để quản lý phụ thuộc cache
    private const string PriceRegionCachePrefix = "PriceRegionList_";
    private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();

    public QuanLyBangGiaVung(ILogger<QuanLyBangGiaVung> logger, TmdtContext context, IMemoryCache cache)
    {
        _logger = logger;
        _context = context;
        _cache = cache;
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
            bg.MaLoaiHang == model.MaLoaiHang
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

            ClearPriceRegionCache();
            return Ok(new { message = "Thêm bảng giá thành công", data = model });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thêm mới bảng giá ID: {MaBangGia}", model.MaBangGia);
            return StatusCode(500, new { message = "Lỗi máy chủ khi lưu dữ liệu", detail = ex.Message });
        }
    }
    [HttpDelete("vô-hieu-hoa/{id}")]
    public async Task<IActionResult> VoHieuHoa(int id)
    {
        try
        {
            var item = await _context.BangGiaVungs.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy dữ liệu" });

            item.IsActive = false;
            item.NgayCapNhat = DateTime.Now;
            item.LyDoThayDoi = "Ngừng áp dụng bởi người dùng";

            await _context.SaveChangesAsync();
            ClearPriceRegionCache();

            return Ok(new { success = true, message = "Đã ngừng áp dụng bảng giá này." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi vô hiệu hóa");
            return StatusCode(500, "Lỗi hệ thống");
        }
    }

    [HttpPost("capnhatgiavung")]
    public async Task<IActionResult> UpdateProcess([FromBody] BangGiaVungUpdateDto request)
    {
        // Sử dụng transaction để đảm bảo an toàn dữ liệu chéo bảng (nếu có)
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Kiểm tra bản ghi gốc và đảm bảo nó vẫn đang Active (tránh Race Condition)
            var existing = await _context.BangGiaVungs
                .FirstOrDefaultAsync(x => x.MaBangGia == request.MaBangGia && x.IsActive == true);

            if (existing == null)
                return NotFound(new { message = "Không tìm thấy bảng giá đang hoạt động hoặc bản ghi đã bị thay đổi bởi người khác." });

            // TRƯỜNG HỢP 1: VÔ HIỆU HÓA (XÓA MỀM)
            if (request.Action == "DEACTIVATE")
            {
                existing.IsActive = false;
                existing.NgayCapNhat = DateTime.Now;
                existing.LyDoThayDoi = request.LyDoThayDoi ?? "Ngừng áp dụng hệ thống";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                ClearPriceRegionCache();
                return Ok(new { success = true, message = "Đã vô hiệu hóa bảng giá thành công." });
            }

            // 2. PHÂN TÍCH BIẾN ĐỘNG: Đưa tất cả các trường ảnh hưởng đến TIỀN vào đây
            // Nếu thay đổi khu vực hoặc giá thì đều phải ra version mới để đối soát
            bool isVersionNeeded =
                existing.KhuVucLay != request.KhuVucLay ||
                existing.KhuVucGiao != request.KhuVucGiao ||
                existing.DonGiaCoBan != request.DonGiaCoBan ||
                existing.PhuPhiMoiKg != request.PhuPhiMoiKg ||
                existing.DonGiaKm != request.DonGiaKm ||
                existing.PhiDungDiem != request.PhiDungDiem ||
                existing.KmToiThieu != request.KmToiThieu ||
                existing.TrongLuongToiThieuKg != request.TrongLuongToiThieuKg ||
                existing.MaLoaiHang != request.MaLoaiHang||
                existing.TrongLuongToiDaKg != request.TrongLuongToiDaKg;


            // TRƯỜNG HỢP 2: TẠO PHIÊN BẢN MỚI (Versioning)
            if (isVersionNeeded)
            {
                // Vô hiệu hóa bản cũ trước
                existing.IsActive = false;
                existing.NgayCapNhat = DateTime.Now;
                existing.LyDoThayDoi = $"Đã nâng cấp lên phiên bản mới. Lý do: {request.LyDoThayDoi}";

                // Copy và tạo bản mới với dữ liệu cập nhật
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
                    MaBangCu = existing.MaBangGia, // Giữ liên kết lịch sử
                    IsActive = true,
                    NgayCapNhat = DateTime.Now,
                    LyDoThayDoi = request.LyDoThayDoi
                };

                _context.BangGiaVungs.Add(newVersion);
            }
            else
            {
                // TRƯỜNG HỢP 3: CHỈ SỬA GHI CHÚ/THÔNG TIN PHỤ (Không ảnh hưởng giá)
                existing.LyDoThayDoi = request.LyDoThayDoi;
                existing.NgayCapNhat = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Xóa cache để các request điều phối sau đó lấy được giá mới ngay lập tức
            ClearPriceRegionCache();

            return Ok(new
            {
                success = true,
                message = isVersionNeeded ? "Đã tạo phiên bản giá mới thành công" : "Cập nhật thông tin bổ sung thành công",
                isNewVersion = isVersionNeeded
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi nghiêm trọng khi cập nhật bảng giá ID: {ID}", request.MaBangGia);
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
            // 1. Tính trọng lượng quy đổi (Hệ số 6000)
            // Lưu ý: Request truyền vào là của TỪNG KIỆN hoặc TỔNG tùy theo logic gọi từ API tạo đơn
            double trongLuongTheTich = (request.TheTichTong ?? 0) / 6000.0;
            decimal trongLuongDeTinh = (decimal)Math.Max((double)(request.KhoiLuongTong ?? 0), trongLuongTheTich);

            // 2. Query bảng giá - Lọc theo tuyến đường và trạng thái
            var query = _context.BangGiaVungs
                .AsNoTracking()
                .Where(bg => bg.IsActive == true &&
                             bg.KhuVucLay == request.ThanhPhoLay &&
                             bg.KhuVucGiao == request.ThanhPhoGiao);

            // Lọc theo loại hàng cụ thể của kiện hàng
            if (request.MaLoaiHang > 0)
            {
                query = query.Where(bg => bg.MaLoaiHang == request.MaLoaiHang);
            }

            // Lọc theo dải trọng lượng mà kiện hàng rơi vào
            var danhSachBangGia = await query
                .Where(bg => trongLuongDeTinh >= (bg.TrongLuongToiThieuKg ?? 0) &&
                             (bg.TrongLuongToiDaKg == null || trongLuongDeTinh <= bg.TrongLuongToiDaKg))
                .ToListAsync();

            if (!danhSachBangGia.Any())
                return NotFound(new { message = "Không tìm thấy dịch vụ hoặc loại hàng phù hợp cho tuyến đường này." });

            // 3. Tính toán chi tiết chi phí dự kiến
            var ketQua = danhSachBangGia.Select(bg =>
            {
                decimal tongTien = 0;
                decimal phuPhiKg = 0;
                string chiTietGia = "";

                if (bg.LoaiTinhGia == 2) // VẬN TẢI CHUYẾN (Theo Km - Thường thuê nguyên xe)
                {
                    decimal kmThucTe = (decimal)(request.SoKm ?? 0);
                    decimal kmTinhPhi = Math.Max(kmThucTe, (decimal)(bg.KmToiThieu ?? 0));

                    

                    tongTien = (kmTinhPhi * (bg.DonGiaKm ?? 0)) + (bg.PhiDungDiem ?? 0);

                    chiTietGia = $"{kmTinhPhi}km x {bg.DonGiaKm:N0}đ + Phí dừng {bg.PhiDungDiem:N0}đ + Phụ phí kg {phuPhiKg:N0}đ";
                }
                else // BƯU KIỆN (Giao lẻ theo vùng)
                {
                    // Đối với bưu kiện, thường có Giá cơ bản cho mốc ToiThieu, và cộng thêm cho mỗi kg vượt mốc
                    decimal giaCoBan = bg.DonGiaCoBan ?? 0;

                    // Tính phụ phí cho mỗi kg nếu khối lượng thực tế lớn hơn mốc tối thiểu
                    decimal khoiLuongVuot = Math.Max(0, trongLuongDeTinh - (decimal)(bg.TrongLuongToiThieuKg ?? 0));
                    phuPhiKg = khoiLuongVuot * (bg.PhuPhiMoiKg ?? 0);

                    tongTien = giaCoBan + phuPhiKg;

                    chiTietGia = phuPhiKg > 0
                        ? $"Giá gốc {giaCoBan:N0}đ + Phụ phí vượt {khoiLuongVuot}kg x {bg.PhuPhiMoiKg:N0}đ"
                        : $"Giá trọn gói dải {bg.TrongLuongToiThieuKg}kg: {giaCoBan:N0}đ";
                }

                return new
                {
                    bg.MaBangGia,
                    bg.MaLoaiHang,
                    TenDichVu = bg.LoaiTinhGia == 1 ? "Giao hàng bưu kiện" : "Vận tải chuyến",
                    TrongLuongApDung = trongLuongDeTinh,
                    TongTienDuKien = tongTien,
                    MoTaGia = chiTietGia,
                    GhiChu = $"Tuyến: {bg.KhuVucLay} -> {bg.KhuVucGiao} | Loại hàng: {bg.MaLoaiHang}"
                };
            }).OrderBy(x => x.TongTienDuKien).ToList();

            return Ok(ketQua);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi phân tích giá. Request: {@Request}", request);
            return StatusCode(500, "Lỗi hệ thống khi tính toán giá cước.");
        }
    }
}