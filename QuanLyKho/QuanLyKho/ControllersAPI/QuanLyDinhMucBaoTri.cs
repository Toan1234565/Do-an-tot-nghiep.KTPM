using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyKho.Models;
using QuanLyKho.Models1;
using QuanLyKho.Models1.QuanLyDinhMucBaoTri;
using QuanLyKho.Models1.QuanLyXe;
using Tmdt.Shared.Services;

namespace QuanLyKho.ControllersAPI
{
    [Route("api/quanlydinhmuc")]
    [ApiController]
    public class QuanLyDinhMucBaoTri : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<QuanLyDinhMucBaoTri> _logger;
        private readonly IMemoryCache _cacheKey;
        private readonly ISystemService _sys;

        // CancellationTokenSource dùng để hiệu hủy toàn bộ cache định mức khi có thay đổi
        private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();

        public QuanLyDinhMucBaoTri(TmdtContext context, ILogger<QuanLyDinhMucBaoTri> logger, IMemoryCache cache, ISystemService sys)
        {
            _context = context;
            _logger = logger;
            _cacheKey = cache;
            _sys = sys;
        }

        [HttpGet("danhsachdinhmuc")]
        public async Task<IActionResult> GetDanhSachDinhMuc([FromQuery] int? maLoaiXe, [FromQuery] int page = 1)
        {
            // Lưu ý: Khi Group dữ liệu, việc phân trang (pageSize) sẽ tính trên số lượng hạng mục, 
            // không phải số lượng loại xe để đảm bảo không mất dữ liệu hạng mục.
            string cacheKey = $"GroupedDanhSachDinhMuc_{maLoaiXe}_{page}";

            if (!_cacheKey.TryGetValue(cacheKey, out object? response))
            {
                try
                {
                    var query = _context.DinhMucBaoTris
                        .Include(d => d.MaLoaiXeNavigation)
                        .AsNoTracking();

                    if (maLoaiXe.HasValue)
                    {
                        query = query.Where(d => d.MaLoaiXe == maLoaiXe);
                    }

                    int pageSize = 50; // Tăng pageSize để hiển thị được nhiều hạng mục của một loại xe hơn

                    // 1. Lấy dữ liệu thô từ DB
                    var rawData = await query
                        .OrderBy(x => x.MaLoaiXe) // Sắp xếp theo loại xe để group dễ hơn
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(x => new DinhMucModels
                        {
                            MaDinhMuc = x.MaDinhMuc,
                            TenHangMuc = x.TenHangMuc,
                            DinhMucKm = x.DinhMucKm,
                            DinhMucThang = x.DinhMucThang,
                            TenLoaiXe = x.MaLoaiXeNavigation != null ? x.MaLoaiXeNavigation.TenLoai : "Loại xe khác"
                        }).ToListAsync();

                    // 2. Group dữ liệu theo TenLoaiXe để phù hợp với hiển thị Rowspan trong ảnh
                    var groupedData = rawData
                        .GroupBy(x => x.TenLoaiXe)
                        .Select(g => new
                        {
                            TenLoai = g.Key,
                            DanhSachHangMuc = g.Select(h => new
                            {
                                h.MaDinhMuc,
                                h.TenHangMuc,
                                h.DinhMucKm,
                                h.DinhMucThang
                            }).ToList()
                        }).ToList();

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));

                    _cacheKey.Set(cacheKey, groupedData, cacheOptions);
                    response = groupedData;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi lấy danh sách định mức group");
                    return StatusCode(500, "Lỗi hệ thống");
                }
            }
            return Ok(response);
        }
        [HttpPost("them-dinh-muc")]
        public async Task<IActionResult> ThemDinhMuc([FromBody] DinhMucInputModel model)
        {
            if (model == null) return BadRequest("Dữ liệu không hợp lệ.");

            try
            {
                // Kiểm tra loại xe
                var loaiXeExists = await _context.LoaiXes.AnyAsync(l => l.MaLoaiXe == model.MaLoaiXe);
                if (!loaiXeExists) return BadRequest("Loại xe không tồn tại.");

                // CHUYỂN ĐỔI: Từ InputModel sang Entity Database
                var newDinhMuc = new DinhMucBaoTri
                {
                    MaLoaiXe = model.MaLoaiXe,
                    TenHangMuc = model.TenHangMuc,
                    DinhMucKm = model.DinhMucKm,
                    DinhMucThang = model.DinhMucThang
                };

                _context.DinhMucBaoTris.Add(newDinhMuc);
                await _context.SaveChangesAsync();

                ResetCache();
                // Ghi log (Sử dụng newDinhMuc.MaDinhMuc vừa được sinh ra)
                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý phương tiện",
                    "Thêm mới định mức: " + model.TenHangMuc,
                    "DinhMucBaoTri",
                    newDinhMuc.MaDinhMuc.ToString(),
                    new Dictionary<string, object>(),
                    new Dictionary<string, object> { /* ... data ... */ }
                );

                return Ok(new { message = "Thêm thành công!", id = newDinhMuc.MaDinhMuc });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi thêm định mức");
                return StatusCode(500, "Lỗi hệ thống.");
            }
        }
        [HttpPut("sua-dinh-muc/{id}")]
        public async Task<IActionResult> SuaDinhMuc(int id, [FromBody] DinhMucInputModel model)
        {
            if (id <= 0 || model == null) return BadRequest("Dữ liệu không hợp lệ.");

            try
            {
                // Vẫn phải lấy từ DB để đảm bảo bản ghi tồn tại và có dữ liệu cũ để so sánh Log
                var dinhMucItem = await _context.DinhMucBaoTris.FindAsync(id);
                if (dinhMucItem == null) return NotFound("Không tìm thấy định mức.");

                // Lưu dữ liệu cũ để ghi log
                var datacu = new Dictionary<string, object>
                {
                    { "Loại xe", dinhMucItem.MaLoaiXe },
                    { "Tên hạng mục", dinhMucItem.TenHangMuc },
                    { "Định mức", $"KM: {dinhMucItem.DinhMucKm} - Tháng: {dinhMucItem.DinhMucThang}" }
                };

                // CẬP NHẬT: Gán từ InputModel vào Entity
                dinhMucItem.MaLoaiXe = model.MaLoaiXe;
                dinhMucItem.TenHangMuc = model.TenHangMuc;
                dinhMucItem.DinhMucKm = model.DinhMucKm;
                dinhMucItem.DinhMucThang = model.DinhMucThang;

                _context.DinhMucBaoTris.Update(dinhMucItem);
                await _context.SaveChangesAsync();

                ResetCache();

                // Chuẩn bị dữ liệu mới cho Log
                var datamoi = new Dictionary<string, object>
                {
                    { "Loại xe", model.MaLoaiXe },
                    { "Tên hạng mục", model.TenHangMuc },
                    { "Định mức", $"KM: {model.DinhMucKm} - Tháng: {model.DinhMucThang}" }
                };

                var (diffCu, diffMoi) = LocThayDoi.GetChanges(datacu, datamoi);

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý phương tiện",
                    "Cập nhật định mức: " + model.TenHangMuc,
                    "DinhMucBaoTri",
                    id.ToString(),
                    diffCu,
                    diffMoi
                );

                return Ok(new { message = "Cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi cập nhật ID: {id}");
                return StatusCode(500, "Lỗi hệ thống.");
            }
        }
        // 3. API LẤY CHI TIẾT (Để load lên Form sửa ở phía Client)
        [HttpGet("chi-tiet/{id}")]
        public async Task<IActionResult> GetChiTiet(int id)
        {
            var item = await _context.DinhMucBaoTris
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.MaDinhMuc == id);

            if (item == null) return NotFound();
            return Ok(item);
        }



        // Hàm Reset Token để vô hiệu hóa toàn bộ cache cũ
        private void ResetCache()
        {
            if (!_resetCacheToken.IsCancellationRequested)
            {
                _resetCacheToken.Cancel();
                _resetCacheToken.Dispose();
            }
            _resetCacheToken = new CancellationTokenSource();
        }
    }
}