using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1.QuanLyHopDong;

namespace QuanLyKhachHang.ControllersAPI
{
    [Route("api/quanlyhopdong")]
    [ApiController]
    public class QuanLyHopDong : ControllerBase
    {
        private readonly ILogger<QuanLyHopDong> _logger;
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;

        // Prefix để quản lý tất cả cache liên quan đến hợp đồng
        private const string ContractCachePrefix = "ContractList_";

        // Token để xóa toàn bộ cache thuộc prefix này khi có thay đổi dữ liệu
        private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();

        public QuanLyHopDong(ILogger<QuanLyHopDong> logger, TmdtContext context, IMemoryCache cache)
        {
            _logger = logger;
            _context = context;
            _cache = cache;
        }

        [HttpGet("danhsachhopdong")]
        public async Task<IActionResult> LayDanhSachHopDong(
             [FromQuery] string? Search,
             [FromQuery] DateTime? thoiGianBD,
             [FromQuery] DateTime? thoiGianKT,
             [FromQuery] string? trangThai = "Tất cả",
             [FromQuery] int page = 1)
        {
            // Tạo Cache Key duy nhất dựa trên các tham số lọc
            string cacheKey = $"{ContractCachePrefix}{Search}_{thoiGianBD}_{thoiGianKT}_{trangThai}_{page}";

            if (!_cache.TryGetValue(cacheKey, out object cachedData))
            {
                try
                {
                    int pageSize = 10;
                    var query = _context.HopDongVanChuyens
                        .Include(kh => kh.MaKhachHangNavigation)
                        .AsNoTracking().AsQueryable();

                    if (!string.IsNullOrWhiteSpace(Search))
                    {
                        string searchLower = Search.Trim().ToLower();
                        query = query.Where(h =>
                            (h.TenHopDong != null && h.TenHopDong.ToLower().Contains(searchLower)) ||
                            (h.LoaiHangHoa != null && h.LoaiHangHoa.ToLower().Contains(searchLower))
                        );
                    }
                    if (trangThai != null && trangThai != "Tất cả")
                    {
                        query = query.Where(h => h.TrangThai == trangThai);
                    }
                    if (thoiGianBD.HasValue) query = query.Where(h => h.NgayKy >= thoiGianBD);
                    if (thoiGianKT.HasValue) query = query.Where(h => h.NgayKy <= thoiGianKT);
                    var now = DateTime.Now;
                    // Tìm các hợp đồng có Ngày Hết Hạn nhỏ hơn hiện tại nhưng trạng thái vẫn chưa là "Hết hiệu lực" (hoặc mã trạng thái tương ứng)
                    var expiredContracts = await _context.HopDongVanChuyens
                        .Where(h => h.NgayHetHan < now && h.TrangThai != "Hết hiệu lực")
                        .ToListAsync();

                    if (expiredContracts.Any())
                    {
                        foreach (var contract in expiredContracts)
                        {
                            contract.TrangThai = "Hết hiệu lực"; // Cập nhật text trạng thái
                        }
                        await _context.SaveChangesAsync();
                        ClearContractCache(); // Xóa cache vì dữ liệu đã thay đổi
                    }
                    var totalItems = await query.CountAsync();
                    var data = await query
                        .OrderByDescending(h => h.NgayKy)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(h => new HopDongVanChuyenModels
                        {
                            MaHopDong = h.MaHopDong,
                            TenHopDong = h.TenHopDong,
                            MaKhachHang = h.MaKhachHang,
                            NgayKy = h.NgayKy,
                            NgayHetHan = h.NgayHetHan,
                            LoaiHangHoa = h.LoaiHangHoa,
                            TrangThai = h.TrangThai,
                        })
                        .ToListAsync();

                    cachedData = new
                    {
                        TotalItems = totalItems,
                        TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                        CurrentPage = page,
                        Data = data
                    };

                    // Cấu hình Cache: hết hạn sau 30 phút hoặc khi token bị hủy
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));

                    _cache.Set(cacheKey, cachedData, cacheOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi lấy danh sách hợp đồng");
                    return StatusCode(500, "Lỗi máy chủ nội bộ");
                }
            }

