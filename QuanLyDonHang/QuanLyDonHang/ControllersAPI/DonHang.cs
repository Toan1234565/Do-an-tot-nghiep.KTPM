using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyDonHang.Models;
using QuanLyDonHang.Models1;
using QuanLyDonHang.Models1.QuanLyBangGiaVung;
using QuanLyDonHang.Models1.QuanLyDieuPhoiGomHang;
using QuanLyDonHang.Models1.QuanLyDonHang.Models1;
using QuanLyDonHang.Models1.ServerKhachHang;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QuanLyDonHang.ControllersAPI
{
    [Route("api/quanlydonhang")]
    [ApiController]
    public class DonHang : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<DonHang> _logger;
        private readonly IMemoryCache _cache;
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKhachHangService _khachHangService;
        private readonly IDiaChiService _diaChiService;
        private readonly IKhoBaiService _khoBaiService;


        // Thông tin tài khoản test Momo (Bạn có thể đăng ký tại developers.momo.vn)
        string endpoint = "https://test-payment.momo.vn/v2/gateway/api/create";
        string partnerCode = "MOMOBKUN20180529"; // Mã đối tác
        string accessKey = "klm05673asj91kj";    // Access Key
        string secretKey = "at67bcvdghas78nhj";   // Secret Key

        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();
        public DonHang(TmdtContext context, ILogger<DonHang> logger, IMemoryCache cache, IHttpClientFactory httpClientFactory, HttpClient httpClient, IKhachHangService khachHangService, IDiaChiService diaChiService, IKhoBaiService khoBaiService )
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _httpClient = httpClient;
            _khachHangService = khachHangService;
            _diaChiService = diaChiService;
            _httpClient = httpClient;
                _khoBaiService = khoBaiService;
        }
        [HttpGet("danhsachdonhang")]
        public async Task<IActionResult> GetDanhSach([FromQuery] string? searchTerm, [FromQuery] string? trangthai, [FromQuery] int page = 1, [FromQuery] int pageSize = 15, [FromQuery] DateTime? batday = null, [FromQuery] DateTime? ketthuc = null)
        {
            if (page < 1) page = 1;
            if (pageSize > 100) pageSize = 100; // Giới hạn pageSize để tránh tấn công DOS

            string cacheKey = $"donhang_{searchTerm}_{trangthai}_{page}_{pageSize}_{batday:yyyyMMdd}_{ketthuc:yyyyMMdd}";

            if (_cache.TryGetValue(cacheKey, out object cachedData))
            {
                return Ok(cachedData);
            }

            try
            {
                // 1. Dùng AsNoTracking và BỎ Include không cần thiết
                var query = _context.DonHangs.AsNoTracking().AsQueryable();

                // 2. Tối ưu tìm kiếm
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.Trim();
                    // Nếu searchTerm là số, ưu tiên tìm theo MaDonHang (Dùng số sẽ ăn Index tốt hơn)
                    if (int.TryParse(searchTerm, out int maDH))
                    {
                        query = query.Where(dh => dh.MaDonHang == maDH || dh.TenDonHang.Contains(searchTerm));
                    }
                    else
                    {
                        query = query.Where(dh => dh.TenDonHang.Contains(searchTerm));
                    }
                }

                // 3. Lọc trạng thái
                if (!string.IsNullOrEmpty(trangthai) && !trangthai.Equals("Tất cả", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(dh => dh.TrangThaiHienTai == trangthai);
                }

                // 4. Lọc ngày (Tối ưu logic kết thúc ngày)
                if (batday.HasValue)
                {
                    query = query.Where(dh => dh.ThoiGianTao >= batday.Value);
                }
                if (ketthuc.HasValue)
                {
                    // Lấy đến hết ngày (23:59:59) của ngày kết thúc
                    var endOfDay = ketthuc.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(dh => dh.ThoiGianTao <= endOfDay);
                }

                // 5. Thực hiện truy vấn đồng thời hoặc tuần tự tối ưu
                var totalItems = await query.CountAsync();

                var danhsach = await query
                    .OrderByDescending(dh => dh.ThoiGianTao)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(dh => new DSDonHangModels // Chỉ lấy các cột cần thiết
                    {
                        MaDonHang = dh.MaDonHang,
                        MaKhachHang = dh.MaKhachHang,
                        MaDiaChiLayHang = dh.MaDiaChiLayHang,
                        TenDonHang = dh.TenDonHang,
                        ThoiGianTao = dh.ThoiGianTao,
                        TrangThaiHienTai = dh.TrangThaiHienTai,
                        MaDiaChiNhanHang = dh.MaDiaChiNhanHang ?? 0
                    })
                    .ToListAsync();

                var result = new
                {
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / pageSize),
                    PageSize = pageSize,
                    CurrentPage = page,
                    Data = danhsach
                };

                // 6. Cache với Sliding Expiration (nếu hay truy cập sẽ tồn tại lâu hơn)
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2))
                    .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                _cache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách đơn hàng");
                return StatusCode(500, "Lỗi hệ thống khi xử lý danh sách đơn hàng.");
            }
        }

        // lay thong tin de hien o lo trinh 
        [HttpGet("chi-tiet-don-hang/{madonhang}")]
        public async Task<IActionResult> ChiTietDonHang(int? madonhang)
        {
            // 1. Kiểm tra đầu vào (Input Validation)
            if (!madonhang.HasValue || madonhang <= 0)
            {
                return BadRequest("Mã đơn hàng không hợp lệ.");
            }

            try
            {
                string cacheKey = $"ChiTietDonHang_{madonhang}";

                // 2. Kiểm tra Cache
                if (_cache.TryGetValue(cacheKey, out object cachedData))
                {
                    return Ok(cachedData);
                }

                // 3. Truy vấn Database (Dùng try-catch để bắt lỗi kết nối DB)
                var donhang = await _context.DonHangs
                    .Where(dh => dh.MaDonHang == madonhang)                   
                    .Select(dh => new ChiTietDonHangLoTrinhModel
                    {
                        TenNguoiNhan = dh.TenNguoiNhan,
                        SdtNguoiNhan = dh.SdtNguoiNhan,
                        MaDiaChiLayHang = (int)dh.MaDiaChiLayHang,
                        MaDiaChiNhanHang = (int)dh.MaDiaChiNhanHang,
                        MaKhachHang = dh.MaKhachHang,
                    })
                    .FirstOrDefaultAsync();

                // 4. Kiểm tra dữ liệu có tồn tại không
                if (donhang == null)
                {
                    return NotFound($"Không tìm thấy đơn hàng với mã: {madonhang}");
                }

                // 5. Thiết lập Cache
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    // Tránh lỗi nếu _resetCacheSignal bị null
                    .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                _cache.Set(cacheKey, donhang, cacheOptions);

                return Ok(donhang);
            }
            catch (Exception ex)
            {
                // 6. Ghi log lỗi (Sử dụng ILogger nếu có)
                _logger.LogError(ex, "Lỗi khi lấy thông tin đơn hàng {MaDonHang}", madonhang);
                return StatusCode(500, "Đã xảy ra lỗi hệ thống khi xử lý yêu cầu của bạn.");
            }
        }


        // xem chi tiet don hang o quan ly don  hang
        [HttpGet("thongtindonhang/{madonhang}")]        
        public async Task<IActionResult> ThongTinDonHang(int? madonhang)
        {
            // 1. Kiểm tra đầu vào
            if (!madonhang.HasValue || madonhang <= 0)
                return BadRequest("Mã đơn hàng không hợp lệ.");

            try
            {
                string cacheKey = $"ChiTietDonHang_Full_{madonhang}";

                // 2. Kiểm tra Cache (Lưu ý: Cache kiểu DonHangModels)
                if (_cache.TryGetValue(cacheKey, out DonHangModels? cachedData))
                {
                    return Ok(cachedData);
                }

                // 3. Truy vấn dữ liệu đơn hàng và kiện hàng từ DB local
                var dh = await _context.DonHangs
                    .Include(x => x.KienHangs)
                    .ThenInclude(bg=>bg.MaBangGiaVungNavigation)
                    .ThenInclude(loai => loai.MaLoaiHangNavigation)// Load danh sách kiện hàng đính kèm
                    .FirstOrDefaultAsync(x => x.MaDonHang == madonhang);

                if (dh == null)
                    return NotFound($"Không tìm thấy đơn hàng mã: {madonhang}");

                // 4. Tạo đối tượng kết quả dựa trên Model bạn cung cấp
                // Giả sử DonHangModels là class chứa dữ liệu trả về cuối cùng
                var result = new DonHangModels
                {
                    MaDonHang = dh.MaDonHang,
                    MaKhachHang = dh.MaKhachHang,
                    TenDonHang = dh.TenDonHang,
                    ThoiGianTao = dh.ThoiGianTao,
                    TrangThaiHienTai = dh.TrangThaiHienTai,
                    TenNguoiNhan = dh.TenNguoiNhan,
                    SdtNguoiNhan = dh.SdtNguoiNhan,
                    GhiChuDacBiet = dh.GhiChuDacBiet,
                    MaDiaChiNhanHang = (int)dh.MaDiaChiNhanHang,
                    MaDiaChiLayHang = dh.MaDiaChiLayHang,
                    MaKhoHienTai = dh.MaKhoHienTai,
                    TongTienDuKien = dh.TongTienDuKien,
                    TongTienThucTe = dh.TongTienThucTe,
                    MaMucDoDv = dh.MaMucDoDv,
                    KienHangs = dh.KienHangs.Select(kh => new KienHangModels
                    {
                        MaVach = kh.MaVach,
                        KhoiLuong = kh.KhoiLuong,
                        TheTich = kh.TheTich,

                        SoTien = kh.SoTien,
                        MaBangGiaVung = kh.MaBangGiaVung,
                        TenLoaiHang = kh.MaBangGiaVungNavigation.MaLoaiHangNavigation.TenLoaiHang

                    }).ToList()
                };

                // 1. Khởi tạo các task song song
                var taskKhachHang = _khachHangService.GetChiTietKhachHangAsync(dh.MaKhachHang);
                var taskDiaChiLay = dh.MaDiaChiLayHang.HasValue
                    ? _diaChiService.GetChiTietDiaChiAsync(dh.MaDiaChiLayHang.Value)
                    : Task.FromResult<DiaChiModel?>(null);
                var taskDiaChiNhan = _diaChiService.GetChiTietDiaChiAsync((int)dh.MaDiaChiNhanHang);
                var taskDiaChiKho = dh.MaKhoHienTai.HasValue
                    ? _diaChiService.GetChiTietDiaChiAsync(dh.MaKhoHienTai.Value)
                    : Task.FromResult<DiaChiModel?>(null);

                // 2. Đợi tất cả các task hoàn thành (Dù thành công hay thất bại)
                try
                {
                    await Task.WhenAll(taskKhachHang, taskDiaChiLay, taskDiaChiNhan, taskDiaChiKho);
                }
                catch (Exception ex)
                {
                    // Chỉ ghi log lỗi chung, không để nó crash chương trình
                    _logger.LogError("Một hoặc nhiều API liên server đã bị lỗi: {Message}", ex.Message);
                }

                // 3. Trích xuất dữ liệu an toàn bằng cách kiểm tra trạng thái từng Task
                // Nếu sập (IsCompletedSuccessfully = false), gán giá trị mặc định là null
                var khachHangInfo = taskKhachHang.IsCompletedSuccessfully ? taskKhachHang.Result : null;
                var diaChiLayInfo = taskDiaChiLay.IsCompletedSuccessfully ? taskDiaChiLay.Result : null;
                var diaChiNhanInfo = taskDiaChiNhan.IsCompletedSuccessfully ? taskDiaChiNhan.Result : null;
                var diaChiKhoInfo = taskDiaChiKho.IsCompletedSuccessfully ? taskDiaChiKho.Result : null;


                // 6. Gán dữ liệu từ các Server khác vào Model chính
                result.ThongTinKhachHang = khachHangInfo;
                result.DiaChiLayHang = diaChiLayInfo;
                result.DiaChiNhanHang = diaChiNhanInfo;
                result.DiaChiKhoHienTai = diaChiKhoInfo;

                // 7. Thiết lập Cache
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                _cache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng khi lấy chi tiết đơn hàng {Ma}", madonhang);
                return StatusCode(500, "Lỗi hệ thống khi xử lý lộ trình đơn hàng.");
            }
        }

        [HttpGet("danhsachdonhangtheokhachhang/{makhachhang}")]
        public async Task<IActionResult> GetDonHangByKhachHang(int makhachhang, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            // 1. Kiểm tra đầu vào
            if (makhachhang <= 0) return BadRequest("Mã khách hàng không hợp lệ.");
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 10;

            // 2. Thiết lập Cache Key
            string cacheKey = $"donhang_khachhang_{makhachhang}_p{page}_s{pageSize}";

            if (_cache.TryGetValue(cacheKey, out object cachedData))
            {
                return Ok(cachedData);
            }

            try
            {
                // 3. Xây dựng truy vấn cơ sở
                // Sử dụng AsNoTracking để tối ưu hóa hiệu năng đọc
                var query = _context.DonHangs
                    .AsNoTracking()
                    .Where(dh => dh.MaKhachHang == makhachhang);

                // Đếm tổng số mục (Sẽ chạy rất nhanh nếu đã tạo Index cho MaKhachHang)
                var totalItems = await query.CountAsync();

                // 4. Lấy dữ liệu phân trang và Mapping trực tiếp trong SQL
                var danhsach = await query
                    .OrderByDescending(dh => dh.ThoiGianTao) // Đảm bảo đã có Index trên ThoiGianTao nếu bảng cực lớn
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(dh => new KhachHang_DonHangModels
                    {
                        MaDonHang = dh.MaDonHang,
                       
                        TenDonHang = dh.TenDonHang,
                        ThoiGianTao = dh.ThoiGianTao,
                        TrangThaiHienTai = dh.TrangThaiHienTai,
                        TongTienThucTe = dh.TongTienThucTe,

                    })
                    .ToListAsync();

                var result = new
                {
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / pageSize),
                    CurrentPage = page,
                    Data = danhsach
                };

                // 5. Lưu Cache
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                _cache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == -2 || ex.Number == 258)
            {
                // Xử lý riêng lỗi Timeout (Số -2 hoặc 258)
                _logger.LogError(ex, "SQL Timeout khi truy vấn đơn hàng của khách hàng {MaKhachHang}", makhachhang);
                return StatusCode(503, "Hệ thống cơ sở dữ liệu đang bận, vui lòng thử lại sau giây lát.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách đơn hàng cho khách hàng {MaKhachHang}", makhachhang);
                return StatusCode(500, "Lỗi máy chủ khi truy vấn danh sách đơn hàng.");
            }
        }

        [HttpPost("tao-moi")]
        public async Task<IActionResult> TaoDonHang([FromBody] DonHangCreate request)
        {
            // --- KIỂM TRA ĐẦU VÀO ---
            if (request == null || request.DanhSachKienHang == null || !request.DanhSachKienHang.Any())
                return BadRequest(new { message = "Dữ liệu đơn hàng không hợp lệ." });

            var client = _httpClientFactory.CreateClient();

            // --- BƯỚC 1: CÁC TÁC VỤ GỌI API NGOÀI (NẰM NGOÀI TRANSACTION) ---
            int maKhachHang;
            int maDcLay, maDcGiao;
            string maH3Nhan, maH3Giao;

            try
            {
                // 1. Đồng bộ khách hàng (Có thể mất đến 5.7s nhưng không gây khóa DB nữa)
                maKhachHang = await _khachHangService.CheckSoDienThoaiAsync(
                    request.SoDienThoai,
                    request.TenKhachHang,
                    request.DiaChiLay
                );

                // 2. Kiểm tra địa chỉ LẤY và GIAO
                var lyResult = await _diaChiService.CheckDiaChiAsync(request.DiaChiLay);
                maDcLay = lyResult.maDiaChi;
                maH3Nhan = lyResult.maVungH3;

                var giaoResult = await _diaChiService.CheckDiaChiAsync(request.DiaChiGiao);
                maDcGiao = giaoResult.maDiaChi;
                maH3Giao = giaoResult.maVungH3; 

                if (maDcLay <= 0 || maDcGiao <= 0)
                    return BadRequest(new { message = "Địa chỉ lấy hoặc giao không hợp lệ (Không thể định vị tọa độ)." });
            }
            catch (HttpRequestException httpEx)
            {
                // BẮT RIÊNG LỖI CỦA HTTP CLIENT: Nếu các API vệ tinh trả về 400, ta gửi thẳng lỗi 400 về cho Front-end
                _logger.LogWarning($"API vệ tinh trả về lỗi nghiệp vụ: {httpEx.Message}");

                // Trả về lỗi 400 kèm lời nhắn giúp User biết họ nhập sai địa chỉ nào
                return BadRequest(new
                {
                    success = false,
                    message = "Thông tin địa chỉ giao hoặc lấy hàng không thể định vị được trên bản đồ. Vui lòng kiểm tra lại chính tả (Tỉnh/Thành phố, Quận/Huyện, Phường/Xã)."
                });
            }
            catch (Exception ex)
            {
                // Bắt các lỗi hệ thống khác (Mất mạng, sập nguồn service...)
                _logger.LogError($"Lỗi hệ thống bất ngờ: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Hệ thống kết nối nội bộ trục trặc, vui lòng thử lại sau ít phút." });
            }
            

            // 3. Tìm kho gần nhất (Đọc dữ liệu nhanh)
            int? maKhoGanNhat = await _khoBaiService.TimKhoGanNhatAsync(maDcLay);


            // --- BƯỚC 2: TÍNH TOÁN CHI PHÍ & BẢNG GIÁ (NGOÀI TRANSACTION) ---
            // --- BƯỚC 2: TÍNH TOÁN CHI PHÍ & BẢNG GIÁ (TỐI ƯU BULK READ & LINQ MEMORY) ---
            decimal tongTienGocCacKien = 0;
            var danhSachGiaGoc = new List<decimal>();

            // 1. Gom tất cả mã loại hàng của các kiện hàng để Query một thể
            var danhSachMaLoaiHang = request.DanhSachKienHang
                .Select(k => k.MaLoaiHang) // Thêm trường MaLoaiHang vào DTO nếu chưa có
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            // 2. Thực hiện Bulk Read từ Database (Đúng 1 lần duy nhất)
            var khoBangGiaRAM = await LayDanhSachBangGiaBulkAsync(request.DiaChiLay.ThanhPho, request.DiaChiGiao.ThanhPho, danhSachMaLoaiHang);

            // 3. Đọc sẵn bảng giá mặc định đề phòng không tìm thấy cấu hình phù hợp
            var bangGiaMacDinh = khoBangGiaRAM.FirstOrDefault(bg => bg.MaBangGia == 69)
                                 ?? await _context.BangGiaVungs.AsNoTracking().FirstOrDefaultAsync(bg => bg.MaBangGia == 69);

            // 4. Vòng lặp duyệt qua các kiện - Lúc này chỉ tính toán trên RAM, siêu tốc!
            foreach (var kien in request.DanhSachKienHang)
            {
                // Tính trọng lượng quy đổi cho từng kiện
                double trongLuongTheTich = (kien.TheTich) / 6000.0;
                decimal trongLuongDeTinh = (decimal)Math.Max((double)(kien.KhoiLuong), trongLuongTheTich);

                // Xác định hình thức vận chuyển của kiện
                bool laHangNguyenChuyen = trongLuongDeTinh > 1000 || kien.MaBangGiaVung == 2; // hoặc logic phân loại của bạn

                // Lọc bảng giá phù hợp từ list đã kéo về RAM bằng LINQ
                BangGiaVung dongPhuHop = null;

                if (laHangNguyenChuyen)
                {
                    dongPhuHop = khoBangGiaRAM.FirstOrDefault(bg => bg.LoaiTinhGia == 2 && bg.MaLoaiHang == kien.MaLoaiHang);
                }
                else
                {
                    // Tìm nấc khối lượng phù hợp cho bưu kiện
                    var nhomTheoLoai = khoBangGiaRAM.Where(bg => bg.LoaiTinhGia == 1 && bg.MaLoaiHang == kien.MaLoaiHang).ToList();

                    dongPhuHop = nhomTheoLoai.FirstOrDefault(bg =>
                        trongLuongDeTinh >= (decimal)(bg.TrongLuongToiThieuKg ?? 0) &&
                        trongLuongDeTinh <= (decimal)(bg.TrongLuongToiDaKg ?? 0));

                    // Nếu vượt nấc tối đa thì lấy dòng có khối lượng cao nhất
                    if (dongPhuHop == null)
                    {
                        dongPhuHop = nhomTheoLoai.OrderByDescending(x => x.TrongLuongToiDaKg).FirstOrDefault();
                    }
                }

                // fallback về bảng giá mặc định nếu không khớp dòng nào
                if (dongPhuHop == null)
                {
                    dongPhuHop = bangGiaMacDinh;
                }

                if (dongPhuHop == null)
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy cấu hình chi phí hợp lệ cho kiện hàng." });
                }

                // --- TÍNH GIÁ CHI TIẾT TRÊN RAM ---
                decimal tongTienKien = 0;

                if (dongPhuHop.LoaiTinhGia == 2) // --- XE NGUYÊN CHUYẾN ---
                {
                    decimal kmTinhPhi = Math.Max((decimal)(request.SoKm ?? 0), (decimal)(dongPhuHop.KmToiThieu ?? 0));
                    tongTienKien = (kmTinhPhi * (dongPhuHop.DonGiaKm ?? 0)) + (dongPhuHop.DonGiaCoBan ?? 0) + (dongPhuHop.PhiDungDiem ?? 0);
                }
                else // --- CHUYỂN PHÁT BƯU KIỆN ---
                {
                    decimal giaGoc = dongPhuHop.DonGiaCoBan ?? 0;
                    decimal trongLuongToiThieu = (decimal)(dongPhuHop.TrongLuongToiThieuKg ?? 0);
                    decimal phuPhiMoiKg = dongPhuHop.PhuPhiMoiKg ?? 0;

                    if (phuPhiMoiKg > 0 && trongLuongDeTinh > trongLuongToiThieu)
                    {
                        decimal khoiLuongVuot = trongLuongDeTinh - trongLuongToiThieu;
                        tongTienKien = giaGoc + (khoiLuongVuot * phuPhiMoiKg);
                    }
                    else
                    {
                        tongTienKien = giaGoc;
                    }
                }

                int soLuong = (kien.SoLuongKienHang ?? 0) > 0 ? kien.SoLuongKienHang.Value : 1;
                decimal tongGiaKienHang = tongTienKien * soLuong;

                // Gán đè ngược lại mã bảng giá tìm được để lưu vào DB ở bước sau
                kien.MaBangGiaVung = dongPhuHop.MaBangGia;

                danhSachGiaGoc.Add(tongGiaKienHang);
                tongTienGocCacKien += tongGiaKienHang;
            }

            // --- BƯỚC 3: ÁP DỤNG HỆ SỐ DỊCH VỤ & KHUYẾN MÃI (NGOÀI TRANSACTION) ---
            decimal heSoDichVu = 1.0m;
            var mucDo = await _context.MucDoDichVus
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MaDichVu == request.MaMucDoDv);

            if (mucDo != null)
            {
                heSoDichVu = (decimal)(mucDo.HeSoNhiPhan ?? 1.0);
            }

            decimal tongTienDuKien = tongTienGocCacKien * heSoDichVu;
            decimal soTienGiam = 0;
            int? maKhuyenMai = null;

            if (!string.IsNullOrEmpty(request.MaGiamGia))
            {
                // Gọi API áp dụng mã giảm giá của service Khách Hàng ngoài Transaction
                var kmResult = await _khachHangService.ApDungKhuyenMaiAsync(request.MaGiamGia, tongTienDuKien, maKhachHang);
                soTienGiam = kmResult.soTienGiam;
                maKhuyenMai = kmResult.maKhuyenMai;
            }

            decimal tongTienThucTe = Math.Max(0, tongTienDuKien - soTienGiam);


            // --- BƯỚC 4: MỞ TRANSACTION (CHỈ DÀNH RIÊNG LƯU DỮ LIỆU XUỐNG DB) ---
            // Lúc này Transaction diễn ra siêu tốc (vài mili-giây) vì toàn bộ data đã được tính toán xong xuôi.
            using var transaction = await _context.Database.BeginTransactionAsync();
            var newDonHang = new QuanLyDonHang.Models.DonHang();

            try
            {
                // 1. Khởi tạo thực thể Đơn hàng
                newDonHang.TenDonHang = request.TenDonHang ?? $"Đơn {DateTime.Now:HHmm}";
                newDonHang.MaKhachHang = maKhachHang;
                newDonHang.MaDiaChiNhanHang = maDcGiao;
                newDonHang.MaKhoHienTai = maKhoGanNhat ?? request.MaKhoHienTai;
                newDonHang.MaDiaChiLayHang = maDcLay;
                newDonHang.MaMucDoDv = request.MaMucDoDv;
                newDonHang.TongTienDuKien = tongTienDuKien;
                newDonHang.TongTienThucTe = tongTienThucTe;
                newDonHang.ThoiGianTao = DateTime.Now;
                newDonHang.TrangThaiHienTai = "Chờ lấy hàng";
                newDonHang.GhiChuDacBiet = $"Giảm giá: {soTienGiam:N0}. Kho phụ trách: {maKhoGanNhat}";
                newDonHang.TenNguoiNhan = request.TenNguoiNhan;
                newDonHang.SdtNguoiNhan = request.SdtNguoiNhan;
                newDonHang.MaKhuyenMai = maKhuyenMai;
                newDonHang.MaVungH3Giao = maH3Giao;
                newDonHang.MaVungH3Nhan = maH3Nhan;
                newDonHang.MaPttt = request.MaPTTT;
                newDonHang.TrangThaiThanhToanTong = "Đã thanh toán";

                _context.DonHangs.Add(newDonHang);
                await _context.SaveChangesAsync(); // Lưu trước để sinh ra MaDonHang

                // 2. Lưu danh sách kiện hàng gắn liền với MaDonHang vừa sinh
                for (int i = 0; i < request.DanhSachKienHang.Count; i++)
                {
                    var kienReq = request.DanhSachKienHang[i];
                    _context.KienHangs.Add(new KienHang
                    {
                        MaDonHang = newDonHang.MaDonHang,
                        KhoiLuong = kienReq.KhoiLuong,
                        TheTich = kienReq.TheTich,
                        SoLuongKienHang = kienReq.SoLuongKienHang,
                        YeuCauBaoQuan = kienReq.YeuCauBaoQuan,
                        MaBangGiaVung = (int)kienReq.MaBangGiaVung,
                        MaVach = "BILL" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper(),
                        SoTien = danhSachGiaGoc[i],
                    });
                }

                // 3. Tạo Hóa Đơn đi kèm
                var newHoaDon = new HoaDon
                {
                    MaDonHang = newDonHang.MaDonHang,
                    MaPttt = request.MaPTTT,
                    SoTienThanhToan = tongTienThucTe,
                    NgayThanhToan = DateTime.Now,
                    TrangThaiThanhToan = "Đã thanh toán",
                };

                await _context.HoaDons.AddAsync(newHoaDon);
                await _context.SaveChangesAsync();

                // Đảm bảo ghi xuống DB thành công, giải phóng khóa kết nối ngay lập tức!
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"[Fatal Error] Luồng ghi DB thất bại: {ex.Message} - StackTrace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Lỗi hệ thống khi lưu đơn hàng vào Cơ sở dữ liệu." });
            }


            // --- BƯỚC 5: XỬ LÝ THANH TOÁN & TẠO LINK QR CODE (NGOÀI TRANSACTION) ---
            string paymentUrl = "";
            string qrCodeUrl = "";

            if (request.MaPTTT == 2) // Phương thức chuyển khoản ngân hàng qua VietQR
            {
                string bankId = "MB";
                string accountNo = "0833508903";
                string accountName = "NGUYEN DUC TOAN";
                string addInfo = $"Thanh toan don hang {newDonHang.MaDonHang}";

                qrCodeUrl = $"https://img.vietqr.io/image/{bankId}-{accountNo}-compact2.png?amount={(long)tongTienThucTe}&addInfo={Uri.EscapeDataString(addInfo)}&accountName={Uri.EscapeDataString(accountName)}";
            }
            else if (request.MaPTTT == 3) // Thanh toán qua cổng MoMo
            {
                // Lưu ý: Các biến accessKey, partnerCode, secretKey, endpoint nên được định nghĩa dạng biến toàn cục lớp hoặc lấy từ appsettings.json
                string orderId = newDonHang.MaDonHang.ToString() + "_" + DateTime.Now.Ticks;
                string requestId = Guid.NewGuid().ToString();
                string orderInfo = "Thanh toán đơn hàng #" + newDonHang.MaDonHang;
                string redirectUrl = "https://localhost:7149/api/thanhtoan/momo-callback";
                string ipnUrl = "https://your-domain.com/api/thanhtoan/momo-ipn";
                string amount = ((long)tongTienThucTe).ToString();
                string extraData = "";

                string rawHash = "accessKey=" + accessKey +
                    "&amount=" + amount +
                    "&extraData=" + extraData +
                    "&ipnUrl=" + ipnUrl +
                    "&orderId=" + orderId +
                    "&orderInfo=" + orderInfo +
                    "&partnerCode=" + partnerCode +
                    "&redirectUrl=" + redirectUrl +
                    "&requestId=" + requestId +
                    "&requestType=captureWallet";

                string signature = "";
                using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
                {
                    byte[] hashValue = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawHash));
                    signature = BitConverter.ToString(hashValue).Replace("-", "").ToLower();
                }

                var message = new
                {
                    partnerCode = partnerCode,
                    requestId = requestId,
                    amount = long.Parse(amount),
                    orderId = orderId,
                    orderInfo = orderInfo,
                    redirectUrl = redirectUrl,
                    ipnUrl = ipnUrl,
                    extraData = extraData,
                    requestType = "captureWallet",
                    signature = signature,
                    lang = "vi"
                };

                try
                {
                    var responseMomo = await client.PostAsJsonAsync(endpoint, message);
                    if (responseMomo.IsSuccessStatusCode)
                    {
                        var resultMomo = await responseMomo.Content.ReadFromJsonAsync<JsonElement>();

                        if (resultMomo.TryGetProperty("payUrl", out var urlElement))
                            paymentUrl = urlElement.GetString();

                        if (resultMomo.TryGetProperty("qrCodeUrl", out var qrElement))
                            qrCodeUrl = qrElement.GetString();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Lỗi gọi cổng thanh toán MoMo: {ex.Message}");
                    // Không chặn đứng tiến trình vì đơn hàng đã lưu DB thành công.
                }
            }


            // --- BƯỚC 6: LUỒNG HỎA TỐC (GỬI QUA RABBITMQ) ---
            if (request.MaMucDoDv?.ToString() == "3")
            {
                try
                {
                    // KHUYẾN NGHỊ: Nên Inject IRabbitMQProducer qua Constructor thay vì tạo mới bằng từ khóa `new` thủ công
                    var rabbitMQ = new RabbitMQProducer();
                    var rmqMessage = new
                    {
                        MaDonHang = newDonHang.MaDonHang,
                        MaDiaChiLayHang = maDcLay,
                        MaDiaChiGiaoHang = maDcGiao,
                        MaKhoVao = maKhoGanNhat ?? request.MaKhoHienTai,
                        TongKhoiLuong = request.DanhSachKienHang.Sum(k => k.KhoiLuong),
                        TongTheTich = request.DanhSachKienHang.Sum(k => k.TheTich),
                        ThoiGian = DateTime.Now
                    };
                    await rabbitMQ.SendOrderMessageAsync(rmqMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Lỗi gửi tin nhắn RabbitMQ cho đơn hỏa tốc {newDonHang.MaDonHang}: {ex.Message}");
                }
            }

            // --- TRẢ VỀ KẾT QUẢ THÀNH CÔNG CHO FRONT-END ---
            return Ok(new
            {
                Success = true,
                MaDonHang = newDonHang.MaDonHang,
                H3 = maH3Nhan,
                TongTien = tongTienThucTe,
                PaymentUrl = paymentUrl,
                QrCodeUrl = qrCodeUrl
            });
        }

        [HttpPost("phan-tich-dich-vu-phu-hop")]
        public async Task<IActionResult> PhanTichDichVu([FromBody] YeuCauTinhPhi request)
        {
            try
            {
                var danhSachKetQua = await ThucHienPhanTichGiaInternal(request);

                if (danhSachKetQua == null || !danhSachKetQua.Any())
                    return Ok(new List<KetQuaPhanTichGia>());

                return Ok(danhSachKetQua);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi xử lý bảng giá", detail = ex.Message });
            }
        }


        private async Task<List<KetQuaPhanTichGia>> ThucHienPhanTichGiaInternal(YeuCauTinhPhi request)
        {
            // --- 1. CHUẨN HÓA DỮ LIỆU ĐẦU VÀO ---
            string thanhPhoLay = request.ThanhPhoLay?.Trim() ?? "";
            string thanhPhoGiao = request.ThanhPhoGiao?.Trim() ?? "";

            // Tính trọng lượng quy đổi (Dành cho bưu kiện: Dài x Rộng x Cao / 6000)
            double trongLuongTheTich = (request.TheTichTong ?? 0) / 6000.0;
            decimal trongLuongDeTinh = (decimal)Math.Max((double)(request.KhoiLuongTong ?? 0), trongLuongTheTich);

            // Xác định hình thức vận chuyển dựa trên trọng lượng hoặc yêu cầu cụ thể
            bool laHangNguyenChuyen = trongLuongDeTinh > 1000 || request.LoaiTinhGia == 2;

            // --- 2. XÁC ĐỊNH VÙNG MIỀN DỰA TRÊN ĐẦU VÀO ---
            // --- 2. XÁC ĐỊNH VÙNG MIỀN DỰA TRÊN ĐẦU VÀO ---
            string phanLoaiVung = "Liên miền";
            string chiTietVung = "Liên miền";

            if (thanhPhoLay.Equals(thanhPhoGiao, StringComparison.OrdinalIgnoreCase))
            {
                phanLoaiVung = "Nội tỉnh";
                chiTietVung = "Nội Cụm";
            }
            else if (XacDinhCungMien(thanhPhoLay, thanhPhoGiao))
            {
                phanLoaiVung = "Nội miền";
                chiTietVung = "Nội miền";
            }
            else
            {
                // ĐƯỜNG ĐI LIÊN MIỀN: Gán nhãn trực tiếp theo vùng để khớp database bảng giá vừng
                phanLoaiVung = LayTenMienCuaTinh(thanhPhoLay);  // Ví dụ: Trả về "Miền Trung"
                chiTietVung = LayTenMienCuaTinh(thanhPhoGiao);   // Ví dụ: Trả về "Miền Nam"
            }

            // --- 3. TRUY VẤN DỮ LIỆU BẢNG GIÁ ---
            IQueryable<BangGiaVung> query = _context.BangGiaVungs.AsNoTracking().Where(bg => bg.IsActive == true);

            if (laHangNguyenChuyen)
            {
                query = query.Where(bg => bg.LoaiTinhGia == 2 &&
                                         (bg.KhuVucLay == thanhPhoLay || bg.KhuVucLay == "Hà Nội & Khác" || bg.KhuVucLay == "Mặc định"));
            }
            else
            {
                query = query.Where(bg => bg.LoaiTinhGia == 1 &&
                                          bg.KhuVucLay == phanLoaiVung &&
                                          (bg.KhuVucGiao == chiTietVung || bg.KhuVucGiao == "Đặc biệt"));
            }

            if (request.MaLoaiHang > 0)
            {
                query = query.Where(bg => bg.MaLoaiHang == request.MaLoaiHang);
            }

            var tatCaBangGiaPhanLoai = await query.ToListAsync();

            // --- 4. LỌC KHỐI LƯỢNG (CHỈ ÁP DỤNG CHO BƯU KIỆN) ---
            var danhSachLoc = new List<BangGiaVung>();

            if (laHangNguyenChuyen)
            {
                danhSachLoc = tatCaBangGiaPhanLoai;
            }
            else
            {
                var nhomTheoLoaiHang = tatCaBangGiaPhanLoai.GroupBy(x => x.MaLoaiHang);

                foreach (var nhom in nhomTheoLoaiHang)
                {
                    var dongPhuHop = nhom.FirstOrDefault(bg =>
                        trongLuongDeTinh >= (decimal)(bg.TrongLuongToiThieuKg ?? 0) &&
                        trongLuongDeTinh <= (decimal)(bg.TrongLuongToiDaKg ?? 0));

                    if (dongPhuHop != null)
                    {
                        danhSachLoc.Add(dongPhuHop);
                    }
                    else
                    {
                        var dongMax = nhom.OrderByDescending(x => x.TrongLuongToiDaKg).FirstOrDefault();
                        if (dongMax != null) danhSachLoc.Add(dongMax);
                    }
                }
            }

            // =========================================================================
            // --- BỔ SUNG: FIX LẤY BẢNG GIÁ MẶC ĐỊNH (ID = 69) NẾU KHÔNG TÌM THẤY ---
            // =========================================================================
            if (!danhSachLoc.Any())
            {
                var bangGiaMacDinh = await _context.BangGiaVungs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(bg => bg.MaBangGia == 69); // Giả định khóa chính là MaBangGia

                if (bangGiaMacDinh != null)
                {
                    danhSachLoc.Add(bangGiaMacDinh);
                }
            }
            // =========================================================================

            // --- 5. TÍNH TOÁN CHI TIẾT GIÁ THÀNH ---
            return danhSachLoc.Select(bg => {
                decimal tongTien = 0;
                string moTa = "";

                if (bg.LoaiTinhGia == 2) // --- XE NGUYÊN CHUYẾN (TÍNH THEO KM) ---
                {
                    decimal kmTinhPhi = Math.Max((decimal)(request.SoKm ?? 0), (decimal)(bg.KmToiThieu ?? 0));
                    decimal donGiaKm = bg.DonGiaKm ?? 0;
                    decimal phiCoBan = bg.DonGiaCoBan ?? 0;

                    tongTien = (kmTinhPhi * donGiaKm) + phiCoBan + (bg.PhiDungDiem ?? 0);
                    moTa = $"Xe chuyến: {kmTinhPhi}km x {donGiaKm:N0}đ/km + Vé bến/Sàn: {phiCoBan:N0}đ";
                }
                else // --- CHUYỂN PHÁT BƯU KIỆN (TÍNH THEO NẤC KHỐI LƯỢNG) ---
                {
                    decimal giaGoc = bg.DonGiaCoBan ?? 0;
                    decimal trongLuongToiThieu = (decimal)(bg.TrongLuongToiThieuKg ?? 0);
                    decimal phuPhiMoiKg = bg.PhuPhiMoiKg ?? 0;

                    if (phuPhiMoiKg > 0 && trongLuongDeTinh > trongLuongToiThieu)
                    {
                        decimal khoiLuongVuot = trongLuongDeTinh - trongLuongToiThieu;
                        tongTien = giaGoc + (khoiLuongVuot * phuPhiMoiKg);
                        moTa = $"Giá mốc ({trongLuongToiThieu}kg): {giaGoc:N0}đ + Vượt mốc: {khoiLuongVuot:N2}kg x {phuPhiMoiKg:N0}đ/kg";
                    }
                    else
                    {
                        tongTien = giaGoc;
                        moTa = $"Giá trọn gói nấc ({bg.TrongLuongToiThieuKg}-{bg.TrongLuongToiDaKg}kg): {giaGoc:N0}đ";
                    }
                }

                return new KetQuaPhanTichGia
                {
                    MaBangGia = bg.MaBangGia,
                    TenDichVu = bg.LoaiTinhGia == 2 ? $"Vận tải xe ({bg.KhuVucLay})" : $"Chuyển phát ({bg.KhuVucLay} -> {bg.KhuVucGiao})",
                    LoaiHinh = bg.LoaiTinhGia ?? 1,
                    TrongLuongTinhPhi = trongLuongDeTinh,
                    TongTienDuKien = tongTien,
                    MoTaGia = moTa,
                    KhuVuc = bg.LoaiTinhGia == 2 ? bg.KhuVucLay : $"{phanLoaiVung} ({chiTietVung})"
                };
            }).OrderBy(x => x.TongTienDuKien).ToList();
        }

        private string LayTenMienCuaTinh(string tinhThanh)
        {
            string tinh = tinhThanh?.Trim().ToLower() ?? "";

            var mienBac = new HashSet<string>
            { 
                // Dữ liệu gốc của bạn
                "hà nội", "cao bằng", "điện biên", "hà tĩnh", "lai châu", "lạng sơn",
                "nghệ an", "quảng ninh", "sơn la", "thanh hóa", "bắc ninh", "hải phòng",
                "hưng yên", "lào cai", "thái nguyên", "tuyên quang", "ninh bình", "phú thọ",

                // Bổ sung đầy đủ cho Miền Bắc (Tổng cộng 25 tỉnh/thành phố)
                "hà giang", "bắc kạn", "yên bái", "hòa bình", "thái bình", "hà nam",
                "nam định", "hải dương", "vĩnh phúc", "bắc giang"
            };

            var mienTrung = new HashSet<string>
            { 
                // Dữ liệu gốc của bạn
                "đà nẵng", "huế", "gia lai", "khánh hòa", "quảng ngãi", "quảng trị",

                // Bổ sung đầy đủ cho Miền Trung (Bao gồm Bắc Trung Bộ, Nam Trung Bộ và Tây Nguyên - 19 tỉnh/thành phố)
                "quảng bình", "thừa thiên huế", // Thêm biến thể đầy đủ của Huế
                "quảng nam", "bình định", "phú yên", "ninh thuận", "bình thuận",
                "kon tum", "đắk lắk", "đắc lắc", "đắk nông", "đắc nông", // Thêm biến thể gõ chữ "c" và "k"
                "lâm đồng"
            };
          
            if (mienBac.Contains(tinh)) return "Miền Bắc";
            if (mienTrung.Contains(tinh)) return "Miền Trung";

            return "Miền Nam"; // Mặc định hoặc thuộc miền Nam (bao gồm Cà Mau)
        }
        /// <summary>
        /// Kiểm tra xem địa chỉ nhận và giao có cùng thuộc một miền hay không (Nội miền)
        /// </summary>
        private bool XacDinhCungMien(string tinhLay, string tinhGiao)
        {
            // Cấu chuẩn dữ liệu chuỗi để tránh sai lệch khoảng trắng và chữ hoa/thường
            string lay = tinhLay?.Trim().ToLower() ?? "";
            string giao = tinhGiao?.Trim().ToLower() ?? "";

            if (string.IsNullOrEmpty(lay) || string.IsNullOrEmpty(giao)) return false;
            if (lay == giao) return true; // Cùng 1 tỉnh chắc chắn thuộc cùng 1 miền (Nội tỉnh)

            // Chuẩn hóa hàm kiểm tra đầu vào trước khi đối chiếu: 
            // string tinhThanhClean = thanhPhoTrim.ToLower().Replace("thành phố ", "").Replace("tỉnh ", "").Trim();

            // Danh mục Miền Bắc chuẩn (25 tỉnh/thành phố)
            var mienBac = new HashSet<string>
            {
                // Đồng bằng & Trung du sông Hồng
                "hà nội", "hải phòng", "bắc ninh", "hà nam", "hải dương",
                "hưng yên", "nam định", "ninh bình", "thái bình", "vĩnh phúc",

                // Đông Bắc Bộ
                "hà giang", "cao bằng", "bắc kạn", "lạng sơn", "tuyên quang",
                "thái nguyên", "phú thọ", "bắc giang", "quảng ninh",

                // Tây Bắc Bộ
                "điện biên", "lai châu", "sơn la", "hòa bình", "lào cai", "yên bái"
            };

                        // Danh mục Miền Trung chuẩn (Bao gồm Bắc Trung Bộ, Nam Trung Bộ và Tây Nguyên - 19 tỉnh/thành)
                        var mienTrung = new HashSet<string>
            {
                // Bắc Trung Bộ (Đã dời Thanh - Nghệ - Tĩnh về đúng Miền Trung)
                "thanh hóa", "nghệ an", "hà tĩnh", "quảng bình", "quảng trị", "thừa thiên huế", "huế",

                // Nam Trung Bộ
                "đà nẵng", "quảng nam", "quảng ngãi", "bình định", "phú yên", "khánh hòa", "ninh thuận", "bình thuận",

                // Tây Nguyên (Đã dời Lâm Đồng, Đắk Lắk về đây và chặn trùng lặp ở Miền Nam)
                "kon tum", "gia lai", "đắk lắk", "đắc lắc", "đak lak", "đắk nông", "đắc nông", "lâm đồng"
            };

                        // Danh mục Miền Nam chuẩn (Bao gồm Đông Nam Bộ và Tây Nam Bộ - 19 tỉnh/thành)
                        var mienNam = new HashSet<string>
            {
                // Đông Nam Bộ (Bổ sung đầy đủ các thủ phủ công nghiệp)
                "hồ chí minh", "tp hcm", "tphcm", "saigon", "sài gòn", // Đón đầu các biến thể người dùng gõ
                "bà rịa - vũng tàu", "bà rịa vũng tàu", "vũng tàu",
                "bình dương", "bình phước", "đồng nai", "tây ninh",

                // Tây Nam Bộ (Đồng bằng sông Cửu Long - Tuyệt đối không sót tỉnh nào)
                "an giang", "bạc liêu", "bến tre", "cà mau", "cần thơ",
                "đồng tháp", "hậu giang", "kiên giang", "long an", "sóc trăng",
                "tiền giang", "trà vinh", "vĩnh long"
            };
            // Kiểm tra xem cả 2 tỉnh có cùng thuộc 1 trong các tập hợp trên không
            if (mienBac.Contains(lay) && mienBac.Contains(giao)) return true;
            if (mienTrung.Contains(lay) && mienTrung.Contains(giao)) return true;
            if (mienNam.Contains(lay) && mienNam.Contains(giao)) return true;

            return false;
        }

        private async Task<List<BangGiaVung>> LayDanhSachBangGiaBulkAsync(string tpLay, string tpGiao, List<int> danhSachMaLoaiHang)
        {
            string thanhPhoLay = tpLay?.Trim() ?? "";
            string thanhPhoGiao = tpGiao?.Trim() ?? "";

            // --- 1. XÁC ĐỊNH VÙNG MIỀN (Giữ nguyên logic của bạn) ---
            string phanLoaiVung = "Liên miền";
            string chiTietVung = "Liên miền";

            if (thanhPhoLay.Equals(thanhPhoGiao, StringComparison.OrdinalIgnoreCase))
            {
                phanLoaiVung = "Nội tỉnh";
                chiTietVung = "Nội Cụm";
            }
            else if (XacDinhCungMien(thanhPhoLay, thanhPhoGiao))
            {
                phanLoaiVung = "Nội miền";
                chiTietVung = "Nội miền";
            }
            else
            {
                phanLoaiVung = LayTenMienCuaTinh(thanhPhoLay);
                chiTietVung = LayTenMienCuaTinh(thanhPhoGiao);
            }

            // --- 2. MỘT QUERY DUY NHẤT LẤY TẤT CẢ ---
            // Gom cả điều kiện bưu kiện (LoaiTinhGia = 1) và xe chuyến (LoaiTinhGia = 2)
            return await _context.BangGiaVungs
                .AsNoTracking()
                .Where(bg => bg.IsActive == true
                             && danhSachMaLoaiHang.Contains(bg.MaLoaiHang)
                             && (
                                 // Khớp điều kiện bưu kiện
                                 (bg.LoaiTinhGia == 1 && bg.KhuVucLay == phanLoaiVung && (bg.KhuVucGiao == chiTietVung || bg.KhuVucGiao == "Đặc biệt"))
                                 ||
                                 // Khớp điều kiện xe nguyên chuyến
                                 (bg.LoaiTinhGia == 2 && (bg.KhuVucLay == thanhPhoLay || bg.KhuVucLay == "Hà Nội & Khác" || bg.KhuVucLay == "Mặc định"))
                             ))
                .ToListAsync();
        }

        [HttpGet("PTTT")]
        public async Task<IActionResult> PTTT()
        {
            try
            {
                var cachekey = $"PTTT";
                // 1. Thử lấy dữ liệu từ Cache
                if (!_cache.TryGetValue(cachekey, out List<DanhMucPTTTModels> dsPttt))
                {
                    // 2. Nếu Cache không có, truy vấn Database
                    dsPttt = await _context.DanhMucPhuongThucThanhToans
                        .AsNoTracking()
                        .Where(tt =>tt.TrangThai == true)
                        .Select(pt => new DanhMucPTTTModels
                        {
                            MaPttt = pt.MaPttt,
                            TenPttt = pt.TenPttt
                        })
                        .ToListAsync(); // Thực thi truy vấn tại đây

                    // 3. Thiết lập cấu hình Cache (ví dụ lưu trong 30 phút)
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(30));

                    // 4. Lưu vào Cache
                    _cache.Set(cachekey, dsPttt, cacheOptions);
                }

                return Ok(dsPttt);
            }
            catch (Exception ex)
            {
                // 5. Log lỗi và trả về thông báo lỗi
                // _logger.LogError(ex, "Lỗi lấy danh sách PTTT");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("trang-thai/{maDonHang}")]
        public async Task<IActionResult> GetTrangThai(int maDonHang)
        {
            var donHang = await _context.DonHangs
                .FirstOrDefaultAsync(d => d.MaDonHang == maDonHang);

            if (donHang == null) return NotFound();

            return Ok(new
            {
                trangThaiThanhToan = donHang.TrangThaiThanhToanTong // Trả về "Đã thanh toán" hoặc "Chưa thanh toán"
            });
        }

        [HttpPost("cho-dieu-phoi")]
        public async Task<IActionResult> TuDongGomNhomDonHang([FromBody] ClusterRequest request)
        {
            try
            {
                // Kiểm tra và gán trạng thái mặc định nếu request không truyền lên
                string trangThaiLoc = string.IsNullOrEmpty(request.TrangThaiDonHang) ? "Chờ lấy hàng" : request.TrangThaiDonHang;

                // 1. Eager Loading + AsNoTracking: Lấy đơn hàng cùng danh sách kiện hàng theo điều kiện tối ưu
                var query = _context.DonHangs
                    .Include(dh => dh.KienHangs)
                    .Where(dh => dh.TrangThaiHienTai == trangThaiLoc
                              && dh.MaDiaChiNhanHang != null
                              && dh.MaVungH3Nhan != null
                              && dh.MaMucDoDv != 3); // Bỏ qua đơn hàng ưu tiên đặc biệt hoặc hỏa tốc độc lập

                var donHangs = await query.AsNoTracking().Take(5000).ToListAsync();

                if (!donHangs.Any())
                    return NotFound(new { message = $"Không có đơn hàng nào ở trạng thái [{trangThaiLoc}] cần thu gom, điều phối." });

                var clusters = new List<ClusterResult>();

                // =======================================================================
                // 2. PHÂN LOẠI LOGIC GOM NHÓM THEO TỪNG CHẶNG CỦA CHUỖI CUNG ỨNG
                // =======================================================================

                if (trangThaiLoc == "Chờ lấy hàng")
                {
                    // --- CHẶNG 1: GOM HÀNG NGOÀI PHỐ (First Mile) ---
                    // Gom nhóm theo điểm lấy hàng (MaDiaChiLayHang) để tối ưu cung đường xe đi gom hàng tại kho người bán
                    clusters = donHangs
                        .Where(dh => dh.MaDiaChiLayHang != null)
                        .GroupBy(dh => dh.MaDiaChiLayHang)
                        .Select(group =>
                        {
                            var allKienHangs = group.SelectMany(dh => dh.KienHangs).ToList();

                            return new ClusterResult
                            {
                                MaVungH3 = "FIRST_MILE", // Gom tại điểm đi, chưa phân cụm vùng đích H3
                                SoLuongDonHang = group.Count(),
                                MaDiaChiLayHang = (int)group.Key!,
                                MaDiaChiCum = (int)group.Key!, // Điểm gom chính là địa chỉ lấy hàng
                                MaDiaChiNhanHang = 0, // Nhiều địa chỉ nhận khác nhau, để 0 để xử lý gom độc lập
                                DanhSachMaDonHang = group.Select(dh => dh.MaDonHang).ToList(),
                                TongKhoiLuong = allKienHangs.Sum(kh => kh.KhoiLuong ?? 0),
                                TongTheTich = allKienHangs.Sum(kh => kh.TheTich ?? 0)
                            };
                        }).ToList();
                }
                else if (trangThaiLoc == "Chờ trung chuyển")
                {
                    // --- CHẶNG 2: TRUNG CHUYỂN TRỤC (Linehaul / Hub-to-Hub) ---
                    // Gom nhóm dựa trên TIỀN TỐ VÙNG NHẬN để đóng xe lớn chạy liên tỉnh/liên miền
                    clusters = donHangs
                        .GroupBy(dh =>
                        {
                            string fullH3 = dh.MaVungH3Nhan!.Trim();
                            string prefix3 = fullH3.Length >= 3 ? fullH3.Substring(0, 3) : fullH3;
                            string prefix6 = fullH3.Length >= 6 ? fullH3.Substring(0, 6) : fullH3;

                            // SỬA LỖI ĐỊA LÝ ĐẶC THÙ: Ép Hải Phòng & Bắc Giang (872ea3) về cụm trục MIEN_BAC (Kho 11)
                            if (prefix3 == "871" || prefix6 == "872ea3")
                                return "MIEN_BAC";

                            if (prefix3 == "872")
                                return "MIEN_TRUNG"; // Các phân vùng còn lại của đầu mã 872 (Kho 15)

                            return "MIEN_NAM"; // Các đầu mã vùng phía nam như 876... (Kho 17)
                        })
                        .Select(group =>
                        {
                            var representativeOrder = group.First();
                            var allKienHangs = group.SelectMany(dh => dh.KienHangs).ToList();

                            return new ClusterResult
                            {
                                MaVungH3 = group.Key, // Gán định danh miền đích làm mã vùng cụm (MIEN_BAC, MIEN_TRUNG, MIEN_NAM)
                                SoLuongDonHang = group.Count(),
                                MaDiaChiLayHang = (int)(representativeOrder.MaDiaChiLayHang ?? 0),
                                MaDiaChiCum = (int)(representativeOrder.MaDiaChiLayHang ?? 0), // Kho xuất phát trung chuyển
                                MaDiaChiNhanHang = 0, // Điểm đến là cả một vùng trung tâm (Hub nhận), xử lý map tọa độ Hub ở tầng Service điều phối
                                DanhSachMaDonHang = group.Select(dh => dh.MaDonHang).ToList(),
                                TongKhoiLuong = allKienHangs.Sum(kh => kh.KhoiLuong ?? 0),
                                TongTheTich = allKienHangs.Sum(kh => kh.TheTich ?? 0)
                            };
                        }).ToList();
                }
                else if (trangThaiLoc == "Chờ giao hàng")
                {
                    // --- CHẶNG 3: PHÁT HÀNG CHẶNG CUỐI (Last Mile) ---
                    // Gom nhóm chi tiết theo từng ô lục giác H3 nơi nhận để phân tuyến cho Shipper đi phát tận nhà
                    clusters = donHangs
                        .GroupBy(dh => dh.MaVungH3Nhan!.Trim())
                        .Select(group =>
                        {
                            var representativeOrder = group.First();
                            var allKienHangs = group.SelectMany(dh => dh.KienHangs).ToList();

                            return new ClusterResult
                            {
                                MaVungH3 = group.Key,
                                SoLuongDonHang = group.Count(),
                                MaDiaChiLayHang = (int)(representativeOrder.MaDiaChiLayHang ?? 0),
                                // ĐỒNG BỘ LOGIC: Điểm dừng trung gian phục vụ OR-Tools tính toán khoảng cách
                                MaDiaChiCum = (int)(representativeOrder.MaDiaChiNhanHang ?? 0),
                                MaDiaChiNhanHang = (int)(representativeOrder.MaDiaChiNhanHang ?? 0),
                                DanhSachMaDonHang = group.Select(dh => dh.MaDonHang).ToList(),
                                TongKhoiLuong = allKienHangs.Sum(kh => kh.KhoiLuong ?? 0),
                                TongTheTich = allKienHangs.Sum(kh => kh.TheTich ?? 0)
                            };
                        }).ToList();
                }

                // Áp dụng bộ lọc số lượng đơn hàng tối thiểu cho mỗi cụm nếu có yêu cầu từ Client
                if (request.MinOrdersPerCluster.HasValue && request.MinOrdersPerCluster.Value > 1)
                {
                    clusters = clusters.Where(c => c.SoLuongDonHang >= request.MinOrdersPerCluster.Value).ToList();
                }

                // 3. Xử lý Signal/Cache (Giữ nguyên luồng giải phóng bộ nhớ đệm hệ thống của bạn)
                if (_resetCacheSignal != null)
                {
                    _resetCacheSignal.Cancel();
                    _resetCacheSignal.Dispose();
                }
                _resetCacheSignal = new CancellationTokenSource();

                // Tính toán tổng số đơn sau khi đã qua bộ lọc MinOrdersPerCluster
                int totalOrdersInClusters = clusters.Sum(c => c.SoLuongDonHang);

                return Ok(new
                {
                    TotalClusters = clusters.Count,
                    TotalOrdersProcessed = totalOrdersInClusters,
                    TotalOrdersLoaded = donHangs.Count,
                    Clusters = clusters.OrderByDescending(c => c.SoLuongDonHang).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực hiện thuật toán gom nhóm đơn hàng H3 đa tầng");
                return StatusCode(500, "Lỗi Server nội bộ khi xử lý gom nhóm: " + ex.Message);
            }
        }

        [HttpPut("cap-nhat-trang-thai-nhieu")]
        public async Task<IActionResult> CapNhatTrangThaiNhieuDonHang([FromBody] UpdateMultiStatusRequest request)
        {
            if (request == null || request.DanhSachMaDonHang == null || !request.DanhSachMaDonHang.Any())
            {
                return BadRequest("Danh sách mã đơn hàng không được để trống.");
            }

            try
            {
                // Truy vấn các đơn hàng có trong danh sách
                var donHangs = await _context.DonHangs
                    .Where(dh => request.DanhSachMaDonHang.Contains(dh.MaDonHang))
                    .ToListAsync();

                if (!donHangs.Any())
                {
                    return NotFound("Không tìm thấy đơn hàng nào trong danh sách cung cấp.");
                }

                // Cập nhật trạng thái
                foreach (var dh in donHangs)
                {
                    dh.TrangThaiHienTai = request.TrangThaiMoi;
                }

                await _context.SaveChangesAsync();

                // Xóa cache để dữ liệu danh sách đơn hàng được cập nhật mới nhất
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new
                {
                    Message = $"Đã cập nhật trạng thái '{request.TrangThaiMoi}' cho {donHangs.Count} đơn hàng.",
                    UpdatedIds = donHangs.Select(dh => dh.MaDonHang)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật trạng thái hàng loạt.");
                return StatusCode(500, "Lỗi hệ thống khi cập nhật trạng thái.");
            }
        }

        [HttpPut("cap-nhat-trang-thai/{madonhang}")]
        public async Task<IActionResult> CapNhatTrangThaiDonHang(int madonhang, [FromBody] string trangThaiMoi)
        {
            if (string.IsNullOrEmpty(trangThaiMoi) || madonhang <= 0)
                return BadRequest(new { Success = false, Message = "Dữ liệu không hợp lệ." });

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Lấy thông tin đơn hàng
                var dbDonHang = await _context.DonHangs
                    .Include(dh => dh.KienHangs)
                    .FirstOrDefaultAsync(dh => dh.MaDonHang == madonhang);

                if (dbDonHang == null) return NotFound(new { Success = false, Message = "Không tìm thấy đơn hàng." });
                // 1. Kiểm tra trạng thái mới có giống trạng thái hiện tại không
                if (dbDonHang.TrangThaiHienTai == trangThaiMoi)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = $"Đơn hàng đã ở trạng thái '{trangThaiMoi}' rồi, không cần cập nhật."
                    });
                }
                // 2. Cập nhật trạng thái tại Local DB (Server Đơn hàng)
                dbDonHang.TrangThaiHienTai = trangThaiMoi;

                await _context.SaveChangesAsync();
                // 3. Commit DB trước khi gửi tin nhắn (Đảm bảo dữ liệu thật đã nằm trong DB)
                await transaction.CommitAsync();

                // 4. BẮN TIN NHẮN SANG RABBITMQ (Thay thế hoàn toàn cho HTTP Call)
                // Đây là cách làm "Event-Driven" chuẩn Microservices
                try
                {
                    var rabbitMQ = new RabbitMQProducer(); // Hoặc dùng Dependency Injection
                    var message = new RoutingOrderMessage
                    {
                        MaDonHang = dbDonHang.MaDonHang,
                        MaKhoVao = dbDonHang.MaKhoHienTai,
                        
                        MaDiaChiNhanHang = dbDonHang.MaDiaChiNhanHang,
                        MaVungH3Nhan = dbDonHang.MaVungH3Nhan,
                        MaVungH3Giao = dbDonHang.MaVungH3Giao,
                        TongKhoiLuong = (double)dbDonHang.KienHangs.Sum(k => k.KhoiLuong),
                        TongTheTich = (double)dbDonHang.KienHangs.Sum(k => k.TheTich),
                        TrangThaiMoi = trangThaiMoi,
                        ThoiGian = DateTime.Now
                    };

                    await rabbitMQ.SendOrderMessageAsync(message);
                }
                catch (Exception ex)
                {
                    // Nếu RabbitMQ lỗi, ta vẫn không báo lỗi cho User vì DB đã lưu xong.
                    // Có thể dùng một bảng 'Outbox' để gửi lại sau nếu cần cực kỳ chính xác.
                    _logger.LogError($"Lỗi gửi tin nhắn RabbitMQ: {ex.Message}");
                }

                return Ok(new { Success = true, Message = "Cập nhật thành công và đã đẩy vào hàng đợi điều phối." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Lỗi: {ex.Message}");
                return StatusCode(500, "Lỗi hệ thống.");
            }
        }

        [HttpGet("vi-tri-hien-tai/{maDonHang}")]
        public async Task<IActionResult> GetViTriHienTaiDonHang(int maDonHang)
        {
            if (maDonHang <= 0)
            {
                return BadRequest("Mã đơn hàng không hợp lệ.");
            }

            try
            {
                // Lấy vị trí và thông tin điều hướng của đơn hàng dựa trên Model định nghĩa
                var viTriDonHang = await _context.DonHangs
                    .Where(dh => dh.MaDonHang == maDonHang)
                    .Select(dh => new DonHangViTriDto
                    {
                        MaDonHang = dh.MaDonHang,
                        TenDonHang = dh.TenDonHang,
                        TrangThaiHienTai = dh.TrangThaiHienTai,
                        MaKhoHienTai = dh.MaKhoHienTai,
                        MaVungH3Nhan = dh.MaVungH3Nhan,
                        MaVungH3Giao = dh.MaVungH3Giao
                    })
                    .FirstOrDefaultAsync();

                if (viTriDonHang == null)
                {
                    return NotFound($"Không tìm thấy vị trí cho đơn hàng mã {maDonHang}.");
                }

                return Ok(viTriDonHang);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy vị trí hiện tại của đơn hàng {MaDonHang}", maDonHang);
                return StatusCode(500, "Lỗi hệ thống khi truy vấn vị trí đơn hàng.");
            }
        }

        [HttpGet("thong-tin-giao-hang/{maDonHang}")]
        public async Task<IActionResult> GetThongTinGiaoHang(int maDonHang)
        {
            if (maDonHang <= 0)
            {
                return BadRequest("Mã đơn hàng cung cấp không hợp lệ.");
            }

            try
            {
                // 1. TRUY VẤN DỮ LIỆU ĐƠN HÀNG GỐC
                var donHang = await _context.DonHangs
                    .Where(dh => dh.MaDonHang == maDonHang)
                    .Select(dh => new
                    {
                        dh.MaDonHang,
                        dh.MaDiaChiNhanHang,
                        dh.MaDiaChiLayHang,
                        MaVungH3GiaoChuan = dh.MaVungH3Giao != null ? dh.MaVungH3Giao.Trim() : string.Empty
                    })
                    .FirstOrDefaultAsync();

                if (donHang == null)
                {
                    return NotFound($"Không tìm thấy thông tin đơn hàng với ID {maDonHang} trong hệ thống.");
                }

                // 2. CHUẨN HÓA LOGIC PHÂN TÍCH TIỀN TỐ H3 XÁC ĐỊNH VÙNG MIỀN ĐÍCH
                string mienGiaoHang = "North"; // Đặt mặc định hoặc cấu hình mặc định hệ thống

                if (!string.IsNullOrEmpty(donHang.MaVungH3GiaoChuan))
                {
                    string h3 = donHang.MaVungH3GiaoChuan;

                    // BƯỚC A: Kiểm tra các tiền tố đặc thù của MIỀN TRUNG trước (Độ dài 4 ký tự để tránh nuốt mã)
                    if (h3.StartsWith("878a") || h3.StartsWith("87b0") || h3.StartsWith("887"))
                    {
                        mienGiaoHang = "Central";
                    }
                    // BƯỚC B: Kiểm tra các tiền tố thuộc MIỀN NAM & TÂY NGUYÊN
                    else if (h3.StartsWith("8760") || h3.StartsWith("87f2") ||
                             h3.StartsWith("87d5") || h3.StartsWith("87c9") || h3.StartsWith("886"))
                    {
                        mienGiaoHang = "South";
                    }
                    // BƯỚC C: Kiểm tra dải rộng của MIỀN BẮC (Sau khi loại trừ các vùng trên)
                    else if (h3.StartsWith("87") || h3.StartsWith("882"))
                    {
                        mienGiaoHang = "North";
                    }
                    // BƯỚC D: Trường hợp mã vùng H3 lạ không nằm trong danh sách cấu hình hệ thống hiện tại
                    else
                    {
                        mienGiaoHang = "North"; // Fallback an toàn cho hệ thống
                    }
                }
                else
                {
                    mienGiaoHang = "Unknown";
                }

                // 3. ĐẨY DỮ LIỆU DTO ĐỒNG BỘ TRẢ VỀ CHO CONTROLLER ĐIỀU PHỐI
                return Ok(new
                {
                    mienGiaoHang = mienGiaoHang,
                    maDiaChiNhanHang = donHang.MaDiaChiNhanHang,
                    maDiaChiLayHang = donHang.MaDiaChiLayHang,
                    maVungH3Giao = donHang.MaVungH3GiaoChuan
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi bóc tách thông tin vùng miền giao hàng cho đơn: {MaDonHang}", maDonHang);
                return StatusCode(500, "Lỗi Server nội bộ khi bóc tách thông tin luân chuyển vùng: " + ex.Message);
            }
        }
    }
}