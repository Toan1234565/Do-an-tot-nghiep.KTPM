using H3;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1.LienServer;
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
        private readonly IDonHangService _donHangService;
        private const int PageSize = 10; // Đưa vào hằng số

        public KhachHangAPI(ILogger<KhachHangAPI> logger, TmdtContext context, IMemoryCache cache, IDonHangService donHangService)
        {
            _logger = logger;
            _context = context;
            _cache = cache;
            _donHangService = donHangService;
        }

        [HttpGet("danhsachkhachhang")]
        public async Task<IActionResult> LayDanhSachKhachHang([FromQuery] string? searchTerm, [FromQuery] int page = 1)
        {
            // 1. Validate đầu vào cơ bản
            if (page <= 0) page = 1;

            // Chuẩn hóa search term: Loại bỏ khoảng trắng thừa
            searchTerm = searchTerm?.Trim();

            // Key cache nên bao gồm cả PageSize nếu PageSize có thể thay đổi
            var cacheKey = $"CustomerList_P{page}_S_{searchTerm ?? "ALL"}";

            if (_cache.TryGetValue(cacheKey, out var cacheData))
            {
                return Ok(cacheData);
            }

            try
            {
                // 2. Sử dụng IQueryable để xây dựng câu lệnh SQL tối ưu
                var query = _context.KhachHangs.AsNoTracking();

                // 3. Xử lý tìm kiếm (Sử dụng EF.Functions.Like nếu muốn tìm kiếm dấu tiếng Việt linh hoạt hơn)
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(kh =>
                        (kh.TenLienHe != null && kh.TenLienHe.Contains(searchTerm)) ||
                        (kh.TenCongTy != null && kh.TenCongTy.Contains(searchTerm)) ||
                        (kh.SoDienThoai != null && kh.SoDienThoai.Contains(searchTerm)) ||
                        (kh.Email != null && kh.Email.Contains(searchTerm))
                    );
                }

                // 4. Tính toán phân trang
                var totalRecords = await query.CountAsync();

                // Nếu không có bản ghi, trả về format chuẩn nhưng mảng Data rỗng
                if (totalRecords == 0)
                {
                    return Ok(new
                    {
                        CurrentPage = page,
                        TotalPages = 0,
                        PageSize = PageSize,
                        TotalRecords = 0,
                        Data = new List<KhachHangModels>()
                    });
                }

                var totalPages = (int)Math.Ceiling(totalRecords / (double)PageSize);

                // 5. Thực thi Query với Select Projection (Chỉ lấy cột cần thiết)
                var customers = await query
                    .OrderByDescending(kh => kh.MaKhachHang) // Thường khách hàng mới nên lên đầu
                    .Skip((page - 1) * PageSize)
                    .Take(PageSize)
                    .Select(kh => new KhachHangModels
                    {
                        MaKhachHang = kh.MaKhachHang,
                        TenCongTy = kh.TenCongTy ?? "N/A",
                        TenLienHe = kh.TenLienHe ?? "N/A",
                        SoDienThoai = kh.SoDienThoai,
                        Email = kh.Email,
                        // Lấy thông tin điểm thưởng nếu có
                        DiemThuongs = kh.DiemThuongs.Select(dt => new DiemThuongModels
                        {
                            TongDiemTichLuy = dt.TongDiemTichLuy,
                            DiemDaDung = dt.DiemDaDung
                        }).ToList()
                    })
                    .ToListAsync();

                // 6. Đóng gói kết quả
                var result = new
                {
                    CurrentPage = page,
                    TotalPages = totalPages,
                    PageSize = PageSize,
                    TotalRecords = totalRecords,
                    Data = customers
                };

                // 7. Lưu Cache (Thời gian ngắn để đảm bảo tính cập nhật)
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2)); // Nếu có người xem liên tục thì giữ thêm 2p

                _cache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng khi lấy danh sách khách hàng. SearchTerm: {Search}", searchTerm);
                return StatusCode(500, new { message = "Không thể kết nối đến dữ liệu khách hàng. Vui lòng thử lại sau." });
            }
        }

        [HttpGet("khachhang/{maKhachHang}")]
        public async Task<IActionResult> LayKhachHang(int maKhachHang, [FromServices] IDonHangService _donHangService)
        {
            try
            {
                string cacheKey = $"LayKhachHang_{maKhachHang}";

                // 1. Kiểm tra Cache (Nếu có dữ liệu hợp lệ trong cache thì trả về ngay)
                if (_cache.TryGetValue(cacheKey, out KhachHangModels cachedData))
                {
                    return Ok(cachedData);
                }

                // 2. Định nghĩa các Task gọi song song
                // Task A: Lấy từ DB nội bộ
                var khachHangTask = _context.KhachHangs
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
                            TrangThai = hd.TrangThai
                        }).ToList(),
                        DiaChi = kh.MaDiaChiMacDinhNavigation != null ? new QuanLyKhachHang.Models1.QuanLyDiaChi.DiaChiModels
                        {
                            MaDiaChi = kh.MaDiaChiMacDinhNavigation.MaDiaChi,
                            ThanhPho = kh.MaDiaChiMacDinhNavigation.ThanhPho
                        } : null
                    })
                    .FirstOrDefaultAsync();

                // Task B: Lấy từ API Server Đơn hàng
                var donHangTask = _donHangService.GetDanhSachDonHangByKhachHangAsync(maKhachHang, 1, 10);

                // 3. CHỜ CẢ 2 NHƯNG BỌC TRY-CATCH ĐỂ TRÁNH CHẾT CHÙM
                try
                {
                    // Đợi cả 2 hoàn thành. Nếu 1 trong 2 ném Exception, nó sẽ nhảy xuống catch bên dưới
                    await Task.WhenAll(khachHangTask, donHangTask);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Một trong các task gọi dữ liệu bị lỗi (có thể là API Đơn hàng sập): {Msg}", ex.Message);
                    // Không return lỗi ở đây, chúng ta sẽ kiểm tra từng task ở bước sau
                }

                // 4. XỬ LÝ DỮ LIỆU KHÁCH HÀNG (Dữ liệu gốc - Bắt buộc phải có)
                KhachHangModels? result = null;
                if (khachHangTask.IsCompletedSuccessfully)
                {
                    result = await khachHangTask;
                }

                if (result == null)
                {
                    return NotFound(new { message = "Khách hàng không tồn tại hoặc lỗi Database nội bộ." });
                }

                // 5. XỬ LÝ DỮ LIỆU ĐƠN HÀNG (Dữ liệu bổ sung - Có thì tốt, không có cũng không sao)
                if (donHangTask.IsCompletedSuccessfully)
                {
                    // Task thành công, gán dữ liệu từ Result của Task
                    result.DanhSachDonHang = donHangTask.Result;
                }
                else
                {
                    // Task lỗi (Sập API, Timeout...), gán null hoặc khởi tạo object rỗng kèm thông báo
                    _logger.LogError("API Server Đơn hàng không khả dụng cho KH: {ID}", maKhachHang);
                    result.DanhSachDonHang = null;
                    // Bạn có thể thêm một flag như result.Note = "Dữ liệu đơn hàng tạm thời không khả dụng";
                }

                // 6. LƯU VÀO CACHE TRƯỚC KHI TRẢ VỀ
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1));

                _cache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng tại LayKhachHang ID: {ID}", maKhachHang);
                return StatusCode(500, new { message = "Lỗi hệ thống ngoài dự kiến." });
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