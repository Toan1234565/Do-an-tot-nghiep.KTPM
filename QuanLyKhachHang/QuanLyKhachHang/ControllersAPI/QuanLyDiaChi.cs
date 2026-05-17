using H3;
using H3.Extensions;
using H3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1.QuanLyDiaChi;
using QuanLyKhachHang.Models1.QuanLyKhachHang;
using static QuanLyKhachHang.ControllersAPI.KhachHangAPI;

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
            if (request == null || string.IsNullOrWhiteSpace(request.ThanhPho))
            {
                return BadRequest(new { success = false, message = "Dữ liệu địa chỉ đầu vào không đầy đủ." });
            }

            // Tiến hành Trim sạch khoảng trắng thừa
            string duongTrim = request.Duong?.Trim() ?? "";
            string phuongTrim = request.Phuong?.Trim() ?? "";
            string quanTrim = request.Quan?.Trim() ?? "";
            string thanhPhoTrim = request.ThanhPho?.Trim() ?? "";

            try
            {
                // 1. Tìm địa chỉ trong DB
                var existingAddress = await _context.DiaChis
                    .FirstOrDefaultAsync(dc => dc.ThanhPho == thanhPhoTrim &&
                                               dc.Phuong == phuongTrim &&
                                               dc.Quan == quanTrim &&
                                               dc.Duong == duongTrim);

                if (existingAddress != null)
                {
                    if (!string.IsNullOrEmpty(existingAddress.MaVungH3) && existingAddress.ViDo != null)
                    {
                        return Ok(new { success = true, maDiaChi = existingAddress.MaDiaChi, maVungH3 = existingAddress.MaVungH3 });
                    }

                    // Bù đắp dữ liệu nếu bản ghi cũ thiếu tọa độ
                    var (lat, lon) = await GetCoordinatesAsync(duongTrim, phuongTrim, quanTrim, thanhPhoTrim);

                    if (!lat.HasValue || !lon.HasValue)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Địa chỉ này tồn tại trong hệ thống nhưng bản đồ không thể định vị được tọa độ địa lý. Vui lòng kiểm tra lại chính tả đường phố."
                        });
                    }

                    var h3IndexObj = H3Index.FromLatLng(new LatLng(lat.Value, lon.Value), 7);
                    existingAddress.MaVungH3 = h3IndexObj.ToString();
                    existingAddress.ViDo = lat;
                    existingAddress.KinhDo = lon;

                    _context.DiaChis.Update(existingAddress);
                    await _context.SaveChangesAsync();

                    return Ok(new { success = true, maDiaChi = existingAddress.MaDiaChi, maVungH3 = existingAddress.MaVungH3 });
                }

                // 2. Nếu chưa có địa chỉ này trong DB -> Tiến hành định vị mới
                var (newLat, newLon) = await GetCoordinatesAsync(duongTrim, phuongTrim, quanTrim, thanhPhoTrim);

                if (newLat == null || newLon == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Thông tin địa chỉ giao hoặc lấy hàng không thể định vị được trên bản đồ. Vui lòng kiểm tra lại chính tả (Tỉnh/Thành phố, Quận/Huyện, Phường/Xã)."
                    });
                }

                var h3IndexNew = H3Index.FromLatLng(new LatLng(newLat.Value, newLon.Value), 7);
                var newDc = new DiaChi
                {
                    ThanhPho = thanhPhoTrim,
                    Quan = quanTrim,
                    Phuong = phuongTrim,
                    Duong = duongTrim,
                    KinhDo = newLon,
                    ViDo = newLat,
                    MaVungH3 = h3IndexNew.ToString()
                };

                _context.DiaChis.Add(newDc);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, maDiaChi = newDc.MaDiaChi, maVungH3 = newDc.MaVungH3 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi nghiêm trọng khi xử lý địa chỉ: {request.Duong}, {request.Phuong}, {request.ThanhPho}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống nội bộ khi phân tích dữ liệu địa chỉ." });
            }
        }

        private async Task<(double? lat, double? lon)> GetCoordinatesAsync(string? duong, string? phuong, string? quan, string? thanhPho)
        {
            if (string.IsNullOrWhiteSpace(thanhPho)) return (null, null);

            try
            {
                // Tái sử dụng HttpClient thông qua khuyến nghị của .NET thay vì tạo mới liên tục để tránh Socket Exhaustion
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "SmartLogisticsSLTMS/1.0");

                var levels = new List<string[]>();

                // Chuẩn hóa tên đường bằng cách loại bỏ các tiền tố thô sơ như "số 1", "ngõ 2" để tăng tỷ lệ Match của OSM
                string cleanDuong = CleanStreetName(duong);

                if (!string.IsNullOrWhiteSpace(cleanDuong))
                {
                    levels.Add(new[] { cleanDuong, phuong, quan, thanhPho });
                }

                levels.Add(new[] { phuong, quan, thanhPho }); // Dự phòng bậc 2: Định vị về trung tâm Phường/Xã
                levels.Add(new[] { quan, thanhPho });         // Dự phòng bậc 3: Định vị về trung tâm Quận/Huyện

                foreach (var parts in levels)
                {
                    var searchParts = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                    searchParts.Add("Vietnam");
                    string fullAddress = string.Join(", ", searchParts);

                    string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(fullAddress)}&format=json&limit=1";

                    var response = await client.GetAsync(url);

                    // Xử lý lỗi Rate Limit (HTTP 429) hoặc tạm hoãn giữa các tầng fallback để tránh bị OSM Block
                    if ((int)response.StatusCode == 429)
                    {
                        await Task.Delay(1000); // Đợi 1 giây rồi thử lại tầng tiếp theo
                        continue;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var data = System.Text.Json.JsonSerializer.Deserialize<List<GeocodeResult>>(json);

                        if (data != null && data.Count > 0)
                        {
                            if (double.TryParse(data[0].lat, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double latRes) &&
                                double.TryParse(data[0].lon, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lonRes))
                            {
                                _logger.LogInformation("Định vị thành công: {Address} -> ({Lat}, {Lon})", fullAddress, latRes, lonRes);
                                return (latRes, lonRes);
                            }
                        }
                    }

                    // Giãn cách ngắn giữa các lần gọi fallback để bảo vệ cấu trúc Request sang Nominatim
                    await Task.Delay(600);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Geocoding failed: {Message}", ex.Message);
            }
            return (null, null);
        }

        // Hàm bổ trợ làm sạch chuỗi đường phố nhằm tối ưu hóa kết quả tìm kiếm cho OpenStreetMap
        private string CleanStreetName(string? duong)
        {
            if (string.IsNullOrWhiteSpace(duong)) return "";

            // Loại bỏ các từ số nhà, ngách hẻm đứng đầu vì Nominatim không lưu trữ chi tiết số nhà nhỏ tại Việt Nam
            string result = System.Text.RegularExpressions.Regex.Replace(duong, @"^(số\s+\d+|sh\d+|lô\s+\d+|[\d+/]+)\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return result.Trim();
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
        public async Task<IActionResult> GetToaDoDanhSach([FromBody] List<int> maDiaChis)
        {
            if (maDiaChis == null || !maDiaChis.Any())
            {
                return BadRequest("Danh sách mã địa chỉ trống.");
            }

            try
            {
                // Truy vấn database lấy tất cả địa chỉ có trong danh sách ID gửi lên
                var toaDos = await _context.DiaChis
                    .Where(d => maDiaChis.Contains(d.MaDiaChi))
                    .Select(d => new ToaDoResponseDto
                    {
                        MaDiaChi = d.MaDiaChi,
                        ViDo = (double?)d.ViDo,
                        KinhDo = (double?)d.KinhDo
                    })
                    .ToListAsync();

                return Ok(toaDos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách tọa độ");
                return StatusCode(500, "Lỗi máy chủ.");
            }
        }

    }
}