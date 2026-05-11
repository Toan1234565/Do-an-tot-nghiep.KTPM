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
using QuanLyTaiKhoanNguoiDung.Models12;
using QuanLyTaiKhoanNguoiDung.Models12._1234;
using QuanLyTaiKhoanNguoiDung.Models12.HamBam;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyLoTrinh.QuanLyLoTrinhTheoDoi;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyNhanVien;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyTaiXe;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyPhanQuyen;
using System.ComponentModel;
using System.Security.Claims;
using Tmdt.Shared.Services;
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
        private readonly CacheSignalService _cacheSignal;
        private readonly ISystemService _sys;
        private readonly PhanQuyenService _phanQuyen;
        public QuanLyHoSoTaiXe(TmdtContext context,
                               ILogger<QuanLyHoSoTaiXe> logger,
                               IMemoryCache cache,
                               ISystemService sys,
                               IEmailService emailService, PhanQuyenService phanQuyen, CacheSignalService cacheSignal)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _emailService = emailService;
            _phanQuyen = phanQuyen;
            _cacheSignal = cacheSignal;
            _sys = sys;
        }
       
        [HttpGet("danhsachtaixe")]
        [Authorize]
        public async Task<IActionResult> GetDanhSachTaiXe(
            [FromQuery] string? search,
            [FromQuery] string? loaiBang,
            [FromQuery] int? maKho,
            [FromQuery] string? trangthaihoatdong,
            [FromQuery] string? sortBy,
            [FromQuery] bool isDescending = true,
            [FromQuery] int page = 1,
            [FromQuery] bool trangthai = true)
        {
            try
            {
                

                // 3. Quản lý Cache
                var cacheKey = $"Drivers_K{maKho ?? 0}_S{search}_B{loaiBang}_Sort{sortBy}_{isDescending}_P{page}_TT{trangthai}_TTHD{trangthaihoatdong}";

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
                    if (maKho.HasValue) query = query.Where(tk => tk.NguoiDung.MaKho == maKho.Value);
                    if (!string.IsNullOrEmpty(loaiBang)) query = query.Where(tk => tk.NguoiDung.TaiXe.LoaiBangLai == loaiBang);
                    if (!string.IsNullOrEmpty(search))
                    {
                        string s = search.ToLower();
                        query = query.Where(tk => tk.NguoiDung.HoTenNhanVien.ToLower().Contains(s) || tk.NguoiDung.TaiXe.SoBangLai.Contains(s));
                    }
                    
                    // Lọc theo Trạng thái làm việc (Ready/Busy...)
                    if (!string.IsNullOrEmpty(trangthaihoatdong))
                    {
                        query = query.Where(tk => tk.NguoiDung.TaiXe.TrangThaiHoatDong == trangthaihoatdong);
                    }
                    // 1. Lọc theo Trạng thái tài khoản (Luôn áp dụng)
                    query = query.Where(tk => tk.HoatDong == trangthai);

                    // 2. Logic rẽ nhánh
                    if (trangthai == true)
                    {
                        // Chỉ lọc theo Trạng thái làm việc nếu tài khoản đang mở
                        if (!string.IsNullOrEmpty(trangthaihoatdong))
                        {
                            query = query.Where(tk => tk.NguoiDung.TaiXe.TrangThaiHoatDong == trangthaihoatdong);
                        }
                    }
                    else
                    {
                        // Nếu tài khoản bị khóa (trangthai == false), 
                        // chúng ta mặc định lấy tất cả những người có trạng thái là "Bị khóa" 
                        // hoặc bất kỳ trạng thái nào của tài khoản bị vô hiệu hóa.
                        query = query.Where(tk => tk.NguoiDung.TaiXe.TrangThaiHoatDong == "Bị khóa"
                                               || tk.NguoiDung.TaiXe.TrangThaiHoatDong == null);
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
                        .Select(tk => new TaiXeListModel
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
                        // Dùng đúng cái TokenSource đang sống trong Singleton
                        .AddExpirationToken(new CancellationChangeToken(_cacheSignal.TokenSource.Token));
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

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý tài xế",
                    "Gửi Email cảnh báo yêu cầu cấp lại bằng lái",
                    "",
                    "",
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>
                    {
                        {"thông tin", $"Đã gửi email cảnh báo hết hạn bằng lái cho tài xế {tx.MaNguoiDungNavigation.HoTenNhanVien} (ID: {id}) - Ngày hết hạn: {ngayHetHanStr}" }
                    }
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
                var tentaixe = data.FirstOrDefault()?.MaNguoiDungNavigation?.HoTenNhanVien ?? "Danh sách tài xế sắp hết hạn";
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

                    await _sys.GhiLogVaResetCacheAsync(
                       "Quản lý tài xế",
                       "Gửi Email cảnh báo yêu cầu cấp lại bằng lái",
                       "",
                       "",
                       new Dictionary<string, object>(),
                       new Dictionary<string, object>
                       {
                            {"thông tin đã gửi Email cho", tentaixe}
                       }
                   );

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

        [HttpGet("Lich-trinh-theo-chuyen")]
        public async Task<IActionResult> LichTrinhTheoChuyen([FromQuery] int maKho, [FromQuery] string? loaiXeYeuCau)
        {
            try
            {
                var now = DateTime.Now;
                var today = DateOnly.FromDateTime(now);
                var yesterday = today.AddDays(-1);
                var currentTime = TimeOnly.FromDateTime(now);

                var query = _context.TaiXes.AsNoTracking()
                    .Include(tx => tx.MaNguoiDungNavigation)
                    .ThenInclude(n => n.DangKyCaTrucs)
                    .ThenInclude(dk => dk.MaCaNavigation)
                    .AsQueryable();

                // 1. Lọc cơ bản: Đúng kho, đang sẵn sàng và bằng lái còn hạn
                query = query.Where(tx => tx.MaNguoiDungNavigation.MaKho == maKho
                                        && tx.TrangThaiHoatDong == "Sẵn sàng"
                                        && tx.NgayHetHanBang > today);

                // 2. Lọc theo loại xe/bằng lái
                if (!string.IsNullOrEmpty(loaiXeYeuCau))
                {
                    var req = loaiXeYeuCau.ToUpper();
                    query = query.Where(tx => tx.LoaiBangLai.ToUpper().Contains(req));
                }

                var tatCaTaiXePhuHop = await query.ToListAsync();

                // 3. Lọc tài xế đang trong ca trực hợp lệ
                var danhSachTaiXe = tatCaTaiXePhuHop.Where(tx =>
                    tx.MaNguoiDungNavigation.DangKyCaTrucs.Any(dk =>
                        dk.TrangThai == "Đã duyệt" && (
                            // TH1: Ca làm việc trong ngày hôm nay
                            (dk.NgayTruc == today && (
                                // Nếu là ca "Vận chuyển theo chuyến" (00:00 - 23:59) thì luôn đúng
                                dk.MaCa == 1068 ||
                                // Hoặc các ca thường: Giờ hiện tại nằm trong khoảng bắt đầu và kết thúc
                                (currentTime >= dk.MaCaNavigation.GioBatDau && currentTime <= dk.MaCaNavigation.GioKetThuc)
                            ))                           
                        )
                    )
                )
                .Select(tx => new
                {
                    tx.MaNguoiDung,
                    HoTen = tx.MaNguoiDungNavigation.HoTenNhanVien,
                    SoDienThoai = tx.MaNguoiDungNavigation.SoDienThoai,
                    tx.LoaiBangLai,
                    tx.KinhNghiemNam,
                    tx.DiemUyTin,
                    // Lấy tên ca hiện tại để hiển thị lên UI cho điều phối viên dễ nhìn
                    TenCaHienTai = tx.MaNguoiDungNavigation.DangKyCaTrucs
                                .Where(dk => dk.TrangThai == "Đã duyệt" &&
                                       (dk.NgayTruc == today || (dk.NgayTruc == yesterday && dk.MaCaNavigation.GioBatDau > dk.MaCaNavigation.GioKetThuc)))
                                .Select(dk => dk.MaCaNavigation.TenCa)
                                .FirstOrDefault()
                })
                .OrderByDescending(tx => tx.DiemUyTin) // Ưu tiên tài xế uy tín trước
                .ToList();

                return Ok(danhSachTaiXe);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        

        //GanTaiXe_PhuongTien

        [HttpGet("gan-tai-xe-phuong-tien/{maKho}")] // Chuyển sang HttpGet vì đây là thao tác lấy dữ liệu
        public async Task<IActionResult> GetDanhSachTaiXeTheoKho(int maKho, [FromQuery] bool? trangthai = false)
        {
            try
            {
                // 1. Tạo query cơ bản từ NguoiDung, kết hợp (Join) với TaiXe
                var query = _context.NguoiDungs
                    .Include(tx => tx.TaiXe)
                    .AsNoTracking()
                    .Where(nd => nd.MaKho == maKho && nd.MaChucVu == 16); // Chỉ lấy những người có bản ghi TaiXe

                // 2. Lọc theo trạng thái gán của Tài xế
                if (trangthai.HasValue)
                {
                    query = query.Where(nd => nd.TaiXe.Trangthaigan == trangthai.Value);
                }

                // 3. Thực thi truy vấn và Map dữ liệu ra Model
                var result = await query.Select(nd => new
                {
                    MaNguoiDung = nd.MaNguoiDung,
                    HoTen = nd.HoTenNhanVien,
                    // Vì nd.TaiXe là navigation property, ta truy cập trực tiếp vào nó
                    LoaiBangLai = nd.TaiXe != null ? nd.TaiXe.LoaiBangLai : "Chưa cập nhật"
                }).ToListAsync();

                // 4. Kiểm tra danh sách rỗng
                if (result == null || !result.Any())
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy tài xế nào tại kho {maKho} có trạng thái gán: {trangthai}"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách tài xế tại kho {MaKho}", maKho);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi xử lý hệ thống",
                    error = ex.Message
                });
            }
        }

        [HttpPut("cap-nhat-trang-thai-gan/{id}")]
        public async Task<IActionResult> UpdateTrangThai(int id, bool trangThai)
        {
            var taixe = await _context.TaiXes.FirstOrDefaultAsync(x => x.MaNguoiDung == id);
            if (taixe == null) return NotFound();
            taixe.Trangthaigan = trangThai;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("lich-trinh-tai-xe")]
        public async Task<IActionResult> GetLichTrinhTaiXe([FromQuery] int maKho, [FromQuery] string? loaiXeYeuCau)
        {
            try
            {
                var now = DateTime.Now;
                var today = DateOnly.FromDateTime(now);
                var yesterday = today.AddDays(-1);
                var currentTime = TimeOnly.FromDateTime(now);

                var query = _context.TaiXes.AsNoTracking()
                    .Include(tx => tx.MaNguoiDungNavigation)
                    .ThenInclude(n => n.DangKyCaTrucs)
                    .ThenInclude(dk => dk.MaCaNavigation)
                    .AsQueryable();

                // 1. Lọc theo kho và trạng thái cơ bản của tài xế
                query = query.Where(tx => tx.MaNguoiDungNavigation.MaKho == maKho
                                       && tx.TrangThaiHoatDong == "Sẵn sàng"
                                       && tx.NgayHetHanBang > today);

                // 2. Lọc theo loại xe/bằng lái nếu có yêu cầu (Sử dụng ToUpper để tránh lệch chữ hoa/thường)
                if (!string.IsNullOrEmpty(loaiXeYeuCau))
                {
                    var req = loaiXeYeuCau.ToUpper();
                    query = query.Where(tx => tx.LoaiBangLai.ToUpper().Contains(req));
                }

                var tatCaTaiXePhuHop = await query.ToListAsync();

                // 3. Lọc thủ công trong bộ nhớ để xử lý logic ca trực phức tạp (Xuyên đêm)
                var danhSachTaiXe = tatCaTaiXePhuHop.Where(tx =>
                    tx.MaNguoiDungNavigation.DangKyCaTrucs.Any(dk =>
                        dk.TrangThai == "Đã duyệt" && (
                            // TH1: Ca trực thuộc ngày hôm nay
                            (dk.NgayTruc == today && (
                                (dk.MaCaNavigation.GioBatDau <= dk.MaCaNavigation.GioKetThuc && currentTime >= dk.MaCaNavigation.GioBatDau && currentTime <= dk.MaCaNavigation.GioKetThuc) ||
                                (dk.MaCaNavigation.GioBatDau > dk.MaCaNavigation.GioKetThuc && (currentTime >= dk.MaCaNavigation.GioBatDau || currentTime <= dk.MaCaNavigation.GioKetThuc))
                            ))
                            ||
                            // TH2: Ca trực xuyên đêm bắt đầu từ hôm qua nhưng kết thúc vào hôm nay
                            (dk.NgayTruc == yesterday && dk.MaCaNavigation.GioBatDau > dk.MaCaNavigation.GioKetThuc && currentTime <= dk.MaCaNavigation.GioKetThuc)
                        )
                    )
                )
                .Select(tx => new
                {
                    tx.MaNguoiDung,
                    HoTen = tx.MaNguoiDungNavigation.HoTenNhanVien,
                    SoDienThoai = tx.MaNguoiDungNavigation.SoDienThoai,
                    tx.LoaiBangLai,
                    tx.KinhNghiemNam,
                    tx.DiemUyTin,
                    TenCa = tx.MaNguoiDungNavigation.DangKyCaTrucs
                                .Where(dk => dk.NgayTruc == today || (dk.NgayTruc == yesterday && dk.MaCaNavigation.GioBatDau > dk.MaCaNavigation.GioKetThuc))
                                .Select(dk => dk.MaCaNavigation.TenCa)
                                .FirstOrDefault()
                })
                .OrderByDescending(tx => tx.DiemUyTin)
                .ToList();

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

        [HttpGet("check-status/{id}")]
        public async Task<IActionResult> CheckDriverStatus(int id)
        {
            // 1. Tìm tài xế theo mã người dùng
            var taiXe = await _context.TaiXes
                .FirstOrDefaultAsync(t => t.MaNguoiDung == id);

            if (taiXe == null)
            {
                return NotFound(new { SanSang = false, Message = "Không tìm thấy tài xế này trong hệ thống." });
            }

            // 2. Logic kiểm tra tính sẵn sàng
            // Điều kiện 1: Trạng thái hoạt động phải là 'Đang rảnh' (hoặc 'Nghỉ ngơi' tùy bạn định nghĩa)
            // Điều kiện 2: Bằng lái phải còn hạn (NgayHetHanBang > Ngày hiện tại)

            var ngayHienTai = DateOnly.FromDateTime(DateTime.Now);
            bool bangConHan = taiXe.NgayHetHanBang > ngayHienTai;

            // Giả sử trạng thái sẵn sàng trong DB của bạn lưu là "Đang rảnh"
            bool trangThaiSanSang = taiXe.TrangThaiHoatDong == "Sẵn sàng";

            if (trangThaiSanSang && bangConHan)
            {
                return Ok(new
                {
                    MaNguoiDung = id,
                    SanSang = true,
                    Message = "Tài xế sẵn sàng nhận lệnh."
                });
            }

            // 3. Trả về lý do cụ thể nếu không sẵn sàng
            string lyDo = !bangConHan ? "Bằng lái đã hết hạn." : $"Tài xế hiện đang: {taiXe.TrangThaiHoatDong}";

            return Ok(new
            {
                MaNguoiDung = id,
                SanSang = false,
                Message = lyDo
            });
        }
    }
}