            return Ok(cachedData);
        }
        [HttpGet("chitiethopdong/{maHopDong}")]
        public async Task<IActionResult> LayChiTietHopDong([FromRoute] int maHopDong) // Dùng FromRoute để khớp với {maHopDong}
        {
            try
            {
                // Kiểm tra cache trước (tùy chọn)
                string cacheKey = $"ContractDetail_{maHopDong}";
                if (!_cache.TryGetValue(cacheKey, out object cachedDetail))
                {
                    var hopDong = await _context.HopDongVanChuyens
                        .Include(kh => kh.MaKhachHangNavigation)
                        .ThenInclude(dc => dc.MaDiaChiMacDinhNavigation)
                        .AsNoTracking()
                        .Where(hd => hd.MaHopDong == maHopDong)
                        .Select(hd => new HopDongVanChuyenModels
                        {
                            MaHopDong = hd.MaHopDong,
                            TenHopDong = hd.TenHopDong,
                            MaKhachHang = hd.MaKhachHang,
                            NgayKy = hd.NgayKy,
                            NgayHetHan = hd.NgayHetHan,
                            LoaiHangHoa = hd.LoaiHangHoa,
                            TrangThai = hd.TrangThai,
                            TenFileGoc = hd.TenFileGoc,

                            HasFile = hd.FileHopDong != null,
                            MaKhachHangNavigation = new QuanLyKhachHang.Models1.QuanLyKhachHang.KhachHangModels
                            {
                                MaKhachHang = hd.MaKhachHangNavigation.MaKhachHang,
                                TenCongTy = hd.MaKhachHangNavigation.TenCongTy,
                                TenLienHe = hd.MaKhachHangNavigation.TenLienHe,
                                SoDienThoai = hd.MaKhachHangNavigation.SoDienThoai,
                                Email = hd.MaKhachHangNavigation.Email,
                                 DiaChi= hd.MaKhachHangNavigation.MaDiaChiMacDinhNavigation == null ? null : new QuanLyKhachHang.Models1.QuanLyDiaChi.DiaChiModels
                                {
                                    MaDiaChi = hd.MaKhachHangNavigation.MaDiaChiMacDinhNavigation.MaDiaChi,
                                    Duong = hd.MaKhachHangNavigation.MaDiaChiMacDinhNavigation.Duong,
                                    Phuong = hd.MaKhachHangNavigation.MaDiaChiMacDinhNavigation.Phuong,
                                    ThanhPho = hd.MaKhachHangNavigation.MaDiaChiMacDinhNavigation.ThanhPho,
                                    KinhDo = hd.MaKhachHangNavigation.MaDiaChiMacDinhNavigation.KinhDo,
                                    ViDo = hd.MaKhachHangNavigation.MaDiaChiMacDinhNavigation.ViDo
                                }
                            }
                        })
                        .FirstOrDefaultAsync(); // Lấy 1 bản ghi duy nhất

                    if (hopDong == null)
                    {
                        return NotFound(new { message = "Không tìm thấy hợp đồng yêu cầu" });
                    }

                    cachedDetail = hopDong;

                    // Lưu cache chi tiết (ví dụ: 10 phút)
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
                    _cache.Set(cacheKey, cachedDetail, cacheOptions);
                }

                return Ok(cachedDetail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết hợp đồng {maHopDong}", maHopDong);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy chi tiết hợp đồng" });
            }
        }
        // Phương thức hỗ trợ xóa cache
        private void ClearContractCache()
        {
            if (!_resetCacheToken.IsCancellationRequested && _resetCacheToken.Token.CanBeCanceled)
            {
                _resetCacheToken.Cancel();
                _resetCacheToken.Dispose();
            }
            _resetCacheToken = new CancellationTokenSource();
        }
        
        // 1. API THÊM MỚI HỢP ĐỒNG
        [HttpPost("them-moi")]
        public async Task<IActionResult> ThemMoi([FromForm] HopDongVanChuyen model, IFormFile? file)
        {
            if (model == null) return BadRequest("Dữ liệu không hợp lệ.");

            try
            {
                // Kiểm tra khách hàng tồn tại
                var khachHangExists = await _context.KhachHangs.AnyAsync(kh => kh.MaKhachHang == model.MaKhachHang);
                if (!khachHangExists)
                {
                    return BadRequest(new { success = false, message = "Mã khách hàng không tồn tại." });
                }

                // --- XỬ LÝ FILE ĐÍNH KÈM ---
                if (file != null && file.Length > 0)
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);

                    // Gán mảng byte vào trường lưu file trong DB
                    model.FileHopDong = ms.ToArray();
                    // Lưu tên file gốc để sau này tải xuống có đuôi file (pdf, docx...)
                    model.TenFileGoc = file.FileName;
                }
                if (model.NgayHetHan.HasValue && model.NgayHetHan < DateTime.Now)
                {
                    model.TrangThai = "Hết hiệu lực";
                }
                // Thêm vào context và lưu
                _context.HopDongVanChuyens.Add(model);
                await _context.SaveChangesAsync();

                // Xóa cache để danh sách mới được cập nhật
                ClearContractCache();

                return Ok(new
                {
                    success = true,
                    message = "Thêm mới hợp đồng thành công",
                    data = model.MaHopDong
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm mới hợp đồng");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
        [HttpPut("cap-nhat/{id}")]
        public async Task<IActionResult> CapNhat(int id, [FromForm] HopDongVanChuyen model, IFormFile? file)
        {
            // 1. Kiểm tra ID truyền vào và ID trong model (nếu model có chứa ID)
            // Lưu ý: Nếu model gửi từ form không có MaHopDong, bạn có thể bỏ qua check này 
            // và dùng trực tiếp 'id' từ route.
            if (model.MaHopDong != 0 && id != model.MaHopDong)
            {
                return BadRequest(new { success = false, message = "ID không khớp." });
            }

            try
            {
                // 2. Tìm hợp đồng gốc trong Database
                var existingContract = await _context.HopDongVanChuyens.FindAsync(id);

                if (existingContract == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy hợp đồng." });
                }
                existingContract.NgayHetHan = model.NgayHetHan;
                if (existingContract.NgayHetHan.HasValue && existingContract.NgayHetHan < DateTime.Now)
                {
                    existingContract.TrangThai = "Hết hiệu lực";
                }
                // 3. Cập nhật các thông tin cơ bản
                // KHÔNG gán: existingContract.MaKhachHang = model.MaKhachHang; 
                // Việc bỏ qua dòng trên sẽ đảm bảo khách hàng không bị thay đổi.

                existingContract.TenHopDong = model.TenHopDong;
                existingContract.LoaiHangHoa = model.LoaiHangHoa;
                existingContract.TrangThai = model.TrangThai;
                existingContract.NgayKy = model.NgayKy;
                existingContract.NgayHetHan = model.NgayHetHan;

                // 4. Xử lý File (chỉ cập nhật nếu có file mới được upload)
                if (file != null && file.Length > 0)
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    existingContract.FileHopDong = ms.ToArray();
                    existingContract.TenFileGoc = file.FileName;
                }

                // 5. Lưu thay đổi
                await _context.SaveChangesAsync();
                ClearContractCache();
                return Ok(new { success = true, message = "Cập nhật thông tin hợp đồng thành công!" });
            }
            catch (Exception ex)
            {
                // Log lỗi tại đây nếu cần
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
        [HttpGet("download-file/{id}")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            try
            {
                // 1. Truy vấn lấy dữ liệu file từ database
                var hopDong = await _context.HopDongVanChuyens
                    .AsNoTracking()
                    .Where(h => h.MaHopDong == id)
                    .Select(h => new { h.FileHopDong, h.TenFileGoc })
                    .FirstOrDefaultAsync();

                if (hopDong == null || hopDong.FileHopDong == null)
                {
                    return NotFound(new { message = "Hợp đồng không có file đính kèm." });
                }

                // 2. Xác định MIME Type dựa trên đuôi file để trình duyệt có thể xem trực tiếp
                string contentType = "application/octet-stream"; // Mặc định là tải về nếu không xác định được
                string fileName = hopDong.TenFileGoc ?? $"HopDong_{id}.pdf";
                string extension = Path.GetExtension(fileName).ToLower();

                switch (extension)
                {
                    case ".pdf":
                        contentType = "application/pdf";
                        break;
                    case ".doc":
                        contentType = "application/msword";
                        break;
                    case ".docx":
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        break;
                    case ".png":
                        contentType = "image/png";
                        break;
                    case ".jpg":
                    case ".jpeg":
                        contentType = "image/jpeg";
                        break;
                }

                // 3. Trả về file
                // Để "Xem trực tiếp": Trình duyệt cần contentType đúng (như application/pdf)
                // Để "Tải xuống": Tham số thứ 3 (fileDownloadName) sẽ kích hoạt trình duyệt lưu file
                return File(hopDong.FileHopDong, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải file cho hợp đồng {id}", id);
                return StatusCode(500, "Đã xảy ra lỗi khi truy xuất tệp tin.");
            }
        }

    }
}