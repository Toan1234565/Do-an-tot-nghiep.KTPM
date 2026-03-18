using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using MimeKit;
using OfficeOpenXml;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12._1234;
using QuanLyTaiKhoanNguoiDung.Models12.HamBam;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyLoTrinhTheoDoi;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyTaiXe;
using System.ComponentModel;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using LicenseContext = OfficeOpenXml.LicenseContext;

namespace QuanLyTaiKhoanNguoiDung.ControllersAPI
{
    [Route("api/quanlytaixe")]
    [ApiController]
    public class QuanLyHoSoTaiXe : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<QuanLyHoSoTaiXe> _logger;
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService; // Dùng Interface chuẩn
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public QuanLyHoSoTaiXe(TmdtContext context,
                               ILogger<QuanLyHoSoTaiXe> logger,
                               IMemoryCache cache,
                               IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _emailService = emailService;
        }
        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId)) ? userId : null;
        }
        [HttpGet("danhsachtaixe")]
        [Authorize]
        public async Task<IActionResult> GetDanhSachTaiXe(
            [FromQuery] string? search,
            [FromQuery] string? loaiBang,
            [FromQuery] int? maKho,
            [FromQuery] string? sortBy,
            [FromQuery] bool isDescending = true,
            [FromQuery] int page = 1,
            [FromQuery] bool trangthai = true)
        {
            try
            {
                // 1. Lấy và kiểm tra ID người dùng hiện tại
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized(new { message = "Vui lòng đăng nhập." });

                // Lấy thông tin người dùng đang thực hiện request để check Vai trò và Kho
                var currentUser = await _context.NguoiDungs
                    .Include(nd => nd.MaChucVuNavigation)
                    .ThenInclude(cv => cv.MaVaiTroNavigation)
                    .FirstOrDefaultAsync(nd => nd.MaNguoiDung == currentUserId);

                if (currentUser == null)
                    return Unauthorized(new { message = "Người dùng không tồn tại." });

                string tenVaiTro = currentUser.MaChucVuNavigation?.TenChucVu ?? "";

                // 2. Xác định quyền (Bạn có thể điều chỉnh chuỗi này cho khớp chính xác với DB của bạn)
                bool isQuanLyTong = tenVaiTro.Contains("Quản lý tổng") || tenVaiTro.Contains("Admin");
                bool isQuanLyKho = tenVaiTro.Contains("Quản lý chi nhánh") || tenVaiTro.Contains("Quản lý kho");

                // Nếu không có cả 2 quyền trên thì từ chối truy cập (Forbidden)
                if (!isQuanLyTong && !isQuanLyKho)
                {
                    return StatusCode(403, new { message = "Bạn không có quyền truy cập danh sách này." });
                }

                // 3. Xử lý logic lọc mã Kho
                int? filterMaKho = maKho; // Mặc định dùng tham số từ client (dành cho quản lý tổng)
                if (isQuanLyKho && !isQuanLyTong)
                {
                    filterMaKho = currentUser.MaKho;
                }

                // 3. Quản lý Cache
                var cacheKey = $"Drivers_K{filterMaKho ?? 0}_S{search}_B{loaiBang}_Sort{sortBy}_{isDescending}_P{page}_TT{trangthai}";

                if (!_cache.TryGetValue(cacheKey, out var cachedData))
                {
                    int pageSize = 20;
                    var query = _context.TaiKhoans
                        .AsNoTracking()
                        .Include(tk => tk.NguoiDung)
                            .ThenInclude(nd => nd.TaiXe) 
                        .Include(tk => tk.NguoiDung)
                            .ThenInclude(nd => nd.MaChucVuNavigation)
                        .Where(tk => tk.NguoiDung != null && tk.NguoiDung.MaChucVu == 16) // Giả định 16 là Driver
                        .AsQueryable();

                    // Lọc dữ liệu
                    if (filterMaKho.HasValue) query = query.Where(tk => tk.NguoiDung.MaKho == filterMaKho.Value);
                    if (!string.IsNullOrEmpty(loaiBang)) query = query.Where(tk => tk.NguoiDung.TaiXe.LoaiBangLai == loaiBang);
                    if (!string.IsNullOrEmpty(search))
                    {
                        string s = search.ToLower();
                        query = query.Where(tk => tk.NguoiDung.HoTenNhanVien.ToLower().Contains(s) || tk.NguoiDung.TaiXe.SoBangLai.Contains(s));
                    }
                    if (!trangthai)
                    {
                        query = query.Where(tk => tk.HoatDong == trangthai);
                    }
                    // Sắp xếp
                    query = sortBy?.ToLower() switch
                    {
                        "kinhnghiem" => isDescending ? query.OrderByDescending(tk => tk.NguoiDung.TaiXe.KinhNghiemNam) : query.OrderBy(tk => tk.NguoiDung.TaiXe.KinhNghiemNam),
                        "diemuytin" => isDescending ? query.OrderByDescending(tk => tk.NguoiDung.TaiXe.DiemUyTin) : query.OrderBy(tk => tk.NguoiDung.TaiXe.DiemUyTin),
                        _ => query.OrderBy(nd => nd.MaNguoiDung)
                    };

                    var totalItems = await query.CountAsync();
                    // Chỉnh sửa phần Select để khớp với cấu trúc TaiKhoan -> NguoiDung -> TaiXe
                    var data = await query.Skip((page - 1) * pageSize).Take(pageSize)
                        .Select(tk => new QuanLyTaiXeModels
                        {
                            MaNguoiDung = tk.MaNguoiDung,
                            
                            HoTenTaiXe = tk.NguoiDung.HoTenNhanVien,
                            TrangThai = tk.HoatDong,
                           
                            SoBangLai = tk.NguoiDung.TaiXe != null ? tk.NguoiDung.TaiXe.SoBangLai : "",
                            LoaiBangLai = tk.NguoiDung.TaiXe != null ? tk.NguoiDung.TaiXe.LoaiBangLai : "",
                            KinhNghiemNam = tk.NguoiDung.TaiXe != null ? tk.NguoiDung.TaiXe.KinhNghiemNam : 0,
                            TrangThaiHoatDong = tk.NguoiDung.TaiXe != null ? tk.NguoiDung.TaiXe.TrangThaiHoatDong : "",
                            DiemUyTin = tk.NguoiDung.TaiXe != null ? tk.NguoiDung.TaiXe.DiemUyTin : 0
                        }).ToListAsync();

                    var result = new
                    {
                        TotalItems = totalItems,
                        TotalPages = (int)Math.Ceiling((double)totalItems / pageSize),
                        CurrentPage = page,
                        Data = data
                    };

                    var cacheOptions = new MemoryCacheEntryOptions()
                     .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                     .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token)); // Thêm dòng này
                    cachedData = result;
                    _cache.Set(cacheKey, cachedData, cacheOptions);
                    
                }
                return Ok(cachedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi API Danh sách tài xế");
                return StatusCode(500, "Lỗi hệ thống");
            }
        }
        [HttpGet("chitiet/{maNguoiDung}")]
        public async Task<IActionResult> GetChiTietTaiXe(int maNguoiDung)
        {
            try
            {
                // 1. Kiểm tra Cache để tăng tốc độ phản hồi
                var cacheKey = $"ChiTietTaiXe_{maNguoiDung}";
                if (_cache.TryGetValue(cacheKey, out ChiTietTaiXeModels cachedDetail))
                {
                    return Ok(cachedDetail);
                }

                // 2. Truy vấn dữ liệu đa tầng
                var data = await _context.TaiKhoans
                    .Include(tk => tk.NguoiDung)
                        .ThenInclude(nd => nd.TaiXe)
                    .Include(tk => tk.NguoiDung)
                        .ThenInclude(nd => nd.MaChucVuNavigation)
                    .FirstOrDefaultAsync(tk => tk.MaNguoiDung == maNguoiDung && tk.NguoiDung.TaiXe != null);

                if (data == null)
                {
                    return NotFound(new { message = "Không tìm thấy thông tin tài xế này." });
                }

                var nd = data.NguoiDung;
                var tx = nd.TaiXe;

                // 3. Map dữ liệu vào Model và thực hiện giải mã (Decrypt)
                var detail = new ChiTietTaiXeModels
                {
                    // Thông tin tài khoản & định danh
                    MaNguoiDung = data.MaNguoiDung,
                    TenDangNhap = data.TenDangNhap,
                    
                    MaChucVu = nd.MaChucVu,

                    // Thông tin cá nhân (Giải mã các trường nhạy cảm)
                    HoTenTaiXe = nd.HoTenNhanVien,
                    NgaySinh = nd.NgaySinh,
                    GioiTinh = nd.GioiTinh,
                    NoiSinh = nd.NoiSinh,
                    Email = nd.Email,
                    SoDienThoai = nd.SoDienThoai,
                    //HinhAnh = nd.HinhAnh,
                    SoCccd = !string.IsNullOrEmpty(nd.SoCccd) ? SecurityHelper.Decrypt(nd.SoCccd) : "",


                    // Thông tin nghiệp vụ tài xế
                    SoBangLai = tx?.SoBangLai,
                    LoaiBangLai = tx?.LoaiBangLai,
                    NgayCapBang = tx?.NgayCapBang,
                    NgayHetHanBang = tx.NgayHetHanBang,
                    KinhNghiemNam = tx.KinhNghiemNam,
                    TrangThaiHoatDong = tx.TrangThaiHoatDong,
                    DiemUyTin = tx.DiemUyTin,
                    AnhBangLaiSau = tx.AnhBangLaiSau,
                    AnhBangLaiTruoc = tx.AnhBangLaiTruoc,


                    // Thông tin tài chính (Giải mã)
                    SoTaiKhoan = !string.IsNullOrEmpty(nd.SoTaiKhoan) ? SecurityHelper.Decrypt(nd.SoTaiKhoan) : "",
                    TenNganHang = nd.TenNganHang,
                    BaoHiemXaHoi = !string.IsNullOrEmpty(nd.BaoHiemXaHoi) ? SecurityHelper.Decrypt(nd.BaoHiemXaHoi) : "",

                    // Thông tin đơn vị
                    DonViLamViec = nd.DonViLamViec,
                    TenChucVu = nd.MaChucVuNavigation?.TenChucVu
                };

                // 4. Thiết lập Cache (Hết hạn sau 10 phút hoặc 2 phút nếu không truy cập)
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));

                _cache.Set(cacheKey, detail, cacheOptions);

                return Ok(detail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết tài xế ID: {MaNguoiDung}", maNguoiDung);
                return StatusCode(500, "Lỗi hệ thống khi tải thông tin chi tiết.");
            }
        }
        //[HttpPut("capnhattaixe")]
        //public async Task<IActionResult> UpdateHoSoTaiXe([FromBody] UpdateTaiXeModel model)
        //{
        //    try
        //    {
        //        var taiXe = await _context.TaiXes
        //            .Include(tx => tx.MaNguoiDungNavigation)
        //            .FirstOrDefaultAsync(tx => tx.MaNguoiDung == model.MaNguoiDung);

        //        if (taiXe == null) return NotFound("Tài xế không tồn tại.");

        //        var nguoiDung = taiXe.MaNguoiDungNavigation;

        //        // Cập nhật thông tin Người dùng (Lưu ý: Nếu dùng SecurityHelper.Encrypt thì nên encrypt lại ở đây)
        //        nguoiDung.SoDienThoai = model.SoDienThoai;
        //        nguoiDung.BaoHiemXaHoi = !string.IsNullOrEmpty(model.BaoHiemXaHoi) ? SecurityHelper.Encrypt(model.BaoHiemXaHoi) : nguoiDung.BaoHiemXaHoi;
        //        nguoiDung.DonViLamViec = model.DonViLamViec;
        //        nguoiDung.MaChucVu = model.MaChucVu;

        //        // Cập nhật thông tin Tài xế
        //        taiXe.SoBangLai = model.SoBangLai;

        //        if (taiXe.LoaiBangLai != model.LoaiBangLai)
        //        {
        //            // Kiểm tra xem người dùng có gửi kèm Số bằng mới và Ngày mới không
        //            if (string.IsNullOrEmpty(model.SoBangLai) || model.NgayCapBang == default || model.NgayHetHanBang == default)
        //            {
        //                return BadRequest("Khi thay đổi Loại bằng lái, bạn phải cung cấp đầy đủ Số bằng, Ngày cấp và Ngày hết hạn mới.");
        //            }

        //            // Nếu ok thì cập nhật toàn bộ cụm thông tin bằng lái
        //            taiXe.LoaiBangLai = model.LoaiBangLai;
        //            taiXe.SoBangLai = model.SoBangLai;
        //            taiXe.NgayCapBang = model.NgayCapBang;
        //            taiXe.NgayHetHanBang = model.NgayHetHanBang;

        //            _logger.LogInformation("Tài xế {ID} đã nâng cấp/thay đổi loại bằng sang {Loai}", model.MaNguoiDung, model.LoaiBangLai);
        //        }
        //        else
        //        {
        //            // Nếu không đổi loại bằng, vẫn cho phép cập nhật các trường lẻ nếu có thay đổi
        //            taiXe.SoBangLai = model.SoBangLai;
        //            taiXe.NgayCapBang = model.NgayCapBang;
        //            taiXe.NgayHetHanBang = model.NgayHetHanBang;
        //        }


        //        taiXe.KinhNghiemNam = model.KinhNghiemNam;
        //        taiXe.TrangThaiHoatDong = model.TrangThaiHoatDong;

        //        if (model.DiemUyTin.HasValue && model.DiemUyTin <= 100)
        //        {
        //            taiXe.DiemUyTin = model.DiemUyTin;
        //        }

        //        if (!string.IsNullOrEmpty(model.AnhBangLaiTruoc)) taiXe.AnhBangLaiTruoc = model.AnhBangLaiTruoc;
        //        if (!string.IsNullOrEmpty(model.AnhBangLaiSau)) taiXe.AnhBangLaiSau = model.AnhBangLaiSau;

        //        await _context.SaveChangesAsync();

        //        // --- PHẦN QUAN TRỌNG: LÀM MỚI CACHE ---

        //        // 1. Xóa cache chi tiết của chính tài xế này
        //        _cache.Remove($"ChiTietTaiXe_{model.MaNguoiDung}");

        //        // 2. Xóa cache danh sách. 
        //        // Vì MemoryCache không hỗ trợ xóa theo Pattern (wildcard), 
        //        // cách tốt nhất là dùng một "Signal Token" hoặc xóa các key phổ biến.
        //        // Đơn giản nhất cho quy mô nhỏ là xóa các key mặc định:
        //        _cache.Remove("DanhSachTaiXe_null_null_null_True_1");

        //        // Nếu bạn muốn triệt để hơn, hãy cân nhắc sử dụng một biến Static để quản lý phiên bản Cache
        //        // hoặc tạm thời chấp nhận việc người dùng sẽ thấy dữ liệu mới sau khi hết SlidingExpiration (5p).

        //        return Ok(new { message = "Cập nhật hồ sơ thành công!" });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Lỗi cập nhật tài xế {ID}", model.MaNguoiDung);
        //        return BadRequest("Lỗi cập nhật: " + ex.Message);
        //    }
        //}
        [HttpGet("canh-bao-het-han")]
        public async Task<IActionResult> GetCanhBaoHetHan([FromQuery] int days = 30)
        {
            try
            {
                // 1. Lấy ngày hiện tại kiểu DateOnly
                DateOnly today = DateOnly.FromDateTime(DateTime.Now);
                DateOnly warningDate = today.AddDays(days);

                // 2. Truy vấn dữ liệu từ Database
                var query = _context.TaiXes
                    .Include(tx => tx.MaNguoiDungNavigation)
                    .Where(tx => tx.NgayHetHanBang != null &&
                                 tx.NgayHetHanBang >= today &&
                                 tx.NgayHetHanBang <= warningDate);

                // 3. Thực thi truy vấn và Map dữ liệu
                var rawData = await query
                    .OrderBy(tx => tx.NgayHetHanBang)
                    .Select(tx => new
                    {
                        tx.MaNguoiDung,
                        HoTenTaiXe = tx.MaNguoiDungNavigation.HoTenNhanVien,
                        tx.SoBangLai,
                        tx.LoaiBangLai,
                        NgayHetHan = tx.NgayHetHanBang,
                        SoDienThoai = tx.MaNguoiDungNavigation.SoDienThoai
                    })
                    .ToListAsync();

                // 4. Tính toán SoNgayConLai ở bộ nhớ (vì DayNumber không hỗ trợ tốt trong SQL translation của một số Provider)
                var result = rawData.Select(tx => new
                {
                    tx.MaNguoiDung,
                    tx.HoTenTaiXe,
                    tx.SoBangLai,
                    tx.LoaiBangLai,
                    NgayHetHan = tx.NgayHetHan.ToString("dd/MM/yyyy"), // Định dạng ngày đẹp cho FE
                    SoNgayConLai = tx.NgayHetHan.DayNumber - today.DayNumber,
                    tx.SoDienThoai,
                    // Thêm mức độ cảnh báo để FE dễ xử lý màu sắc
                    MucDo = (tx.NgayHetHan.DayNumber - today.DayNumber) <= 7 ? "Nguy hiểm" : "Canh bao"
                }).ToList();

                return Ok(new
                {
                    TotalWarnings = result.Count,
                    Data = result,
                    CheckAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                // Log lỗi chi tiết để debug
                _logger.LogError(ex, "Lỗi khi lấy danh sách cảnh báo hết hạn bằng lái cho {days} ngày", days);
                return StatusCode(500, new { Message = "Lỗi hệ thống khi truy vấn dữ liệu cảnh báo", Error = ex.Message });
            }
        }
        [HttpPost("GuiThongBaoCapLai")]
        public async Task<IActionResult> GuiThongBaoCapLai([FromQuery] int id)
        {
            var tx = await _context.TaiXes
                .Include(t => t.MaNguoiDungNavigation)
                .FirstOrDefaultAsync(t => t.MaNguoiDung == id);

            if (tx == null)
                return NotFound(new { success = false, message = "Không tìm thấy tài xế." });

            if (string.IsNullOrEmpty(tx.MaNguoiDungNavigation?.Email))
                return BadRequest(new { success = false, message = "Tài xế không có thông tin email." });

            try
            {
                // Vì NgayHetHanBang là DateOnly (không null), bạn gọi trực tiếp ToString
                string ngayHetHanStr = tx.NgayHetHanBang.ToString("dd/MM/yyyy");

                await _emailService.SendEmailAsync(
                    tx.MaNguoiDungNavigation.Email,
                    tx.MaNguoiDungNavigation.HoTenNhanVien ?? "Tài xế",
                    ngayHetHanStr
                );

                return Ok(new { success = true, message = "Gửi email thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi gửi email: " + ex.Message });
            }
        }
        [HttpGet("xuat-bao-cao-het-han")]
        [Obsolete]
        public async Task<IActionResult> XuatBaoCaoHetHan([FromQuery] int days = 30)
        {
            try
            {
                // 1. Chuẩn bị dữ liệu thời gian
                DateOnly today = DateOnly.FromDateTime(DateTime.Now);
                DateOnly warningDate = today.AddDays(days);

                // 2. Truy vấn dữ liệu: Thêm AsNoTracking để tăng hiệu suất và kiểm tra null NgayHetHanBang
                var data = await _context.TaiXes
                    .Include(tx => tx.MaNguoiDungNavigation)
                    .Where(tx => tx.NgayHetHanBang != null &&
                                 tx.NgayHetHanBang >= today &&
                                 tx.NgayHetHanBang <= warningDate)
                    .OrderBy(tx => tx.NgayHetHanBang)
                    .AsNoTracking()
                    .ToListAsync();

                if (data == null || !data.Any())
                {
                    return NotFound("Không có dữ liệu tài xế sắp hết hạn trong khoảng thời gian này.");
                }

                // 3. Cấu hình EPPlus
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Danh Sách Hết Hạn");

                    // Tạo Header
                    string[] headers = { "Mã NV", "Họ Tên Tài Xế", "Số Bằng Lái", "Loại Bằng", "Ngày Hết Hạn", "Số Điện Thoại", "Số Ngày Còn Lại" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cells[1, i + 1];
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.DarkBlue);
                        cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    }

                    // 4. Đổ dữ liệu
                    int row = 2;
                    foreach (var item in data)
                    {
                        worksheet.Cells[row, 1].Value = item.MaNguoiDung;
                        worksheet.Cells[row, 2].Value = item.MaNguoiDungNavigation?.HoTenNhanVien ?? "N/A";
                        worksheet.Cells[row, 3].Value = item.SoBangLai ?? "N/A";
                        worksheet.Cells[row, 4].Value = item.LoaiBangLai ?? "N/A";

                        // Xử lý hiển thị ngày
                        worksheet.Cells[row, 5].Value = item.NgayHetHanBang.ToString("dd/MM/yyyy");

                        worksheet.Cells[row, 6].Value = item.MaNguoiDungNavigation?.SoDienThoai ?? "N/A";

                        // Tính toán số ngày còn lại
                        int remainingDays = item.NgayHetHanBang.DayNumber - today.DayNumber;
                        worksheet.Cells[row, 7].Value = remainingDays;

                        // Định dạng màu đỏ nếu còn dưới 7 ngày (Trực quan hóa báo cáo)
                        if (remainingDays <= 7)
                        {
                            worksheet.Cells[row, 7].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                            worksheet.Cells[row, 7].Style.Font.Bold = true;
                        }

                        row++;
                    }

                    // Tự động căn chỉnh cột
                    worksheet.Cells.AutoFitColumns();

                    // 5. Trả về file stream
                    var fileContents = package.GetAsByteArray();
                    string fileName = $"BaoCao_HetHan_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

                    return File(
                        fileContents,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        fileName
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xuất báo cáo Excel cho {days} ngày", days);
                // Trả về message lỗi chi tiết để dễ debug (Trong môi trường Dev)
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }


        [HttpGet("lich-trinh-tai-xe")]
        public async Task<IActionResult> GetLichTrinhTaiXe([FromQuery] int maKho, [FromQuery] string? loaiXeYeuCau)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                var currentTime = TimeOnly.FromDateTime(DateTime.Now);

                var query = _context.TaiXes.AsNoTracking().AsQueryable();

                // 1. Lọc theo loại xe/bằng lái nếu có yêu cầu từ điều phối
                if (!string.IsNullOrEmpty(loaiXeYeuCau))
                {
                    query = query.Where(tx => tx.LoaiBangLai.Contains(loaiXeYeuCau));
                }

                var danhSachTaiXe = await query
                    .Where(tx => tx.NgayHetHanBang > today
                        && tx.TrangThaiHoatDong == "Sẵn sàng"
                        // Truy cập qua MaNguoiDungNavigation vì DangKyCaTrucs đã chuyển sang NguoiDung
                        && tx.MaNguoiDungNavigation.DangKyCaTrucs.Any(dk =>
                            dk.NgayTruc == today
                            && dk.TrangThai == "Đã duyệt"
                            && dk.MaCaNavigation.MaKho == maKho
                            && (
                                // Trường hợp 1: Ca trong ngày (VD: 08:00 - 17:00)
                                (dk.MaCaNavigation.GioBatDau <= dk.MaCaNavigation.GioKetThuc
                                    && currentTime >= dk.MaCaNavigation.GioBatDau
                                    && currentTime <= dk.MaCaNavigation.GioKetThuc)
                                ||
                                // Trường hợp 2: Ca xuyên đêm (VD: 22:00 - 06:00 sáng hôm sau)
                                (dk.MaCaNavigation.GioBatDau > dk.MaCaNavigation.GioKetThuc
                                    && (currentTime >= dk.MaCaNavigation.GioBatDau || currentTime <= dk.MaCaNavigation.GioKetThuc))
                            )
                        ))
                    .Select(tx => new
                    {
                        tx.MaNguoiDung,
                        HoTen = tx.MaNguoiDungNavigation.HoTenNhanVien,
                        SoDienThoai = tx.MaNguoiDungNavigation.SoDienThoai,
                        tx.LoaiBangLai,
                        tx.KinhNghiemNam,
                        tx.DiemUyTin,
                        TenCa = tx.MaNguoiDungNavigation.DangKyCaTrucs
                                .Where(dk => dk.NgayTruc == today)
                                .Select(dk => dk.MaCaNavigation.TenCa)
                                .FirstOrDefault()
                    })
                    .OrderByDescending(tx => tx.DiemUyTin) // Ưu tiên tài xế uy tín cao lên đầu để điều phối
                    .ToListAsync();

                return Ok(danhSachTaiXe);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost("cap-nhat-trang-thai")]
        public async Task<IActionResult> UpdateTrangThaiTaiXe([FromBody] UpdateTaiXeTrangTai model)
        {
            // 0. Kiểm tra đầu vào cơ bản
            if (model == null || string.IsNullOrWhiteSpace(model.TrangThaiMoi))
            {
                return BadRequest(new { success = false, message = "Dữ liệu trạng thái không hợp lệ." });
            }

            try
            {
                // 1. Tìm tài xế
                var taiXe = await _context.TaiXes
                    .FirstOrDefaultAsync(tx => tx.MaNguoiDung == model.MaNguoiDung);

                if (taiXe == null)
                {
                    return NotFound(new { success = false, message = $"Không tìm thấy tài xế với mã {model.MaNguoiDung}." });
                }

                // 2. Cập nhật trạng thái trong DB
                string trangThaiCu = taiXe.TrangThaiHoatDong;

                // Chỉ cập nhật nếu trạng thái thực sự thay đổi để tiết kiệm tài nguyên
                if (trangThaiCu != model.TrangThaiMoi)
                {
                    taiXe.TrangThaiHoatDong = model.TrangThaiMoi;
                    await _context.SaveChangesAsync();

                    // 3. Xử lý Cache (Chỉ thực hiện khi có thay đổi thực sự)
                    string cacheKeyDetail = $"ChiTietTaiXe_{model.MaNguoiDung}";
                    string cacheKeyList = "DanhSachTaiXe_null_null_null_True_1";

                    _cache.Remove(cacheKeyDetail);
                    _cache.Remove(cacheKeyList);

                    _logger.LogInformation("Tài xế {ID} thay đổi trạng thái từ '{Cu}' sang '{Moi}'",
                        model.MaNguoiDung, trangThaiCu, model.TrangThaiMoi);
                }

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật trạng thái thành công!",
                    data = new
                    {
                        MaNguoiDung = model.MaNguoiDung,
                        TrangThaiMoi = model.TrangThaiMoi,
                        ThoiGianCapNhat = DateTime.Now
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật trạng thái tài xế {ID}", model.MaNguoiDung);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        //API Lấy thông tên tài xế để hiện ở trong giao diện theo dõi lộ trình 
        [HttpGet("lay-ten-tai-xe/{maNguoiDung}")] // 1. Dùng HttpGet cho tác vụ đọc dữ liệu
        public async Task<IActionResult> LayTenTaiXe(int maNguoiDung)
        {
            try
            {
                // 2. Tận dụng MemoryCache: Kiểm tra xem tên tài xế này đã có trong cache chưa
                string cacheKey = $"TenTaiXe_{maNguoiDung}";
                if (_cache.TryGetValue(cacheKey, out string tenTaiXeCached))
                {
                    return Ok(new TenTaiXeLoTrinhModels
                    {
                        MaNguoiDung = maNguoiDung,
                        TenTaiXeThucHien = tenTaiXeCached
                        
                    });
                }

                // 3. Tối ưu EF Core: Dùng Select để ép DB chỉ query đúng 1 cột HoTenNhanVien
                var tenTaiXeDb = await _context.TaiXes
                    .Where(tx => tx.MaNguoiDung == maNguoiDung)
                    .Select(tx => tx.MaNguoiDungNavigation.HoTenNhanVien) // Không load cả object vào RAM
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(tenTaiXeDb))
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tài xế hoặc thông tin người dùng." });
                }

                // Lưu kết quả vào Cache trong 60 phút để tái sử dụng cho các request sau
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(60));
                _cache.Set(cacheKey, tenTaiXeDb, cacheOptions);

                return Ok(new TenTaiXeLoTrinhModels
                {
                    MaNguoiDung = maNguoiDung,
                    TenTaiXeThucHien = tenTaiXeDb
                });
            }
            catch (Exception ex)
            {
                // 4. Ghi log chi tiết lỗi cho Dev đọc, trả lỗi chung chung cho Client
                _logger.LogError(ex, "Lỗi xảy ra khi lấy tên tài xế cho mã người dùng: {MaNguoiDung}", maNguoiDung);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống nội bộ. Vui lòng thử lại sau." });
            }
        }
    }
}