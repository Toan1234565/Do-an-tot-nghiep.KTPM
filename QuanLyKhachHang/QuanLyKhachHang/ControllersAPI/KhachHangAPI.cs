using H3;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1.QuanLyDiaChi;
using QuanLyKhachHang.Models1.QuanLyKhachHang;
using System.Text.RegularExpressions;

namespace QuanLyKhachHang.ControllersAPI
{
    [Route("api/quanlykhachhang")]
    [ApiController]
    public class KhachHangAPI : ControllerBase
    {
        private readonly ILogger<KhachHangAPI> _logger;
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private const int PageSize = 10; // Đưa vào hằng số

        public KhachHangAPI(ILogger<KhachHangAPI> logger, TmdtContext context, IMemoryCache cache)
        {
            _logger = logger;
            _context = context;
            _cache = cache;
        }

        [HttpGet("danhsachkhachhang")]
        public async Task<IActionResult> LayDanhSachKhachHang([FromQuery] string? searchTerm, [FromQuery] int page = 1)
        {
            if (page <= 0) return BadRequest(new { message = "Số trang không hợp lệ" });

            // Tối ưu search term để tránh cache quá nhiều key giống nhau do khoảng trắng
            searchTerm = searchTerm?.Trim();
            var cacheKey = $"CustomerList_P{page}_S_{searchTerm}";

            if (_cache.TryGetValue(cacheKey, out var cacheData))
            {
                return Ok(cacheData);
            }

            try
            {
                var query = _context.KhachHangs.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    // Chuyển ToLower/ToUpper tùy theo Collation của DB để tối ưu index
                    query = query.Where(kh =>
                        (kh.TenLienHe != null && kh.TenLienHe.Contains(searchTerm)) ||
                        (kh.TenCongTy != null && kh.TenCongTy.Contains(searchTerm)) ||
                        (kh.SoDienThoai != null && kh.SoDienThoai.Contains(searchTerm))
                    );
                }

                var totalRecords = await query.CountAsync();

                // Trả về ngay nếu không có dữ liệu để tránh query Skip/Take vô ích
                if (totalRecords == 0) return Ok(new { TotalRecords = 0, Data = new List<object>() });

                var totalPages = (int)Math.Ceiling(totalRecords / (double)PageSize);

                // TỐI ƯU: Select trước khi List để SQL chỉ lấy đúng những cột cần thiết (không lấy hết các cột trong DB)
                var customers = await query
                    .OrderBy(kh => kh.MaKhachHang)
                    .Skip((page - 1) * PageSize)
                    .Take(PageSize)
                    .Select(kh => new KhachHangModels
                    {
                        MaKhachHang = kh.MaKhachHang,
                        TenCongTy = kh.TenCongTy,
                        TenLienHe = kh.TenLienHe,
                        SoDienThoai = kh.SoDienThoai,
                        Email = kh.Email,
                        DiemThuongs = kh.DiemThuongs.Select(dt => new DiemThuongModels
                        {
                            TongDiemTichLuy = dt.TongDiemTichLuy,
                            DiemDaDung = dt.DiemDaDung,
                        }).ToList()
                    })
                    .ToListAsync();

                var result = new
                {
                    CurrentPage = page,
                    TotalPages = totalPages,
                    PageSize = PageSize,
                    TotalRecords = totalRecords,
                    Data = customers
                };

                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5)));

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy danh sách khách hàng. Search: {Search}", searchTerm);
                return StatusCode(500, new { message = "Lỗi hệ thống" });
            }
        }

        [HttpGet("khachhang/{maKhachHang}")]
        public async Task<IActionResult> LayKhachHang(int maKhachHang)
        {
            try
            {
                string cacheKey = $"LayKhachHang_{maKhachHang}";

                // 2. Kiểm tra Cache
                if (_cache.TryGetValue(cacheKey, out object cachedData))
                {
                    return Ok(cachedData);
                }
                // TỐI ƯU: Sử dụng Select để Projection ngay từ đầu. 
                // Điều này giúp SQL không phải thực hiện "Select *", giảm băng thông và CPU của DB.
                var result = await _context.KhachHangs
                    .AsNoTracking()
                    .Where(kh => kh.MaKhachHang == maKhachHang)
                    .Select(kh => new KhachHangModels
                    {
                        MaKhachHang = kh.MaKhachHang,
                        TenCongTy = kh.TenCongTy,
                        TenLienHe = kh.TenLienHe,
                        SoDienThoai = kh.SoDienThoai,
                        Email = kh.Email,
                        DiemThuongs = kh.DiemThuongs.Select(dt => new DiemThuongModels
                        {                           
                            TongDiemTichLuy = dt.TongDiemTichLuy,
                            DiemDaDung = dt.DiemDaDung,
                            NgayCapNhatCuoi = dt.NgayCapNhatCuoi
                        }).ToList(),
                        HopDongVanChuyens = kh.HopDongVanChuyens.Select(hd => new QuanLyKhachHang.Models.HopDongVanChuyen
                        {
                            MaHopDong = hd.MaHopDong,
                            TenHopDong = hd.TenHopDong,
                            LoaiHangHoa = hd.LoaiHangHoa,
                            NgayKy = hd.NgayKy,
                            NgayHetHan = hd.NgayHetHan,
                            TrangThai = hd.TrangThai,
                            MaKhachHang = hd.MaKhachHang
                        }).ToList(),
                        DiaChi = kh.MaDiaChiMacDinhNavigation != null ? new QuanLyKhachHang.Models1.QuanLyDiaChi.DiaChiModels
                        {
                            MaDiaChi = kh.MaDiaChiMacDinhNavigation.MaDiaChi,
                            Duong = kh.MaDiaChiMacDinhNavigation.Duong,
                            Phuong = kh.MaDiaChiMacDinhNavigation.Phuong,
                            ThanhPho = kh.MaDiaChiMacDinhNavigation.ThanhPho,
                            MaBuuDien = kh.MaDiaChiMacDinhNavigation.MaBuuDien,
                            ViDo = kh.MaDiaChiMacDinhNavigation.ViDo,
                            KinhDo = kh.MaDiaChiMacDinhNavigation.KinhDo
                        } : null
                    })
                    .FirstOrDefaultAsync();

                if (result == null) return NotFound(new { message = "Khách hàng không tồn tại" });

                var cacheOptions = new MemoryCacheEntryOptions()
               .SetSlidingExpiration(TimeSpan.FromMinutes(10)) // Hết hạn nếu không truy cập trong 10p
               .SetAbsoluteExpiration(TimeSpan.FromHours(1));  // Xóa cứng sau 1 giờ

                _cache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy khách hàng ID: {ID}", maKhachHang);
                return StatusCode(500, new { message = "Lỗi hệ thống" });
            }
        }
        [HttpGet("chi-tiet-khach-hang/{maKhachHang}")]
        public async Task<IActionResult> chitietkhachhang(int maKhachHang)
        {
            try
            {
                string cacheKey = $"chitietkhachhang_{maKhachHang}";

                // 2. Kiểm tra Cache
                if (_cache.TryGetValue(cacheKey, out object cachedData))
                {
                    return Ok(cachedData);
                }
                // TỐI ƯU: Sử dụng Select để Projection ngay từ đầu. 
                // Điều này giúp SQL không phải thực hiện "Select *", giảm băng thông và CPU của DB.
                var result = await _context.KhachHangs
                    .AsNoTracking()
                    .Where(kh => kh.MaKhachHang == maKhachHang)
                    .Select(kh => new KhachHangSummaryDto
                    {
                        TenKhachHang = kh.TenLienHe,
                        SoDienThoai = kh.SoDienThoai,
                                      
                    })
                    .FirstOrDefaultAsync();

                if (result == null) return NotFound(new { message = "Khách hàng không tồn tại" });

                var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(10)) // Hết hạn nếu không truy cập trong 10p
                .SetAbsoluteExpiration(TimeSpan.FromHours(1));  // Xóa cứng sau 1 giờ

                _cache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy khách hàng ID: {ID}", maKhachHang);
                return StatusCode(500, new { message = "Lỗi hệ thống" });
            }
        }

        [HttpGet("check-phone")]
        public async Task<IActionResult> CheckSoDienThoai([FromQuery] string soDienThoai)
        {
            // 1. Kiểm tra định dạng số điện thoại (Sử dụng Regex đơn giản)
            if (string.IsNullOrWhiteSpace(soDienThoai) || !Regex.IsMatch(soDienThoai, @"^[0-9]{10,11}$"))
            {
                return BadRequest(new { exists = false, message = "Số điện thoại không đúng định dạng." });
            }

            try
            {
                // 2. Chỉ Select những trường thật sự cần thiết để tối ưu Performance
                var khachHang = await _context.KhachHangs
                    .AsNoTracking()
                    .Where(kh => kh.SoDienThoai == soDienThoai)
                    .Select(kh => new
                    {
                        kh.MaKhachHang,
                        kh.TenLienHe,
                        kh.TenCongTy
                    })
                    .FirstOrDefaultAsync();

                if (khachHang != null)
                {
                    return Ok(new
                    {
                        exists = true,
                        message = "Số điện thoại đã tồn tại.",
                        data = khachHang
                    });
                }

                return Ok(new { exists = false, message = "Số điện thoại chưa có trong hệ thống." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi check số điện thoại: {Phone}", soDienThoai);
                return StatusCode(500, new { message = "Lỗi hệ thống khi kiểm tra dữ liệu." });
            }
        }

        [HttpPost("check_so_dien_thoai")]
        public async Task<IActionResult> GetOrCreateByPhone([FromBody] KhachHangModels request)
        {
            if (string.IsNullOrWhiteSpace(request.SoDienThoai) || !Regex.IsMatch(request.SoDienThoai, @"^[0-9]{10,11}$"))
                return BadRequest("Số điện thoại không hợp lệ.");

            try
            {
                // 1. Tìm khách hàng cũ
                var khachHang = await _context.KhachHangs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(kh => kh.SoDienThoai == request.SoDienThoai);

                if (khachHang != null)
                    return Ok(new { maKhachHang = khachHang.MaKhachHang, soDienThoai = khachHang.SoDienThoai });

                // 2. LOGIC TỰ ĐỘNG LẤY TỌA ĐỘ VÀ MÃ H3
                double? lat = request.DiaChi?.ViDo;
                double? lon = request.DiaChi?.KinhDo;
                string? maVungH3 = null;

                if (request.DiaChi != null)
                {
                    if (lat == null || lon == null)
                    {
                        if (!string.IsNullOrEmpty(request.DiaChi.ThanhPho))
                        {
                            _logger.LogInformation("Đang tự động lấy tọa độ cho khách hàng mới...");
                            (lat, lon) = await GetCoordinatesAsync(request.DiaChi.Duong, request.DiaChi.Phuong, request.DiaChi.ThanhPho);
                        }
                    }

                    // Tính toán mã H3 nếu có tọa độ (Quan trọng cho hệ thống Logistics của Hoàng Đế)
                    if (lat.HasValue && lon.HasValue)
                    {
                        try
                        {
                            var h3Index = H3Index.FromLatLng(new H3.Model.LatLng(lat.Value, lon.Value), 7);
                            maVungH3 = h3Index.ToString();
                        }
                        catch { /* Bỏ qua nếu thư viện H3 lỗi */ }
                    }
                }

                // 3. Thực hiện lưu dữ liệu (Transaction)
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var newDiaChi = new DiaChi
                    {
                        Duong = request.DiaChi?.Duong ?? "Chưa xác định",
                        Phuong = request.DiaChi?.Phuong ?? "Chưa xác định",
                        ThanhPho = request.DiaChi?.ThanhPho ?? "Chưa xác định",
                        MaBuuDien = request.DiaChi?.MaBuuDien,
                        ViDo = lat,
                        KinhDo = lon,
                        MaVungH3 = maVungH3 // Lưu thêm mã H3
                    };

                    _context.DiaChis.Add(newDiaChi);
                    await _context.SaveChangesAsync();

                    var newKhachHang = new KhachHang
                    {
                        SoDienThoai = request.SoDienThoai,
                        TenLienHe = request.TenLienHe ?? "Khách vãng lai",
                        TenCongTy = request.TenCongTy ?? request.TenLienHe ?? "Khách lẻ",
                        MaDiaChiMacDinh = newDiaChi.MaDiaChi
                    };

                    _context.KhachHangs.Add(newKhachHang);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // 4. Xóa Cache (Nên dùng Token để clear toàn bộ danh sách khách hàng)
                    // _cache.Remove(...) -> Cách này của ngài cũng được nhưng nên dùng Key chung.

                    return Ok(new
                    {
                        maKhachHang = newKhachHang.MaKhachHang,
                        soDienThoai = newKhachHang.SoDienThoai,
                        toaDo = new { lat, lon, maVungH3 }
                    });
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý khách hàng: {Phone}", request.SoDienThoai);
                return StatusCode(500, "Lỗi máy chủ nội bộ.");
            }
        }

        private async Task<(double? lat, double? lon)> GetCoordinatesAsync(string? duong, string? phuong, string? thanhPho)
        {
            if (string.IsNullOrWhiteSpace(thanhPho)) return (null, null);

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "QuanLyVanchuyenApp/1.0");

                // Lớp 1: Tìm địa chỉ đầy đủ
                var levels = new List<string[]>
                {
                    new[] { duong, phuong, thanhPho }, // Ưu tiên 1: Đầy đủ
                    new[] { phuong, thanhPho }        // Ưu tiên 2: Chỉ lấy Phường/Xã nếu Đường không tìm thấy
                };

                foreach (var parts in levels)
                {
                    var searchParts = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                    searchParts.Add("Vietnam");
                    string fullAddress = string.Join(", ", searchParts);

                    string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(fullAddress)}&format=json&limit=1";

                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var data = System.Text.Json.JsonSerializer.Deserialize<List<GeocodeResult>>(json);

                        if (data != null && data.Count > 0)
                        {
                            double.TryParse(data[0].lat, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double latRes);
                            double.TryParse(data[0].lon, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lonRes);

                            _logger.LogInformation("Tìm thấy tọa độ cho: {Address}", fullAddress);
                            return (latRes, lonRes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Geocoding failed: {Message}", ex.Message);
            }
            return (null, null);
        }

        // Lớp Mapping JSON
        public class GeocodeResult
        {
            [System.Text.Json.Serialization.JsonPropertyName("lat")]
            public string? lat { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("lon")]
            public string? lon { get; set; }
        }

    }
}