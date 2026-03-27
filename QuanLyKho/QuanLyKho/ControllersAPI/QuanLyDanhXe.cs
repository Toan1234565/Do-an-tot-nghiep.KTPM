using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using OfficeOpenXml;
using QuanLyKho.Models;
using QuanLyKho.Models1;
using QuanLyKho.Models1.QuanLyXe;
using System.ComponentModel;
using Tmdt.Shared.Services;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using LicenseContext = OfficeOpenXml.LicenseContext;



namespace QuanLyKho.ControllersAPI
{
    [Route("api/quanlyxe")]
    [ApiController]
    [EnableCors("AllowSpecificOrigins")]
    public class QuanLyDanhXe : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<QuanLyDanhXe> _logger;
        private readonly IMemoryCache _cacheKey;
        // Thêm biến này vào Class Controller
        private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();

        private readonly ISystemService _sys;
        public QuanLyDanhXe(TmdtContext context, ILogger<QuanLyDanhXe> logger, IMemoryCache cache, ISystemService sys)
        {
            _context = context;
            _logger = logger;
            _cacheKey = cache;
            _sys = sys;
        }
        [HttpGet("danhsachxe")]
        public async Task<IActionResult> GetDanhSachXe([FromQuery] string? seaechTerm, [FromQuery] int page, [FromQuery] int? maLoaiXe, [FromQuery] string? status, [FromQuery] string? trangthaidangkiem)
        {
            if (page <= 0)
            {
                page = 1;
            }
            //tao key 
            string cacheKey = $"danhSachXe_{seaechTerm}_{page}_{maLoaiXe}_{status}_{trangthaidangkiem}";
            try
            {
                if (!_cacheKey.TryGetValue(cacheKey, out var cachedData))
                {
                    int pageSixe = 10;
                    var query = _context.PhuongTiens
                        //.Include(loai => loai.MaLoaiXeNavigation)
                        //.Include(dk => dk.DangKiems)
                        //.Include(kho => kho.MaKhoNavigation)
                        .AsNoTracking();

                    if (!string.IsNullOrEmpty(seaechTerm))
                    {
                        query = query.Where(x => x.BienSo != null && x.BienSo.Contains(seaechTerm) || x.MaLoaiXeNavigation.TenLoai.Contains(seaechTerm));
                    }
                    if (maLoaiXe.HasValue)
                    {
                        query = query.Where(x => x.MaLoaiXe == maLoaiXe.Value);
                    }
                    if (status != "Tất cả")
                    {
                        if (status == "Đang hoạt động")
                        {
                            query = query.Where(x => x.TrangThai == "Đang hoạt động");
                        }
                        else if (status == "Bảo trì")
                        {
                            query = query.Where(x => x.TrangThai == "Bảo trì");
                        }
                        else if (status == "Không hoạt động")
                        {
                            query = query.Where(x => x.TrangThai == "Không hoạt động");
                        }
                    }
                    if (trangthaidangkiem != "Tất cả")
                    {
                        // 1. Chuyển đổi DateTime sang DateOnly để khớp với kiểu dữ liệu trong Model
                        DateOnly today = DateOnly.FromDateTime(DateTime.Now);

                        if (trangthaidangkiem == "Còn hạn")
                        {
                            // Lọc những xe có bản ghi đăng kiểm mới nhất vẫn còn hạn
                            query = query.Where(x => x.DangKiems
                                .OrderByDescending(dk => dk.NgayHetHan)
                                .Select(dk => dk.NgayHetHan)
                                .FirstOrDefault() >= today);
                        }
                        else if (trangthaidangkiem == "Hết hạn")
                        {
                            // Lọc những xe: 
                            // - Hoặc là đã có đăng kiểm nhưng bản ghi mới nhất đã quá hạn
                            // - Hoặc là chưa bao giờ có bản ghi đăng kiểm nào
                            query = query.Where(x =>
                                !x.DangKiems.Any() ||
                                x.DangKiems.OrderByDescending(dk => dk.NgayHetHan)
                                    .Select(dk => dk.NgayHetHan)
                                    .FirstOrDefault() < today);
                        }
                    }
                    int totalItems = await query.CountAsync();
                    var danhsach = await query
                        .Include(loai => loai.MaLoaiXeNavigation)
                        .Skip((page - 1) * pageSixe).Take(pageSixe)
                        .Select(x => new PhuongTienModels
                        {

                            MaPhuongTien = x.MaPhuongTien,
                            BienSo = x.BienSo,
                            MaLoaiXe = x.MaLoaiXe,
                            TenLoaiXe = x.MaLoaiXeNavigation != null ? x.MaLoaiXeNavigation.TenLoai : "N/A",
                            TaiTrongToiDaKg = x.TaiTrongToiDaKg,
                            TheTichToiDaM3 = x.TheTichToiDaM3,
                            MucTieuHaoNhienLieu = x.MucTieuHaoNhienLieu,
                            TrangThai = x.TrangThai,
                            TenKho = x.MaKhoNavigation != null ? x.MaKhoNavigation.TenKhoBai : "Chưa xác định"

                        }).ToListAsync();
                    cachedData = new
                    {
                        TotalItems = totalItems,
                        TotalPages = (int)Math.Ceiling((double)totalItems / pageSixe),
                        CurrentPage = page,
                        PageSize = pageSixe,
                        Data = danhsach
                    };
                    _cacheKey.Set(cacheKey, cachedData, TimeSpan.FromMinutes(5));
                }
                return Ok(cachedData);
            }
            catch (SqlException ex)
            {
                // Lỗi kết nối Database
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi lấy danh sách loại xe");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
                // Các lỗi không xác định khác
                _logger.LogError(ex, "Lỗi hệ thống khi lấy danh sách loại xe với Term: {SearchTerm}", seaechTerm);
                return StatusCode(500, "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.");
            }
        }
        [HttpGet("danhsachloaixe")]
        public async Task<IActionResult> GetDanhSachLoaiXe([FromQuery] string? searchTerm, [FromQuery] int page = 1)
        {
            // 1. Validation: Kiểm tra đầu vào
            if (page <= 0) page = 1;

            // Cache key nên bao gồm cả số trang để tránh việc page 1 và page 2 trả về cùng một dữ liệu từ cache
            string cachekey = $"danhsachLoaiXe_{searchTerm ?? "all"}_p{page}";

            try
            {
                // 2. Kiểm tra Cache
                if (!_cacheKey.TryGetValue(cachekey, out var cachedData))
                {
                    int pageSize = 20;

                    // 3. Xây dựng Query với AsNoTracking để tối ưu hiệu suất đọc
                    var query = _context.LoaiXes.AsNoTracking();

                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        // Sử dụng ToLower() nếu muốn tìm kiếm không phân biệt hoa thường tùy cấu hình DB
                        query = query.Where(x => x.TenLoai != null && x.TenLoai.Contains(searchTerm));
                    }

                    int totalItems = await query.CountAsync();

                    // Kiểm tra nếu trang yêu cầu vượt quá số trang thực tế
                    int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
                    // Nếu người dùng yêu cầu trang lớn hơn tổng số trang (và tổng số trang > 0)
                    if (page > totalPages && totalPages > 0)
                    {
                        return BadRequest(new { Message = $"Trang {page} không tồn tại. Tổng số trang hiện có là {totalPages}" });
                    }

                    var danhsach = await query
                        .OrderBy(x => x.MaLoaiXe) // Luôn nên có OrderBy khi dùng Skip/Take để dữ liệu nhất quán
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(x => new LoaiXeModels
                        {
                            MaLoaiXe = x.MaLoaiXe,
                            TenLoai = x.TenLoai
                        })
                        .ToListAsync();

                    if (danhsach == null || !danhsach.Any())
                    {
                        // Trả về kết quả trống thay vì lỗi nếu không tìm thấy dữ liệu phù hợp search
                        return Ok(new { TotalItems = 0, Data = new List<LoaiXeModels>(), Message = "Không tìm thấy loại xe nào." });
                    }

                    cachedData = new
                    {
                        TotalItems = totalItems,
                        TotalPages = totalPages,
                        CurrentPage = page,
                        PageSize = pageSize,
                        Data = danhsach
                    };
                    // Lưu vào cache với thời gian hết hạn
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                        // Thêm dòng này để liên kết cache với token hủy. xóa cache khi thêm mới để tải được dữ liệu mới nhất 
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));
                    _cacheKey.Set(cachekey, cachedData, cacheOptions);


                }

                return Ok(cachedData);
            }
            catch (SqlException ex)
            {
                // Lỗi kết nối Database
                _logger.LogError(ex, "Lỗi kết nối cơ sở dữ liệu khi lấy danh sách loại xe");
                return StatusCode(503, "Dịch vụ cơ sở dữ liệu tạm thời không khả dụng");
            }
            catch (Exception ex)
            {
                // Các lỗi không xác định khác
                _logger.LogError(ex, "Lỗi hệ thống khi lấy danh sách loại xe với Term: {SearchTerm}", searchTerm);
                return StatusCode(500, "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.");
            }
        }
        [HttpPost("themxemoi")]
        public async Task<IActionResult> ThemXe([FromBody] PhuongTienModels model)
        {
            try
            {
                if (model == null)
                {
                    return BadRequest("Dữ liệu không hợp lệ");
                }
                var newXe = new PhuongTien
                {
                    BienSo = model.BienSo,
                    MaLoaiXe = model.MaLoaiXe,
                    TaiTrongToiDaKg = model.TaiTrongToiDaKg,
                    TheTichToiDaM3 = model.TheTichToiDaM3,
                    MucTieuHaoNhienLieu = model.MucTieuHaoNhienLieu,
                    TrangThai = model.TrangThai,
                    MaKho = model.MaKho
                };
                _context.PhuongTiens.Add(newXe);
                
                await _context.SaveChangesAsync();

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý phương tiện",
                    "Thêm mới phương tiện " + model.BienSo,
                    "PhuongTien",
                    "",
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>
                    {
                        {"Biển số", model.BienSo },
                        {"Loại xe", model.MaLoaiXe },                      
                        {"Trạng thái", model.TrangThai },
                        {"Kho", model.MaKho }
                    }
                );

                return Ok(new { Message = "Thêm xe mới thành công", MaPhuongTien = newXe.MaPhuongTien });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm xe mới");
                return StatusCode(500, "Đã xảy ra lỗi khi thêm xe mới");
            }
        }

        //[HttpPost("import-excel")]
        //public async Task<IActionResult> ImportPhuongTienExcel(IFormFile file)
        //{
        //    if (file == null || file.Length <= 0)
        //        return BadRequest("Vui lòng chọn file Excel.");

        //    if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        //        return BadRequest("Định dạng file không hỗ trợ. Vui lòng sử dụng file .xlsx");

        //    var listXe = new List<PhuongTien>();
        //    var errors = new List<string>();

        //    try
        //    {
        //        // Cấu hình EPPlus để sử dụng (bắt buộc với bản 5+ )
        //        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        //        using (var stream = new MemoryStream())
        //        {
        //            await file.CopyToAsync(stream);
        //            using (var package = new ExcelPackage(stream))
        //            {
        //                // Lấy Sheet đầu tiên
        //                ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
        //                var rowCount = worksheet.Dimension.Rows;

        //                // Giả sử dòng 1 là tiêu đề, dữ liệu bắt đầu từ dòng 2
        //                for (int row = 2; row <= rowCount; row++)
        //                {
        //                    try
        //                    {
        //                        var bienSo = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
        //                        if (string.IsNullOrEmpty(bienSo)) continue; // Bỏ qua dòng trống

        //                        var phuongTien = new PhuongTien
        //                        {
        //                            BienSo = bienSo,
        //                            MaLoaiXe = int.TryParse(worksheet.Cells[row, 2].Value?.ToString(), out int maLoai) ? maLoai : 0,
        //                            TaiTrongToiDaKg = double.TryParse(worksheet.Cells[row, 3].Value?.ToString(), out double taiTrong) ? taiTrong : 0,
        //                            TheTichToiDaM3 = double.TryParse(worksheet.Cells[row, 4].Value?.ToString(), out double theTich) ? theTich : 0,
        //                            MucTieuHaoNhienLieu = double.TryParse(worksheet.Cells[row, 5].Value?.ToString(), out double mucTieuHao) ? mucTieuHao : 0,
        //                            TrangThai = worksheet.Cells[row, 6].Value?.ToString() ?? "Đang hoạt động",
        //                            MaKho = int.TryParse(worksheet.Cells[row, 7].Value?.ToString(), out int maKho) ? maKho : null
        //                        };

        //                        listXe.Add(phuongTien);
        //                    }
        //                    catch (Exception ex)
        //                    {
        //                        errors.Add($"Lỗi tại dòng {row}: {ex.Message}");
        //                    }
        //                }
        //            }
        //        }

        //        if (listXe.Any())
        //        {
        //            _context.PhuongTiens.AddRange(listXe);
        //            await _context.SaveChangesAsync();

        //            // Xóa cache danh sách sau khi thêm mới dữ liệu
        //            _resetCacheToken.Cancel();
        //            _resetCacheToken.Dispose();
        //            _resetCacheToken = new CancellationTokenSource();

        //            return Ok(new
        //            {
        //                Message = $"Thêm thành công {listXe.Count} phương tiện.",
        //                Errors = errors
        //            });
        //        }

        //        return BadRequest(new { Message = "Không có dữ liệu hợp lệ để thêm.", Errors = errors });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Lỗi khi import Excel");
        //        return StatusCode(500, "Đã xảy ra lỗi trong quá trình xử lý file.");
        //    }
        //}

        [HttpPost("themloaixe")]
        public async Task<IActionResult> ThemLoaiXe([FromBody] LoaiXeModels model)
        {
            try
            {
                if (model == null)
                {
                    return BadRequest("Dữ liệu không hợp lệ");
                }
                var newLoaiXe = new LoaiXe
                {
                    TenLoai = model.TenLoai
                };
                _context.LoaiXes.Add(newLoaiXe);
                await _context.SaveChangesAsync();


                // Hủy bỏ token cũ để xóa toàn bộ các cache liên quan đến danh sách loại xe
                _resetCacheToken.Cancel();
                _resetCacheToken.Dispose();
                // Khởi tạo token mới cho các lượt cache tiếp theo
                _resetCacheToken = new CancellationTokenSource();

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý phương tiện",
                    "Thêm loại xe",
                    "LoaiXe",
                    "",
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>
                    {
                        {"Tên loại xe", model.TenLoai }
                    }
                );

                return Ok(new { Message = "Thêm loại xe mới thành công", MaLoaiXe = newLoaiXe.MaLoaiXe });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm loại xe mới");
                return StatusCode(500, "Đã xảy ra lỗi khi thêm loại xe mới");
            }
        }
        [HttpPut("capnhatxe/{maPhuongTien}")]
        public async Task<IActionResult> CapNhatXe(int maPhuongTien, [FromBody] PhuongTienModels model)
        {
            try
            {
                var existingXe = await _context.PhuongTiens.FindAsync(maPhuongTien);
                if (existingXe == null)
                {
                    return NotFound("Xe không tồn tại");
                }

                var datacu = new Dictionary<string, object>
                {
                    {"Biển số", existingXe.BienSo },
                    {"Loại xe", existingXe.MaLoaiXe },
                    {"Tai trọng tối đa (kg)", existingXe.TaiTrongToiDaKg },
                    {"Thể tích tối đa (m3)", existingXe.TheTichToiDaM3 },
                    {"Mức tiêu hao nhiên liệu", existingXe.MucTieuHaoNhienLieu },
                    {"Trạng thái", existingXe.TrangThai },
                    {"Kho", existingXe.MaKho }
                };

                existingXe.BienSo = model.BienSo;
                existingXe.MaLoaiXe = model.MaLoaiXe;
                existingXe.TaiTrongToiDaKg = model.TaiTrongToiDaKg;
                existingXe.TheTichToiDaM3 = model.TheTichToiDaM3;
                existingXe.MucTieuHaoNhienLieu = model.MucTieuHaoNhienLieu;
                existingXe.TrangThai = model.TrangThai;
                existingXe.MaKho = model.MaKho;
                await _context.SaveChangesAsync();

                var datamoi = new Dictionary<string, object>
                {
                    {"Biển số", existingXe.BienSo },
                    {"Loại xe", existingXe.MaLoaiXe },
                    {"Tai trọng tối đa (kg)", existingXe.TaiTrongToiDaKg },
                    {"Thể tích tối đa (m3)", existingXe.TheTichToiDaM3 },
                    {"Mức tiêu hao nhiên liệu", existingXe.MucTieuHaoNhienLieu },
                    {"Trạng thái", existingXe.TrangThai },
                    {"Kho", existingXe.MaKho }
                };

                var (diffCu, diffMoi) = LocThayDoi.GetChanges(datacu, datamoi);

                if (diffMoi.Count > 0)
                {
                    
                    await _sys.GhiLogVaResetCacheAsync(
                        "Quản lý phương tiện",
                        $"Cập nhật thay đổi phương tiện: {existingXe.BienSo}",
                        "PhuongTien",
                        "",
                        diffCu,
                        diffMoi
                    );
                }

                return Ok(new { Message = "Cập nhật xe thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật xe");
                return StatusCode(500, "Đã xảy ra lỗi khi cập nhật xe");
            }
        }
        //[HttpGet("chitietphuongtien/{maPhuongTien}")]
        //public async Task<IActionResult> GetChiTietXe(int maPhuongTien)
        //{
        //    string cacheKey = $"chitietphuongtien_{maPhuongTien}";
        //    try
        //    {
        //        if (!_cacheKey.TryGetValue(cacheKey, out PhuongTienModels? cachedXe))
        //        {
        //            var xeFromDb = await _context.PhuongTiens
        //                .Include(ls => ls.LichSuBaoTris)
        //                .ThenInclude(l => l.MaDinhMucNavigation)
        //                .Include(dk => dk.DangKiems)
        //                .AsNoTracking()
        //                .Where(x => x.MaPhuongTien == maPhuongTien)
        //                .Select(x => new PhuongTienModels
        //                {
        //                    MaPhuongTien = x.MaPhuongTien,
        //                    BienSo = x.BienSo,
        //                    MaLoaiXe = x.MaLoaiXe,
        //                    TenLoaiXe = x.MaLoaiXeNavigation != null ? x.MaLoaiXeNavigation.TenLoai : "N/A",
        //                    TaiTrongToiDaKg = x.TaiTrongToiDaKg,
        //                    TheTichToiDaM3 = x.TheTichToiDaM3,
        //                    MucTieuHaoNhienLieu = x.MucTieuHaoNhienLieu,
        //                    TrangThai = x.TrangThai,
        //                    TenKho = x.MaKhoNavigation != null ? x.MaKhoNavigation.TenKhoBai : "Chưa xác định",
        //                    SoKmHienTai = x.SoKmHienTai,
        //                    LichSuBaoTris = x.LichSuBaoTris.Select(ls => new LichSuBaoTriModels
        //                    {
        //                        MaBanGhi = ls.MaBanGhi,
        //                        MaPhuongTien = ls.MaPhuongTien,
        //                        ChiPhi = ls.ChiPhi,
        //                        Ngay = ls.Ngay,
        //                        LoaiBaoTri = ls.MaDinhMucNavigation.TenHangMuc,

        //                    }).ToList(),

        //                    DangKiems = x.DangKiems
        //                        .Select(dk => new DangKiemModel
        //                        {
        //                            IdDangKiem = dk.IdDangKiem,
        //                            MaPhuongTien = dk.MaPhuongTien,
        //                            SoSeriGiayPhep = dk.SoSeriGiayPhep,
        //                            SoTemKiemDinh = dk.SoTemKiemDinh,
        //                            NgayKiemDinh = dk.NgayKiemDinh,
        //                            NgayHetHan = dk.NgayHetHan,
        //                            DonViKiemDinh = dk.DonViKiemDinh,
        //                            PhiDuongBoDenNgay = dk.PhiDuongBoDenNgay,
        //                            GhiChu = dk.GhiChu,
        //                            NgayTao = dk.NgayTao,
        //                            HinhAnhDangKiem = dk.HinhAnhDangKiem

        //                        })
        //                    .ToList()
        //                })
        //                .FirstOrDefaultAsync();

        //            if (xeFromDb == null) return NotFound("Xe không tồn tại");

        //            var cacheOptions = new MemoryCacheEntryOptions()
        //                .SetSlidingExpiration(TimeSpan.FromMinutes(5))
        //                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

        //            _cacheKey.Set(cacheKey, xeFromDb, cacheOptions);
        //            cachedXe = xeFromDb;
        //        }
        //        return Ok(cachedXe);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Lỗi khi lấy chi tiết xe");
        //        return StatusCode(500, "Lỗi hệ thống");
        //    }
        //}

        [HttpGet("chitietphuongtien/{maPhuongTien}")]
        public async Task<IActionResult> GetChiTietXe(int maPhuongTien)
        {
            string cacheKey = $"chitietphuongtien_{maPhuongTien}";
            try
            {
                if (!_cacheKey.TryGetValue(cacheKey, out PhuongTienModels? cachedXe))
                {
                    // 1. Lấy thông tin xe cùng với định mức của loại xe đó
                    var xeFromDb = await _context.PhuongTiens
                        .Include(x => x.MaLoaiXeNavigation)
                            .ThenInclude(lx => lx.DinhMucBaoTris)
                        .Include(x => x.LichSuBaoTris)
                        .Include(dk => dk.DangKiems)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.MaPhuongTien == maPhuongTien);

                    if (xeFromDb == null) return NotFound("Xe không tồn tại");

                    // 2. Mapping sang Model cơ bản
                    var model = new PhuongTienModels
                    {
                        MaPhuongTien = xeFromDb.MaPhuongTien,
                        BienSo = xeFromDb.BienSo,
                        MaLoaiXe = xeFromDb.MaLoaiXe,
                        TenLoaiXe = xeFromDb.MaLoaiXeNavigation != null ? xeFromDb.MaLoaiXeNavigation.TenLoai : "N/A",
                        TaiTrongToiDaKg = xeFromDb.TaiTrongToiDaKg,
                        TheTichToiDaM3 = xeFromDb.TheTichToiDaM3,
                        MucTieuHaoNhienLieu = xeFromDb.MucTieuHaoNhienLieu,
                        TrangThai = xeFromDb.TrangThai,
                        TenKho = xeFromDb.MaKhoNavigation != null ? xeFromDb.MaKhoNavigation.TenKhoBai : "Chưa xác định",
                        SoKmHienTai = xeFromDb.SoKmHienTai,
                        MaKho = (int)xeFromDb.MaKho,
                        DangKiems = xeFromDb.DangKiems.Select(dk => new DangKiemModel
                            {
                                IdDangKiem = dk.IdDangKiem,
                                MaPhuongTien = dk.MaPhuongTien,
                                SoSeriGiayPhep = dk.SoSeriGiayPhep,
                                SoTemKiemDinh = dk.SoTemKiemDinh,
                                NgayKiemDinh = dk.NgayKiemDinh,
                                NgayHetHan = dk.NgayHetHan,
                                DonViKiemDinh = dk.DonViKiemDinh,
                                PhiDuongBoDenNgay = dk.PhiDuongBoDenNgay,
                                GhiChu = dk.GhiChu,
                                NgayTao = dk.NgayTao,
                                HinhAnhDangKiem = dk.HinhAnhDangKiem
                            })
                            .OrderByDescending(dk => dk.NgayHetHan) // Sắp xếp đăng kiểm mới nhất lên đầu
                            .ToList(),
                        LichSuBaoTris = xeFromDb.LichSuBaoTris.Select(ls => new LichSuBaoTriModels
                        {
                            
                            Ngay = ls.Ngay,
                            ChiPhi = ls.ChiPhi,
                            SoKmThucTe = ls.SoKmThucTe,
                            LoaiBaoTri = xeFromDb.MaLoaiXeNavigation?.DinhMucBaoTris
                                             .FirstOrDefault(d => d.MaDinhMuc == ls.MaDinhMuc)?.TenHangMuc ?? "N/A"
                        }).OrderByDescending(x => x.Ngay).ToList()
                    };

                    // 3. Logic tính toán các hạng mục cần bảo trì
                    DateOnly today = DateOnly.FromDateTime(DateTime.Now);
                    foreach (var dinhMuc in xeFromDb.MaLoaiXeNavigation?.DinhMucBaoTris ?? Enumerable.Empty<DinhMucBaoTri>())
                    {
                        var lastService = xeFromDb.LichSuBaoTris
                            .Where(ls => ls.MaDinhMuc == dinhMuc.MaDinhMuc)
                            .OrderByDescending(ls => ls.Ngay)
                            .FirstOrDefault();

                        bool isUrgent = false;
                        string lyDo = "";
                        DateOnly? ngayToiHan = null; // Khai báo ngoài để dùng chung cho cả khối logic

                        // A. Tính toán ngày tới hạn nếu có định mức tháng
                        if (dinhMuc.DinhMucThang.HasValue && lastService?.Ngay != null)
                        {
                            ngayToiHan = lastService.Ngay.Value.AddMonths(dinhMuc.DinhMucThang.Value);
                        }

                        // B. Kiểm tra theo KM
                        if (dinhMuc.DinhMucKm.HasValue)
                        {
                            double kmToiHan = (lastService?.SoKmThucTe ?? 0) + dinhMuc.DinhMucKm.Value;
                            if (xeFromDb.SoKmHienTai >= (kmToiHan - 500))
                            {
                                isUrgent = true;
                                lyDo = xeFromDb.SoKmHienTai >= kmToiHan ? "Quá hạn KM" : "Sắp đến hạn KM";
                            }
                        }

                        // C. Kiểm tra theo Ngày (nếu KM chưa tới nhưng ngày đã tới)
                        if (!isUrgent && ngayToiHan.HasValue)
                        {
                            if (ngayToiHan <= today.AddDays(15))
                            {
                                isUrgent = true;
                                lyDo = ngayToiHan <= today ? "Quá hạn ngày" : "Sắp đến hạn ngày";
                            }
                        }

                        // D. Nếu xe mới hoàn toàn chưa bảo trì hạng mục này bao giờ
                        if (lastService == null)
                        {
                            isUrgent = true;
                            lyDo = "Chưa được bảo trì hạng mục này lần nào";
                        }

                        if (isUrgent)
                        {
                            // Tính toán số KM thực tế còn lại
                            double? kmConLai = null;
                            if (dinhMuc.DinhMucKm.HasValue)
                            {
                                double kmDaSuDung = (double)(xeFromDb.SoKmHienTai - (lastService?.SoKmThucTe ?? 0));
                                kmConLai = dinhMuc.DinhMucKm.Value - kmDaSuDung;
                            }

                            model.DanhSachCanBaoTri.Add(new CanhBaoBaoTriModel
                            {
                                MaDinhMuc = dinhMuc.MaDinhMuc,
                                TenHangMuc = dinhMuc.TenHangMuc ?? "N/A",
                                LyDo = lyDo,
                                TrangThai = (lyDo.Contains("Quá") || lastService == null) ? "Nguy cấp" : "Cần lưu ý",
                                ConLaiKm = kmConLai,
                                NgayDuKien = ngayToiHan // Gán ngày dự kiến vào đây
                            });
                        }
                    }

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

                    _cacheKey.Set(cacheKey, model, cacheOptions);
                    cachedXe = model;
                }
                return Ok(cachedXe);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết xe");
                return StatusCode(500, "Lỗi hệ thống");
            }
        }

        // 2. API CẬP NHẬT ĐĂNG KIỂM (SỬA)
        [HttpPut("capnhatdangkiem/{id}")]
        public async Task<IActionResult> CapNhatDangKiem(int id, [FromForm] DangKiemModel model, IFormFile? hinhAnh)
        {
            try
            {
                var existingDk = await _context.DangKiems.FindAsync(id);
                if (existingDk == null) return NotFound("Không tìm thấy bản ghi đăng kiểm");

                var datacu = new Dictionary<string, object>
                {
                    {"Số seri giấy phép", existingDk.SoSeriGiayPhep },
                    {"Số tem kiểm định", existingDk.SoTemKiemDinh },
                    {"Ngày kiểm định", existingDk.NgayKiemDinh },
                    {"Ngày hết hạn", existingDk.NgayHetHan },
                    {"Đơn vị kiểm định", existingDk.DonViKiemDinh },
                    {"Phí đường bộ đến ngày", existingDk.PhiDuongBoDenNgay },
                    {"Ghi chú", existingDk.GhiChu },
                    {"Hình ảnh đăng kiểm", existingDk.HinhAnhDangKiem }
                };

                existingDk.SoSeriGiayPhep = model.SoSeriGiayPhep;
                existingDk.SoTemKiemDinh = model.SoTemKiemDinh;
                existingDk.NgayKiemDinh = model.NgayKiemDinh;
                existingDk.NgayHetHan = model.NgayHetHan;
                existingDk.DonViKiemDinh = model.DonViKiemDinh;
                existingDk.PhiDuongBoDenNgay = model.PhiDuongBoDenNgay;
                existingDk.GhiChu = model.GhiChu;

                var datamoi = new Dictionary<string, object>
                {
                    {"Số seri giấy phép", existingDk.SoSeriGiayPhep },
                    {"Số tem kiểm định", existingDk.SoTemKiemDinh },
                    {"Ngày kiểm định", existingDk.NgayKiemDinh },
                    {"Ngày hết hạn", existingDk.NgayHetHan },
                    {"Đơn vị kiểm định", existingDk.DonViKiemDinh },
                    {"Phí đường bộ đến ngày", existingDk.PhiDuongBoDenNgay },
                    {"Ghi chú", existingDk.GhiChu },
                    {"Hình ảnh đăng kiểm", existingDk.HinhAnhDangKiem }
                };

                if (hinhAnh != null && hinhAnh.Length > 0)
                {
                    string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    string path = Path.Combine(wwwRootPath, "uploads", "dangkiem");
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                    // Xóa ảnh cũ nếu có (tùy chọn)
                    // if (!string.IsNullOrEmpty(existingDk.HinhAnhDangKiem)) { ... }

                    string fileName = $"DK_Edit_{id}_{Guid.NewGuid()}{Path.GetExtension(hinhAnh.FileName)}";
                    using (var fileStream = new FileStream(Path.Combine(path, fileName), FileMode.Create))
                    {
                        await hinhAnh.CopyToAsync(fileStream);
                    }
                    existingDk.HinhAnhDangKiem = "/uploads/dangkiem/" + fileName;
                }

                _context.DangKiems.Update(existingDk);
                await _context.SaveChangesAsync();

                // Clear cache
                _cacheKey.Remove($"chitietphuongtien_{existingDk.MaPhuongTien}");

                var (diffCu, diffMoi) = LocThayDoi.GetChanges(datacu, datamoi);

                // --- BƯỚC 5: GHI LOG VÀ RESET CACHE ---
                if (diffMoi.Count > 0)
                {
                   

                    await _sys.GhiLogVaResetCacheAsync(
                        "Quản lý phương tiện",
                        $"Cập nhật thay đăng kiểm xe ",
                        "QuanLyPhuongTien",
                        "",
                        diffCu,
                        diffMoi
                    );
                }

                return Ok(new { Message = "Cập nhật đăng kiểm thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật đăng kiểm");
                return StatusCode(500, "Lỗi hệ thống khi cập nhật");
            }
        }
        [HttpGet("xe-sap-den-han-bao-tri")]
        public async Task<IActionResult> GetXeSapDenHanBaoTri([FromQuery] int soNgayCanhBao = 15, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                DateTime todayDt = DateTime.Now;
                DateOnly today = DateOnly.FromDateTime(todayDt);
                DateOnly deadlineNgay = today.AddDays(soNgayCanhBao);

                // 1. Lấy dữ liệu thô từ DB (Tránh xử lý logic quá phức tạp trong SQL)
                var query = _context.PhuongTiens
                    .Include(x => x.MaLoaiXeNavigation)
                        .ThenInclude(lx => lx.DinhMucBaoTris)
                    .Include(x => x.LichSuBaoTris)
                    .AsNoTracking();

                var allVehicles = await query.ToListAsync();

                // 2. Xử lý logic nghiệp vụ trên Memory
                var resultData = allVehicles.Select(xe =>
                {
                    // Danh sách các hạng mục cần bảo trì của xe này
                    var hangMucCanBaoTri = new List<string>();
                    bool isUrgent = false;

                    foreach (var dinhMuc in xe.MaLoaiXeNavigation.DinhMucBaoTris)
                    {
                        // Tìm lần bảo trì cuối cùng của RIÊNG hạng mục này
                        var lastServiceForThisItem = xe.LichSuBaoTris
                            .Where(ls => ls.MaDinhMuc == dinhMuc.MaDinhMuc)
                            .OrderByDescending(ls => ls.Ngay)
                            .FirstOrDefault();

                        if (lastServiceForThisItem == null)
                        {
                            hangMucCanBaoTri.Add($"Mới: {dinhMuc.TenHangMuc}");
                            isUrgent = true;
                            continue;
                        }

                        // Kiểm tra theo KM
                        bool denHanKm = false;
                        if (dinhMuc.DinhMucKm.HasValue)
                        {
                            double kmToiHan = (lastServiceForThisItem.SoKmThucTe ?? 0) + dinhMuc.DinhMucKm.Value;
                            if (xe.SoKmHienTai >= (kmToiHan - 500)) // Cảnh báo trước 500km
                            {
                                denHanKm = true;
                            }
                        }

                        // Kiểm tra theo Ngày
                        bool denHanNgay = false;
                        if (dinhMuc.DinhMucThang.HasValue && lastServiceForThisItem.Ngay.HasValue)
                        {
                            DateOnly ngayToiHan = lastServiceForThisItem.Ngay.Value.AddMonths(dinhMuc.DinhMucThang.Value);
                            if (ngayToiHan <= deadlineNgay)
                            {
                                denHanNgay = true;
                            }
                        }

                        if (denHanKm || denHanNgay)
                        {
                            string lyDo = denHanKm ? "hết KM" : "hết hạn ngày";
                            hangMucCanBaoTri.Add($"{dinhMuc.TenHangMuc} ({lyDo})");
                        }
                    }

                    return new
                    {
                        PhuongTien = xe,
                        HangMucCanBaoTri = hangMucCanBaoTri,
                        SapDenHan = hangMucCanBaoTri.Any()
                    };
                })
                .Where(x => x.SapDenHan) // Chỉ lấy những xe có ít nhất 1 hạng mục đến hạn
                .ToList();

                // 3. Phân trang
                int totalItems = resultData.Count;
                var pagedData = resultData
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new
                    {
                        MaPhuongTien = x.PhuongTien.MaPhuongTien,
                        BienSo = x.PhuongTien.BienSo,
                        TenLoaiXe = x.PhuongTien.MaLoaiXeNavigation?.TenLoai ?? "N/A",
                        SoKmHienTai = x.PhuongTien.SoKmHienTai,
                        TrangThai = x.PhuongTien.TrangThai,
                        CacHangMucToiHan = x.HangMucCanBaoTri, // Trả về mảng các hạng mục
                        GhiChu = string.Join(", ", x.HangMucCanBaoTri)
                    })
                    .ToList();

                return Ok(new { TotalItems = totalItems, Page = page, PageSize = pageSize, Data = pagedData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }
        [HttpGet("xe-sap-het-han-dang-kiem")]
        public async Task<IActionResult> GetXeHetHanDangKiem([FromQuery] int soNgayCanhBao = 30, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                DateOnly today = DateOnly.FromDateTime(DateTime.Now);
                DateOnly deadline = today.AddDays(soNgayCanhBao);

                var query = _context.PhuongTiens
                    .Include(x => x.MaLoaiXeNavigation)
                    .AsNoTracking()
                    .Select(x => new
                    {
                        PhuongTien = x,
                        NgayHetHanMoiNhat = x.DangKiems
                                            .OrderByDescending(dk => dk.NgayHetHan)
                                            .Select(dk => dk.NgayHetHan)
                                            .FirstOrDefault()
                    })
                    .Where(x => x.NgayHetHanMoiNhat != null && x.NgayHetHanMoiNhat <= deadline);

                // 1. Tính tổng số xe thỏa mãn điều kiện
                int totalItems = await query.CountAsync();

                // 2. Thực hiện lấy dữ liệu trang hiện tại
                var data = await query
                    .OrderBy(x => x.NgayHetHanMoiNhat)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new
                    {
                        MaPhuongTien = x.PhuongTien.MaPhuongTien,
                        BienSo = x.PhuongTien.BienSo,
                        TenLoaiXe = x.PhuongTien.MaLoaiXeNavigation != null ? x.PhuongTien.MaLoaiXeNavigation.TenLoai : "N/A",
                        TrangThai = x.PhuongTien.TrangThai,
                        GhiChu = x.NgayHetHanMoiNhat < today ? $"ĐÃ HẾT HẠN: {x.NgayHetHanMoiNhat:dd/MM/yyyy}" : $"Sắp hết hạn: {x.NgayHetHanMoiNhat:dd/MM/yyyy}"
                    })
                    .ToListAsync();

                return Ok(new { TotalItems = totalItems, Page = page, PageSize = pageSize, Data = data });
            }
            catch (Exception) { return StatusCode(500, "Lỗi hệ thống"); }
        }
        [HttpGet("xe-sap-het-han-phi-duong-bo")]
        public async Task<IActionResult> GetXeSapHetHanPhiDuongBo([FromQuery] int soNgayCanhBao = 30, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                DateTime todayDt = DateTime.Today;
                DateOnly today = DateOnly.FromDateTime(todayDt);
                DateOnly deadline = today.AddDays(soNgayCanhBao);

                var query = _context.PhuongTiens
                    .Include(x => x.MaLoaiXeNavigation)
                    .AsNoTracking()
                    .Select(x => new
                    {
                        PhuongTien = x,
                        PhiDuongBoMoiNhat = x.DangKiems
                                            .OrderByDescending(dk => dk.NgayHetHan)
                                            .Select(dk => (DateOnly?)dk.PhiDuongBoDenNgay)
                                            .FirstOrDefault()
                    })
                    .Where(x => x.PhiDuongBoMoiNhat != null && x.PhiDuongBoMoiNhat <= deadline);

                // 1. Tính tổng số xe (tất cả các trang)
                int totalItems = await query.CountAsync();

                // 2. Lấy dữ liệu và tính toán số ngày còn lại
                var rawData = await query
                    .OrderBy(x => x.PhiDuongBoMoiNhat)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var data = rawData.Select(x =>
                {
                    var ngayHetHan = x.PhiDuongBoMoiNhat.Value.ToDateTime(TimeOnly.MinValue);
                    int soNgayConLai = (ngayHetHan - todayDt).Days;

                    return new
                    {
                        MaPhuongTien = x.PhuongTien.MaPhuongTien,
                        BienSo = x.PhuongTien.BienSo,
                        TenLoaiXe = x.PhuongTien.MaLoaiXeNavigation?.TenLoai ?? "N/A",
                        TrangThai = x.PhuongTien.TrangThai,
                        // Hiển thị cụ thể "Quá hạn" hoặc "Còn X ngày"
                        GhiChu = soNgayConLai < 0 ? $"Quá hạn: {x.PhiDuongBoMoiNhat:dd/MM/yyyy}" : $"Còn {soNgayConLai} ngày ({x.PhiDuongBoMoiNhat:dd/MM/yyyy})"
                    };
                }).ToList();

                return Ok(new { TotalItems = totalItems, Page = page, PageSize = pageSize, Data = data });
            }
            catch (Exception) { return StatusCode(500, "Lỗi hệ thống"); }
        }

        [HttpGet("export-bao-cao-xe")]
        public async Task<IActionResult> ExportBaoCaoXe([FromQuery] string loaiBaoCao, [FromQuery] int soNgayCanhBao = 30)
        {
            try
            {
                // 1. Khởi tạo cấu hình Excel
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                DateOnly today = DateOnly.FromDateTime(DateTime.Now);
                DateOnly deadline = today.AddDays(soNgayCanhBao);

                // 2. Lấy dữ liệu từ Database
                var query = _context.PhuongTiens
                    .Include(x => x.MaLoaiXeNavigation)
                        .ThenInclude(lx => lx.DinhMucBaoTris)
                    .Include(x => x.LichSuBaoTris)
                    .Include(x => x.DangKiems)
                    .AsNoTracking();

                var allVehicles = await query.ToListAsync();
                string title = "BÁO CÁO PHƯƠNG TIỆN";
                var exportData = new List<dynamic>();

                // 3. Xử lý logic theo loại báo cáo
                if (loaiBaoCao == "bao-tri")
                {
                    title = $"DANH SÁCH CHI TIẾT HẠNG MỤC CẦN BẢO TRÌ (DỰ KIẾN ĐẾN {deadline:dd/MM/yyyy})";

                    foreach (var xe in allVehicles)
                    {
                        var dinhMucs = xe.MaLoaiXeNavigation?.DinhMucBaoTris ?? Enumerable.Empty<DinhMucBaoTri>();
                        foreach (var dm in dinhMucs)
                        {
                            var lastSvc = xe.LichSuBaoTris
                                .Where(ls => ls.MaDinhMuc == dm.MaDinhMuc)
                                .OrderByDescending(ls => ls.Ngay)
                                .FirstOrDefault();

                            // Tính toán thời hạn dự kiến
                            DateOnly? ngayToiHan = (dm.DinhMucThang.HasValue && lastSvc?.Ngay != null)
                                ? lastSvc.Ngay.Value.AddMonths(dm.DinhMucThang.Value)
                                : null;

                            double? kmToiHan = (dm.DinhMucKm.HasValue)
                                ? (double)((lastSvc?.SoKmThucTe ?? 0) + dm.DinhMucKm.Value)
                                : null;

                            // Điều kiện lọc cảnh báo
                            bool denHanKm = kmToiHan.HasValue && (xe.SoKmHienTai >= (kmToiHan.Value - 500));
                            bool denHanNgay = ngayToiHan.HasValue && (ngayToiHan.Value <= deadline);
                            bool chuaTungBT = lastSvc == null;

                            if (denHanKm || denHanNgay || chuaTungBT)
                            {
                                string tinhTrang = "";
                                if (chuaTungBT) tinhTrang = "Chưa được bảo trì lần nào";
                                else if (denHanKm && xe.SoKmHienTai >= kmToiHan) tinhTrang = "QUÁ HẠN KM";
                                else if (denHanNgay && ngayToiHan <= today) tinhTrang = "QUÁ HẠN NGÀY";
                                else tinhTrang = "Sắp đến hạn";

                                exportData.Add(new
                                {
                                    BienSo = xe.BienSo,
                                    LoaiXe = xe.MaLoaiXeNavigation?.TenLoai ?? "N/A",
                                    KmHienTai = xe.SoKmHienTai,
                                    HangMuc = dm.TenHangMuc,
                                    NgayCuoi = lastSvc?.Ngay?.ToString("dd/MM/yyyy") ?? "N/A",
                                    HanDinhMuc = ngayToiHan?.ToString("dd/MM/yyyy") ?? (kmToiHan.HasValue ? $"{kmToiHan:N0} km" : "N/A"),
                                    TrangThai = tinhTrang
                                });
                            }
                        }
                    }
                    await _sys.GhiLogVaResetCacheAsync(
                        "Quản lý phương tiện",
                        $"Xuất báo cáo bảo trì xe",
                        "QuanLyPhuongTien",
                        "",
                        new Dictionary<string, object>(),
                        new Dictionary<string, object>()
                    );
                }
                else if (loaiBaoCao == "dang-kiem" || loaiBaoCao == "phi-duong-bo")
                {
                    bool isDK = loaiBaoCao == "dang-kiem";
                    title = isDK ? $"BÁO CÁO HẠN ĐĂNG KIỂM (DỰ KIẾN ĐẾN {deadline:dd/MM/yyyy})"
                                 : $"BÁO CÁO PHÍ ĐƯỜNG BỘ (DỰ KIẾN ĐẾN {deadline:dd/MM/yyyy})";

                    foreach (var xe in allVehicles)
                    {
                        var dkMoiNhat = xe.DangKiems.OrderByDescending(d => d.NgayHetHan).FirstOrDefault();
                        DateOnly? ngayCheck = isDK ? dkMoiNhat?.NgayHetHan : dkMoiNhat?.PhiDuongBoDenNgay;

                        if (ngayCheck == null || ngayCheck <= deadline)
                        {
                            exportData.Add(new
                            {
                                BienSo = xe.BienSo,
                                LoaiXe = xe.MaLoaiXeNavigation?.TenLoai ?? "N/A",
                                KmHienTai = xe.SoKmHienTai,
                                HangMuc = isDK ? "Đăng kiểm" : "Phí đường bộ",
                                NgayCuoi = isDK ? dkMoiNhat?.NgayHetHan.ToString("dd/MM/yyyy") : "N/A",
                                HanDinhMuc = ngayCheck?.ToString("dd/MM/yyyy") ?? "Chưa có dữ liệu",
                                TrangThai = (ngayCheck == null) ? "Chưa có dữ liệu" : (ngayCheck <= today ? "QUÁ HẠN" : "Sắp hết hạn")
                            });
                        }
                    }
                    await _sys.GhiLogVaResetCacheAsync(
                       "Quản lý phương tiện",
                       $"Xuất báo cáo đăng kiểm",
                       "QuanLyPhuongTien",
                       "",
                       new Dictionary<string, object>(),
                       new Dictionary<string, object>()
                   );
                }

                // 4. Tạo File Excel
                using (var package = new ExcelPackage())
                {
                    var ws = package.Workbook.Worksheets.Add("BaoCaoChiTiet");

                    // Style tiêu đề lớn
                    ws.Cells["A1:G1"].Merge = true;
                    ws.Cells["A1"].Value = title;
                    ws.Cells["A1"].Style.Font.Size = 16;
                    ws.Cells["A1"].Style.Font.Bold = true;
                    ws.Cells["A1"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    ws.Cells["A1"].Style.Font.Color.SetColor(System.Drawing.Color.DarkBlue);

                    // Header bảng
                    string[] headers = { "STT", "Biển Số", "Loại Xe", "Hạng Mục", "Lần Cuối", "Hạn Định Mức", "Tình Trạng" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = ws.Cells[2, i + 1];
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 32, 96)); // Dark Blue
                        cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    }

                    // Đổ dữ liệu vào hàng
                    int row = 3;
                    foreach (var item in exportData)
                    {
                        ws.Cells[row, 1].Value = row - 2;
                        ws.Cells[row, 2].Value = item.BienSo;
                        ws.Cells[row, 3].Value = item.LoaiXe;
                        ws.Cells[row, 4].Value = item.HangMuc;
                        ws.Cells[row, 5].Value = item.NgayCuoi;
                        ws.Cells[row, 6].Value = item.HanDinhMuc;
                        ws.Cells[row, 7].Value = item.TrangThai;

                        // Căn giữa STT và Biển số
                        ws.Cells[row, 1, row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                        // Highlight dòng cảnh báo
                        if (item.TrangThai.Contains("QUÁ") || item.TrangThai.Contains("Chưa"))
                        {
                            ws.Cells[row, 7].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                            ws.Cells[row, 7].Style.Font.Bold = true;
                        }

                        // Vẽ khung border cho hàng
                        ws.Cells[row, 1, row, 7].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        row++;
                    }

                    ws.Cells.AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    string fileName = $"BaoCao_{loaiBaoCao}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                    return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xuất báo cáo xe");
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }

        [HttpPost("import-bao-tri-dang-kiem")]
        public async Task<IActionResult> ImportThongTinXe(IFormFile file)
        {
            // 1. Kiểm tra file đầu vào
            if (file == null || file.Length == 0)
                return BadRequest("Vui lòng chọn file Excel để tải lên.");

            if (!Path.GetExtension(file.FileName).Contains(".xls"))
                return BadRequest("Chỉ hỗ trợ định dạng file Excel (.xls, .xlsx).");

            var errors = new List<string>();
            int successCount = 0;

            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // --- TỐI ƯU HIỆU NĂNG: CACHING DỮ LIỆU ---
                // Lấy toàn bộ xe và định mức bảo trì vào bộ nhớ một lần duy nhất
                var allPhuongTien = await _context.PhuongTiens
                    .Include(x => x.MaLoaiXeNavigation)
                        .ThenInclude(lx => lx.DinhMucBaoTris)
                    .ToListAsync();

                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var ws = package.Workbook.Worksheets.FirstOrDefault();
                        if (ws == null || ws.Dimension == null) return BadRequest("File Excel không có dữ liệu.");

                        int rowCount = ws.Dimension.Rows;
                        int colCount = ws.Dimension.Columns;

                        // 2. NHẬN DIỆN CỘT ĐỘNG (Dynamic Mapping)
                        var columnMap = new Dictionary<string, int>();
                        for (int col = 1; col <= colCount; col++)
                        {
                            string header = ws.Cells[1, col].Text.Trim().ToLower();
                            if (!string.IsNullOrEmpty(header)) columnMap[header] = col;
                        }

                        // Helper để lấy dữ liệu theo từ khóa
                        string GetValue(int r, params string[] keywords)
                        {
                            foreach (var kw in keywords)
                            {
                                var matchedKey = columnMap.Keys.FirstOrDefault(k => k.Contains(kw.ToLower()));
                                if (matchedKey != null) return ws.Cells[r, columnMap[matchedKey]].Text.Trim();
                            }
                            return string.Empty;
                        }

                        // 3. VÒNG LẶP XỬ LÝ DỮ LIỆU
                        for (int row = 2; row <= rowCount; row++)
                        {
                            string bienSo = GetValue(row, "biển số", "bks", "bien so");
                            string loaiThongTin = GetValue(row, "loại thông tin", "phân loại").ToLower();
                            string ngayThucHienStr = GetValue(row, "ngày thực hiện", "ngày làm");
                            string ngayHetHanStr = GetValue(row, "ngày hết hạn", "hạn kiểm định", "đến ngày");
                            string tenHangMuc = GetValue(row, "tên hạng mục", "hạng mục");
                            string soKmStr = GetValue(row, "số km", "kilometer");
                            string chiPhiStr = GetValue(row, "chi phí", "giá tiền", "số tiền");

                            if (string.IsNullOrEmpty(bienSo)) continue;

                            // Tìm xe từ Cache thay vì DB
                            var xe = allPhuongTien.FirstOrDefault(x => x.BienSo?.Replace("-", "").Replace(".", "") == bienSo.Replace("-", "").Replace(".", ""));
                            if (xe == null)
                            {
                                errors.Add($"Dòng {row}: Không tìm thấy xe '{bienSo}' trong hệ thống.");
                                continue;
                            }

                            // Parse ngày tháng (Hỗ trợ đa định dạng)
                            DateTime tempDate;
                            DateOnly? ngayThucHien = DateTime.TryParse(ngayThucHienStr, out tempDate) ? DateOnly.FromDateTime(tempDate) : null;
                            DateOnly? ngayHetHan = DateTime.TryParse(ngayHetHanStr, out tempDate) ? DateOnly.FromDateTime(tempDate) : null;

                            if (ngayThucHien == null)
                            {
                                errors.Add($"Dòng {row}: Ngày thực hiện '{ngayThucHienStr}' không hợp lệ.");
                                continue;
                            }

                            // --- PHÂN NHÁNH XỬ LÝ ---

                            // A. XỬ LÝ BẢO TRÌ
                            if (loaiThongTin.Contains("bảo trì"))
                            {
                                var dinhMuc = xe.MaLoaiXeNavigation?.DinhMucBaoTris
                                    .FirstOrDefault(dm => dm.TenHangMuc?.Trim().ToLower() == tenHangMuc.Trim().ToLower());

                                if (dinhMuc == null)
                                {
                                    errors.Add($"Dòng {row}: Hạng mục '{tenHangMuc}' không thuộc định mức của loại xe {xe.MaLoaiXeNavigation?.TenLoai}.");
                                    continue;
                                }

                                decimal.TryParse(chiPhiStr, out decimal chiPhi);
                                double.TryParse(soKmStr, out double soKm);

                                var newLichSu = new LichSuBaoTri
                                {
                                    MaPhuongTien = xe.MaPhuongTien,
                                    MaDinhMuc = dinhMuc.MaDinhMuc,
                                    Ngay = ngayThucHien,
                                    SoKmThucTe = soKm > 0 ? soKm : (double?)null,
                                    ChiPhi = chiPhi > 0 ? chiPhi : (decimal?)null
                                };
                                _context.LichSuBaoTris.Add(newLichSu);

                                // Cập nhật số Km hiện tại cho xe nếu số mới lớn hơn
                                if (soKm > (xe.SoKmHienTai ?? 0)) xe.SoKmHienTai = soKm;
                            }

                            // B. XỬ LÝ ĐĂNG KIỂM / PHÍ ĐƯỜNG BỘ
                            else if (loaiThongTin.Contains("đăng kiểm") || loaiThongTin.Contains("đường bộ"))
                            {
                                if (ngayHetHan == null)
                                {
                                    errors.Add($"Dòng {row}: Loại '{loaiThongTin}' yêu cầu phải có 'Ngày hết hạn'.");
                                    continue;
                                }

                                // Tìm bản ghi đăng kiểm trùng ngày hoặc tạo mới
                                var dangKiem = await _context.DangKiems
                                    .FirstOrDefaultAsync(dk => dk.MaPhuongTien == xe.MaPhuongTien && dk.NgayKiemDinh == ngayThucHien);

                                if (dangKiem == null)
                                {
                                    dangKiem = new DangKiem
                                    {
                                        MaPhuongTien = xe.MaPhuongTien,
                                        NgayKiemDinh = ngayThucHien.Value,
                                        SoSeriGiayPhep = "AUTO-" + DateTime.Now.Ticks, // Tránh lỗi null DB
                                        NgayTao = DateTime.Now
                                    };
                                    _context.DangKiems.Add(dangKiem);
                                }

                                if (loaiThongTin.Contains("đăng kiểm"))
                                    dangKiem.NgayHetHan = ngayHetHan.Value;
                                else
                                    dangKiem.PhiDuongBoDenNgay = ngayHetHan;
                            }
                            else
                            {
                                errors.Add($"Dòng {row}: Không xác định được loại thông tin '{loaiThongTin}'.");
                                continue;
                            }

                            successCount++;
                        }

                        // 4. LƯU DATABASE & GHI LOG
                        if (successCount > 0)
                        {
                            await _context.SaveChangesAsync();

                            await _sys.GhiLogVaResetCacheAsync(
                                "Quản lý phương tiện",
                                $"Import thành công {successCount} dòng bảo trì/đăng kiểm",
                                "PhuongTien", "", new Dictionary<string, object>(), new Dictionary<string, object>()
                            );
                        }

                        return Ok(new
                        {
                            Message = "Hoàn tất xử lý file.",
                            SuccessCount = successCount,
                            ErrorCount = errors.Count,
                            Errors = errors
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }
        [HttpPost("hoan-thanh-bao-tri")]
        public async Task<IActionResult> HoanThanhBaoTri([FromBody] HoanThanhBaoTriRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var xe = await _context.PhuongTiens.FindAsync(request.MaPhuongTien);
                if (xe == null) return NotFound();

                // 1. Lưu chi tiết vào bảng lịch sử
                foreach (var item in request.ChiPhiChiTiet)
                {
                    var lichSu = new LichSuBaoTri
                    {
                        MaPhuongTien = request.MaPhuongTien,
                        MaDinhMuc = item.MaDinhMuc,
                        Ngay = DateOnly.FromDateTime(request.NgayBaoTri),
                        SoKmThucTe = request.SoKmThucTe,
                        ChiPhi = item.ChiPhi
                    };
                    _context.LichSuBaoTris.Add(lichSu);
                }

                // 2. Cập nhật Odometer và trạng thái xe
                xe.SoKmHienTai = (double)Math.Max(xe.SoKmHienTai ?? 0, request.SoKmThucTe);
                xe.TrangThai = "Đang hoạt động";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _cacheKey.Remove($"chitietphuongtien_{request.MaPhuongTien}");
                // 3. Ghi Log chi tiết hạng mục đã bảo trì
                // Tạo một chuỗi mô tả danh sách ID hạng mục và số tiền tương ứng
                // 1. Lấy danh sách ID từ request
                var listMaDinhMuc = request.ChiPhiChiTiet.Select(x => x.MaDinhMuc).ToList();

                // 2. Truy vấn tên hạng mục từ DB (chỉ lấy những cột cần thiết để tối ưu hiệu năng)
                var danhMucNames = await _context.DinhMucBaoTris // Giả sử bảng của bạn tên là DinhMucBaoTris
                    .Where(dm => listMaDinhMuc.Contains(dm.MaDinhMuc))
                    .Select(dm => new { dm.MaDinhMuc, dm.TenHangMuc }) // Lấy cặp ID và Tên
                    .ToListAsync();

                // 3. Kết hợp dữ liệu để tạo chuỗi Log có Tên hạng mục
                var chiTietLog = request.ChiPhiChiTiet
                    .Select(item => {
                        var tenHangMuc = danhMucNames.FirstOrDefault(d => d.MaDinhMuc == item.MaDinhMuc)?.TenHangMuc ?? "N/A";
                        return $"{tenHangMuc} ({item.ChiPhi:N0}đ)";
                    })
                    .ToList();

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý phương tiện",
                    $"Hoàn thành bảo trì xe {xe.BienSo} - Tổng {request.ChiPhiChiTiet.Count} hạng mục",
                    "QuanLyPhuongTien",
                    "Update", // Thêm hành động cụ thể nếu hàm yêu cầu
                    new Dictionary<string, object>
                    {
                        { "Biển số", xe.BienSo },
                        
                        { "Danh sách hạng mục", string.Join(", ", chiTietLog) }, // Lưu vết danh sách hạng mục
                        { "Tổng chi phí", request.ChiPhiChiTiet.Sum(x => x.ChiPhi) },
                        { "SoKmCapNhat", request.SoKmThucTe }
                    },
                    new Dictionary<string, object>()
                );

                return Ok(new { message = "Thành công" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("xe-san-sang-dieu-phoi")]
        public async Task<IActionResult> GetXeSanSang([FromQuery] double khoiLuongHang, [FromQuery] int maKho)
        {
            // 1. Tạo Cache Key dựa trên mã kho và khối lượng hàng (làm tròn để tăng hiệu quả cache)
            // Ví dụ: Đơn hàng 1.2 tấn và 1.3 tấn có thể dùng chung kết quả lọc nếu ta làm tròn lên 0.5
            string cacheKey = $"dispatch_vehicles_k{maKho}_w{Math.Ceiling(khoiLuongHang)}";

            try
            {
                // 2. Thử lấy dữ liệu từ Cache
                if (!_cacheKey.TryGetValue(cacheKey, out var cachedXe))
                {
                    _logger.LogInformation("Cache miss cho điều phối tại kho {MaKho}", maKho);

                    // 3. Truy vấn Database nếu không có cache
                    var query = _context.PhuongTiens
                        .AsNoTracking()
                        .Where(x => x.TrangThai == "Không hoạt động"
                                 && x.MaKho == maKho
                                 && x.TaiTrongToiDaKg >= khoiLuongHang);

                    var xePhuHop = await query
                        .Select(x => new
                        {
                            x.MaPhuongTien,
                            x.BienSo,
                            x.TaiTrongToiDaKg,
                            x.TheTichToiDaM3, // Thêm thể tích để điều phối chính xác hơn
                            TenLoaiXe = x.MaLoaiXeNavigation != null ? x.MaLoaiXeNavigation.TenLoai : "N/A",
                            TenKho = x.MaKhoNavigation != null ? x.MaKhoNavigation.TenKhoBai : "N/A"
                        })
                        .ToListAsync();

                    // 4. Thiết lập Options cho Cache
                    var cacheOptions = new MemoryCacheEntryOptions()
                        // Thời gian cache ngắn (2 phút) vì xe di chuyển liên tục
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(2))
                        // Xóa cache ngay lập tức nếu có lệnh reset (khi thêm xe/đổi trạng thái xe)
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));

                    _cacheKey.Set(cacheKey, xePhuHop, cacheOptions);
                    cachedXe = xePhuHop;
                }

                return Ok(cachedXe);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách xe điều phối tại kho {MaKho}", maKho);
                return StatusCode(500, "Lỗi hệ thống khi điều phối lộ trình");
            }
        }
        [HttpPost("cap-nhat-trang-thai-xe/{maPhuongTien}")]
        public async Task<IActionResult> UpdateTrangThaiXe(int maPhuongTien, [FromBody] UpdateTrangThaiXeDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TrangThai))
            {
                return BadRequest("Trạng thái không hợp lệ.");
            }

            try
            {
                // Sử dụng FirstOrDefaultAsync hoặc FindAsync
                var existingXe = await _context.PhuongTiens.FindAsync(maPhuongTien);

                if (existingXe == null)
                {
                    return NotFound($"Không tìm thấy phương tiện với mã {maPhuongTien}");
                }

                // Chỉ cập nhật duy nhất trường trạng thái
                if (existingXe.TrangThai != model.TrangThai)
                {
                    existingXe.TrangThai = model.TrangThai;

                    // Nếu bạn có dùng Cache cho danh sách xe, hãy xóa nó ở đây
                    // _cache.Remove("DanhSachXe_Key");

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Phương tiện {ID} đã chuyển sang trạng thái: {Status}",
                        maPhuongTien, model.TrangThai);
                }

                return Ok(new
                {
                    Success = true,
                    Message = "Cập nhật trạng thái thành công",
                    NewStatus = model.TrangThai
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật trạng thái xe {ID}", maPhuongTien);
                return StatusCode(500, "Lỗi hệ thống khi cập nhật trạng thái");
            }
        }
    }
}