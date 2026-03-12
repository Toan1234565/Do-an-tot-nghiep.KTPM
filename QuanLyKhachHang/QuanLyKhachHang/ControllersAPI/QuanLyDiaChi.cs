using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1.QuanLyDiaChi;
using QuanLyKhachHang.Models1.QuanLyKhachHang;
using H3;
using H3.Extensions;
using H3.Model;

namespace QuanLyKhachHang.ControllersAPI
{
    [Route("api/quanlydiachi")]
    [ApiController]
    public class QuanLyDiaChi : ControllerBase
    {
        public readonly TmdtContext _context;
        public readonly ILogger<QuanLyDiaChi> _logger;
        public readonly IMemoryCache _cache;
        private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();
        public QuanLyDiaChi(TmdtContext context, ILogger<QuanLyDiaChi> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }
        [HttpPost("themdiachi")]
        public async Task<IActionResult> ThemDiachi([FromBody] DiaChiModels newDiaChi)
        {
            try
            {
                var themmoi = new DiaChi
                {
                    Duong = newDiaChi.Duong,
                    Phuong = newDiaChi.Phuong,
                    ThanhPho = newDiaChi.ThanhPho,
                    MaBuuDien = newDiaChi.MaBuuDien,
                    ViDo = newDiaChi.ViDo,
                    KinhDo = newDiaChi.KinhDo
                };
                _context.DiaChis.Add(themmoi);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Thêm địa chỉ thành công", MaDiaChi = themmoi.MaDiaChi });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm địa chỉ mới");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi thêm địa chỉ" });
            }
        }

        [HttpGet("chitietdiachi/{maDiaChi}")]
        public async Task<IActionResult> LayDiaChi([FromRoute] int maDiaChi) // Khớp tên tham số
        {
            try
            {
                // 1. Tìm kiếm dữ liệu
                var diachi = await _context.DiaChis
                    .AsNoTracking()
                    .FirstOrDefaultAsync(dc => dc.MaDiaChi == maDiaChi);

                // 2. Kiểm tra Null NGAY LẬP TỨC
                if (diachi == null)
                {
                    return NotFound(new { message = "Địa chỉ không tồn tại" });
                }

                // 3. Sau khi chắc chắn có dữ liệu mới thực hiện gán Model
                var diachimodel = new DiaChiModels
                {
                    Duong = diachi.Duong,
                    Phuong = diachi.Phuong,
                    ThanhPho = diachi.ThanhPho,
                    MaBuuDien = diachi.MaBuuDien,
                    KinhDo = diachi.KinhDo,
                    ViDo = diachi.ViDo
                };

                return Ok(diachimodel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy địa chỉ với mã: {maDiaChi}", maDiaChi);
                return StatusCode(500, new { message = "Đã xảy ra lỗi nội bộ khi lấy địa chỉ" });
            }
        }

        [HttpGet("danhsachdiachi")]
        public async Task<IActionResult> LayDanhSachDiaChi()
        {
            // 1. Định nghĩa khóa Cache
            string cacheKey = "FullAddressList";

            try
            {
                // 2. Kiểm tra xem dữ liệu có trong Cache chưa
                if (!_cache.TryGetValue(cacheKey, out List<DiaChiModels>? danhsachdiachi))
                {
                    // 3. Nếu chưa có, truy vấn từ Database
                    danhsachdiachi = await _context.DiaChis
                         .AsNoTracking()
                         .Select(dc => new DiaChiModels
                         {
                             MaDiaChi = dc.MaDiaChi,
                             Duong = dc.Duong,
                             Phuong = dc.Phuong,
                             ThanhPho = dc.ThanhPho,
                             // Ép kiểu về double nếu trong DB đang để kiểu khác
                             ViDo = Convert.ToDouble(dc.ViDo),
                             KinhDo = Convert.ToDouble(dc.KinhDo)
                         }).ToListAsync();

                    // 4. Thiết lập tùy chọn Cache (Thời gian sống 30 phút)
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30)) // Hết hạn tuyệt đối
                        .SetSlidingExpiration(TimeSpan.FromMinutes(10))  // Hết hạn nếu không được truy cập trong 10p
                        .SetPriority(CacheItemPriority.Normal);

                    // 5. Lưu dữ liệu vào Cache
                    _cache.Set(cacheKey, danhsachdiachi, cacheOptions);

                    _logger.LogInformation("Dữ liệu địa chỉ được lấy từ Database.");
                }
                else
                {
                    _logger.LogInformation("Dữ liệu địa chỉ được lấy từ Cache.");
                }

                return Ok(danhsachdiachi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách địa chỉ");
                return StatusCode(500, new { message = "Đã xảy ra lỗi nội bộ khi lấy danh sách địa chỉ" });
            }
        }


