using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using OfficeOpenXml;
using QuanLyKho.Models;
using QuanLyKho.Models1.QuanLyXe;
using System.ComponentModel;
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
        public QuanLyDanhXe(TmdtContext context, ILogger<QuanLyDanhXe> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cacheKey = cache;
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
                existingXe.BienSo = model.BienSo;
                existingXe.MaLoaiXe = model.MaLoaiXe;
                existingXe.TaiTrongToiDaKg = model.TaiTrongToiDaKg;
                existingXe.TheTichToiDaM3 = model.TheTichToiDaM3;
                existingXe.MucTieuHaoNhienLieu = model.MucTieuHaoNhienLieu;
                existingXe.TrangThai = model.TrangThai;
                existingXe.MaKho = model.MaKho;
                await _context.SaveChangesAsync();
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

                existingDk.SoSeriGiayPhep = model.SoSeriGiayPhep;
                existingDk.SoTemKiemDinh = model.SoTemKiemDinh;
                existingDk.NgayKiemDinh = model.NgayKiemDinh;
                existingDk.NgayHetHan = model.NgayHetHan;
                existingDk.DonViKiemDinh = model.DonViKiemDinh;
                existingDk.PhiDuongBoDenNgay = model.PhiDuongBoDenNgay;
                existingDk.GhiChu = model.GhiChu;

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
    }
}