        [HttpPost("check_dia_chi")]
        public async Task<IActionResult> CheckOrCreateDiaChi([FromBody] DiaChiModels request)
        {
            try
            {
                // 1. Kiểm tra xem địa chỉ này đã tồn tại trong DB chưa
                var existingAddress = await _context.DiaChis
                    .FirstOrDefaultAsync(dc => dc.ThanhPho == request.ThanhPho &&
                                             dc.Phuong == request.Phuong &&
                                             dc.Duong == request.Duong);

                if (existingAddress != null)
                {
                    return Ok(existingAddress.MaDiaChi); // Trả về ID nếu đã có
                }
                var (lat, lon) = await GetCoordinatesAsync(request.Duong, request.Phuong, request.ThanhPho);

                string h3Hex = string.Empty;
                if (lat.HasValue && lon.HasValue)
                {
                    // Độ phân giải 7 (khoảng 1.2km) phù hợp cho vận tải đô thị
                    var h3IndexObj = H3Index.FromLatLng(new LatLng(lat.Value, lon.Value), 7);
                    h3Hex = h3IndexObj.ToString();
                }

                // 2. Nếu chưa có thì tạo mới
                var newDc = new DiaChi
                {
                    ThanhPho = request.ThanhPho,
                    Phuong = request.Phuong,
                    Duong = request.Duong,
                    KinhDo =lon,
                    ViDo = lat,
                    MaVungH3 = h3Hex

                };
                if (lat == null || lon == null)
                {
                    
                    return BadRequest(new { message = "Không tìm thấy kinh độ/vĩ độ trên bản đồ." });
                }
               
                _context.DiaChis.Add(newDc);
                await _context.SaveChangesAsync();

                return Ok(new { maDiaChi = newDc.MaDiaChi, maVungH3 = newDc.MaVungH3 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra hoặc tạo địa chỉ");
                return StatusCode(500, "Lỗi tạo địa chỉ");
            }
        }
        private async Task<(double? lat, double? lon)> GetCoordinatesAsync(string? duong, string? phuong, string? thanhPho)
        {
            if (string.IsNullOrWhiteSpace(thanhPho)) return (null, null);

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "QuanLyVanchuyenApp/1.0");

                // Kết hợp địa chỉ thành chuỗi tìm kiếm (Query)
                // Ví dụ: "Số 1 Đống Đa, Quang Trung, Hà Nội, Vietnam"
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(duong)) parts.Add(duong);
                if (!string.IsNullOrWhiteSpace(phuong)) parts.Add(phuong);
                if (!string.IsNullOrWhiteSpace(thanhPho)) parts.Add(thanhPho);
                parts.Add("Vietnam");

                string fullAddress = string.Join(", ", parts);
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
                        return (latRes, lonRes);
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
        [HttpGet("lay-toa-do/{id}")]
        public async Task<IActionResult> GetCoordinates(int id)
        {
            var diaChi = await _context.DiaChis
                .Select(d => new {
                    d.MaDiaChi,
                    d.ViDo,
                    d.KinhDo,
                    d.ThanhPho
                })
                .FirstOrDefaultAsync(d => d.MaDiaChi == id);

            if (diaChi == null) return NotFound(new { message = "Không tìm thấy địa chỉ" });

            return Ok(diaChi);
        }
        [HttpPost("lay-toa-do-danh-sach")]
        public async Task<IActionResult> GetToaDoSach([FromBody] List<int> ids)
        {
            var listToaDo = await _context.DiaChis
                .Where(d => ids.Contains(d.MaDiaChi))
                .Select(d => new {
                    d.MaDiaChi,
                    ViDo = d.ViDo,
                    KinhDo = d.KinhDo
                })
                .ToListAsync();

            return Ok(listToaDo);
        }



    }
}