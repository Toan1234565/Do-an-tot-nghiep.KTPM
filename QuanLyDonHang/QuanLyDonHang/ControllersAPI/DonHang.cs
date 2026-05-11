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
            if (request == null || request.DanhSachKienHang == null || !request.DanhSachKienHang.Any())
                return BadRequest(new { message = "Dữ liệu đơn hàng không hợp lệ." });

            var client = _httpClientFactory.CreateClient();
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // --- BƯỚC 1: ĐỒNG BỘ KHÁCH HÀNG (Dùng KhachHangService) ---
                int maKhachHang = await _khachHangService.CheckSoDienThoaiAsync(
                    request.SoDienThoai,
                    request.TenKhachHang,
                    request.DiaChiLay
                );

                // --- BƯỚC 2: XỬ LÝ ĐỊA CHỈ & LẤY MÃ VÙNG H3 (Dùng DiaChiService) ---
                var (maDcLay, maH3Nhan) = await _diaChiService.CheckDiaChiAsync(request.DiaChiLay);
                if (maDcLay <= 0) return BadRequest(new { message = "Địa chỉ LẤY không hợp lệ." });

                var (maDcGiao, maH3Giao) = await _diaChiService.CheckDiaChiAsync(request.DiaChiGiao);
                if (maDcGiao <= 0) return BadRequest(new { message = "Địa chỉ GIAO không hợp lệ." });

                // --- BƯỚC 3: TÌM KHO PHỤ TRÁCH (Dùng KhoBaiService) ---
                int? maKhoGanNhat = await _khoBaiService.TimKhoGanNhatAsync(maDcLay);

                // --- BƯỚC 4: TÍNH TOÁN GIÁ & HỆ SỐ ---
                decimal tongTienGocCacKien = 0;
                var danhSachGiaGoc = new List<decimal>();

                foreach (var kien in request.DanhSachKienHang)
                {
                    var payloadGia = new YeuCauTinhPhi
                    {
                        ThanhPhoLay = request.DiaChiLay.ThanhPho?.Trim(),
                        ThanhPhoGiao = request.DiaChiGiao.ThanhPho?.Trim(),
                        KhoiLuongTong = kien.KhoiLuong,
                        TheTichTong = kien.TheTich,
                        
                        SoKm = request.SoKm
                    };

                    var options = await ThucHienPhanTichGiaInternal(payloadGia);

                    if (options != null && options.Any())
                    {
                        var selectedOption = options.FirstOrDefault(o => o.MaBangGia == kien.MaBangGiaVung);

                        if (selectedOption == null)
                        {
                            return BadRequest(new
                            {
                                success = false,
                                message = $"Kiện hàng có trọng lượng {kien.KhoiLuong}kg không khớp với mã bảng giá #{kien.MaBangGiaVung} đã chọn."
                            });
                        }

                        decimal giaDonVi = selectedOption.TongTienDuKien;
                        int soLuong = (kien.SoLuongKienHang ?? 0) > 0 ? kien.SoLuongKienHang.Value : 1;
                        decimal tongGiaKien = giaDonVi * soLuong;

                        danhSachGiaGoc.Add(tongGiaKien);
                        tongTienGocCacKien += tongGiaKien;
                    }
                    else
                    {
                        return BadRequest(new { success = false, message = "Hệ thống không tìm thấy bảng giá hợp lệ cho tuyến đường hoặc loại hàng này." });
                    }
                }

                // --- BƯỚC 5: ÁP DỤNG HỆ SỐ DỊCH VỤ & KHUYẾN MÃI ---
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
                    var kmResult = await _khachHangService.ApDungKhuyenMaiAsync(request.MaGiamGia, tongTienDuKien, maKhachHang);
                    soTienGiam = kmResult.soTienGiam;
                    maKhuyenMai = kmResult.maKhuyenMai;
                }

                decimal tongTienThucTe = Math.Max(0, tongTienDuKien - soTienGiam);

                // --- BƯỚC 6: LƯU ĐƠN HÀNG, KIỆN HÀNG, HÓA ĐƠN ---
                var newDonHang = new QuanLyDonHang.Models.DonHang
                {
                    TenDonHang = request.TenDonHang ?? $"Đơn {DateTime.Now:HHmm}",
                    MaKhachHang = maKhachHang,
                    MaDiaChiNhanHang = maDcGiao,
                    MaKhoHienTai = maKhoGanNhat ?? request.MaKhoHienTai,
                    MaDiaChiLayHang = maDcLay,
                    MaMucDoDv = request.MaMucDoDv,
                    TongTienDuKien = tongTienDuKien,
                    TongTienThucTe = tongTienThucTe,
                    ThoiGianTao = DateTime.Now,
                    TrangThaiHienTai = "Chờ lấy hàng",
                    GhiChuDacBiet = $"Giảm giá: {soTienGiam:N0}. Kho phụ trách: {maKhoGanNhat}",
                    TenNguoiNhan = request.TenNguoiNhan,
                    SdtNguoiNhan = request.SdtNguoiNhan,
                    MaKhuyenMai = maKhuyenMai,
                    MaVungH3Giao = maH3Giao,
                    MaVungH3Nhan = maH3Nhan,
                    MaPttt = request.MaPTTT,
                    TrangThaiThanhToanTong = "Chưa thanh toán"
                };

                _context.DonHangs.Add(newDonHang);
                await _context.SaveChangesAsync();

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

                var newHoaDon = new HoaDon
                {
                    MaDonHang = newDonHang.MaDonHang,
                    MaPttt = request.MaPTTT,
                    SoTienThanhToan = tongTienThucTe,
                    NgayThanhToan = DateTime.Now,
                    TrangThaiThanhToan = "Chưa thanh toán",
                };

                await _context.HoaDons.AddAsync(newHoaDon);
                await _context.SaveChangesAsync();

                // Commit Transaction thành công
                await transaction.CommitAsync();

                // --- BƯỚC 7: XỬ LÝ THANH TOÁN & QR CODE ---
                // --- BƯỚC 7: XỬ LÝ THANH TOÁN & QR CODE ---
                string paymentUrl = "";
                string qrCodeUrl = "";

                if (request.MaPTTT == 2)
                {
                    string bankId = "MB";
                    string accountNo = "0833508903";
                    string accountName = "NGUYEN DUC TOAN";
                    string addInfo = $"Thanh toan don hang {newDonHang.MaDonHang}";

                    qrCodeUrl = $"https://img.vietqr.io/image/{bankId}-{accountNo}-compact2.png?amount={(long)tongTienThucTe}&addInfo={Uri.EscapeDataString(addInfo)}&accountName={Uri.EscapeDataString(accountName)}";
                }
                else if (request.MaPTTT == 3)
                {
                    // Các biến accessKey, partnerCode, secretKey, endpoint cần được định nghĩa ở cấp Class hoặc truyền từ appsettings.json
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

               

                // --- BƯỚC 8: LUỒNG HỎA TỐC (Gửi qua RabbitMQ) ---
                if (request.MaMucDoDv?.ToString() == "3")
                {
                    try
                    {
                        // Giả định bạn đã có class RabbitMQProducer được inject hoặc khởi tạo
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
                        _logger.LogError($"Lỗi gửi tin nhắn RabbitMQ cho đơn {newDonHang.MaDonHang}: {ex.Message}");
                    }
                }

                // --- TRẢ VỀ CHO FRONT-END ---
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
            catch (Exception ex)
            {
                await transaction.RollbackAsync(); // Hoàn tác dữ liệu nếu có lỗi hệ thống
                _logger.LogError($"[Fatal Error] TaoDonHang: {ex.Message} - StackTrace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Lỗi hệ thống khi tạo đơn hàng", detail = ex.Message });
            }
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
            // Loại bỏ khoảng trắng thừa để so khớp chính xác với SQL
            string nhanLay = request.ThanhPhoLay?.Trim() ?? "";
            string nhanGiao = request.ThanhPhoGiao?.Trim() ?? "";

            // Tính trọng lượng quy đổi (Dành cho bưu kiện)
            double trongLuongTheTich = (request.TheTichTong ?? 0) / 6000.0;
            decimal trongLuongDeTinh = (decimal)Math.Max((double)(request.KhoiLuongTong ?? 0), trongLuongTheTich);

            // Giả định nếu trên 1 tấn là hàng nặng, ưu tiên tính theo chuyến (LoaiTinhGia = 2)
            bool laHangNang = trongLuongDeTinh > 1000;

            // --- 2. TRUY VẤN DỮ LIỆU ---
            // Ở đây ta lọc trực tiếp theo Tên Tỉnh (Hà Nội, Đà Nẵng...) thay vì Miền
            var tatCaBangGia = await _context.BangGiaVungs
                .AsNoTracking()
                .Where(bg => bg.IsActive == true
                       && bg.KhuVucLay == nhanLay
                       && bg.KhuVucGiao == nhanGiao)
                .ToListAsync();

            // --- 3. LỌC BẢNG GIÁ PHÙ HỢP VỚI CẤU HÌNH HÀNG HÓA ---
            var danhSachLoc = tatCaBangGia.Where(bg => {
                // Khớp loại hàng (nếu có yêu cầu cụ thể)
                if (request.MaLoaiHang > 0 && bg.MaLoaiHang != request.MaLoaiHang) return false;

                // Nếu là hàng nặng (>1000kg), ưu tiên lấy bảng giá tính theo Km
                if (laHangNang) return bg.LoaiTinhGia == 2;

                // Đối với hàng bưu kiện (LoaiTinhGia = 1), kiểm tra khoảng trọng lượng
                if (bg.LoaiTinhGia == 1)
                {
                    var dsCungLoai = tatCaBangGia.Where(x => x.LoaiTinhGia == 1 && x.MaLoaiHang == bg.MaLoaiHang).ToList();
                    decimal maxTrongLuong = dsCungLoai.Any() ? (decimal)dsCungLoai.Max(x => x.TrongLuongToiDaKg ?? 0) : 0;

                    return (trongLuongDeTinh >= (decimal)(bg.TrongLuongToiThieuKg ?? 0) && trongLuongDeTinh <= (decimal)(bg.TrongLuongToiDaKg ?? 0))
                           || (trongLuongDeTinh > maxTrongLuong && (decimal)(bg.TrongLuongToiDaKg ?? 0) == maxTrongLuong);
                }

                return bg.LoaiTinhGia == 2; // Giữ lại các bảng giá tính theo Km để người dùng lựa chọn
            })
            .OrderBy(x => x.LoaiTinhGia) // Ưu tiên bưu kiện hiện lên trước
            .ToList();

            // --- 4. TÍNH TOÁN CHI TIẾT SỐ TIỀN ---
            return danhSachLoc.Select(bg => {
                decimal tongTien = 0;
                string moTa = "";

                if (bg.LoaiTinhGia == 2) // Tính theo Km (Nguyên chuyến)
                {
                    decimal kmTinhPhi = Math.Max((decimal)(request.SoKm ?? 0), (decimal)(bg.KmToiThieu ?? 0));
                    tongTien = (kmTinhPhi * (bg.DonGiaKm ?? 0)) + (bg.DonGiaCoBan ?? 0) + (bg.PhiDungDiem ?? 0);
                    moTa = $"Cước: {kmTinhPhi}km x {bg.DonGiaKm:N0}đ + Phí sàn: {bg.DonGiaCoBan:N0}đ";
                }
                else // Tính theo Bưu kiện (Khối lượng)
                {
                    decimal giaGoc = bg.DonGiaCoBan ?? 0;
                    decimal mucToiDa = (decimal)(bg.TrongLuongToiDaKg ?? 0);
                    decimal khoiLuongVuot = Math.Max(0, trongLuongDeTinh - mucToiDa);

                    tongTien = giaGoc + (khoiLuongVuot * (bg.PhuPhiMoiKg ?? 0));
                    moTa = khoiLuongVuot > 0
                           ? $"Giá mốc ({mucToiDa}kg): {giaGoc:N0}đ + Phí vượt: {khoiLuongVuot:N1}kg x {bg.PhuPhiMoiKg:N0}đ"
                           : $"Giá trọn gói: {giaGoc:N0}đ";
                }

                return new KetQuaPhanTichGia
                {
                    MaBangGia = bg.MaBangGia,
                    TenDichVu = bg.LoaiTinhGia == 2 ? "Vận tải nguyên chuyến" : "Chuyển phát bưu kiện",
                    LoaiHinh = bg.LoaiTinhGia ?? 0,
                    TrongLuongTinhPhi = trongLuongDeTinh,
                    TongTienDuKien = tongTien,
                    MoTaGia = moTa,
                    KhuVuc = $"{nhanLay} -> {nhanGiao}"
                };
            }).OrderBy(x => x.TongTienDuKien).ToList();
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
                // 1. Eager Loading + AsNoTracking: Lấy đơn hàng cùng danh sách kiện hàng
                var donHangs = await _context.DonHangs
                    .Include(dh => dh.KienHangs)
                    .Where(dh => dh.TrangThaiHienTai == "Chờ lấy hàng"
                              && dh.MaDiaChiNhanHang != null // Phải có địa chỉ lấy
                              && dh.MaVungH3Nhan != null    // Phải có vùng H3 nhận
                              && dh.MaMucDoDv != 3)         // Ví dụ: Loại biên 3 không gom nhóm
                    .AsNoTracking()
                    .Take(5000)
                    .ToListAsync();

                if (!donHangs.Any())
                    return NotFound(new { message = "Không có đơn hàng cần thu gom." });

                // 2. Gom nhóm theo vùng H3 của bên NHẬN (MaVungH3Nhan)
                var clusters = donHangs
                    .GroupBy(dh => dh.MaVungH3Nhan)
                    .Select(group => {
                        // Lấy đơn hàng đầu tiên trong nhóm làm đại diện để lấy mã địa chỉ
                        var representativeOrder = group.First();
                        var allKienHangs = group.SelectMany(dh => dh.KienHangs).ToList();

                        return new ClusterResult
                        {
                            MaVungH3 = group.Key!,
                            SoLuongDonHang = group.Count(),
                            // Đảm bảo gán MaDiaChiLayHang vì bên kia dùng trường này ưu tiên
                            MaDiaChiLayHang = (int)representativeOrder.MaDiaChiLayHang ,
                            MaDiaChiCum = (int)representativeOrder.MaDiaChiLayHang ,
                            MaDiaChiNhanHang = (int)(representativeOrder.MaDiaChiNhanHang ?? 0),
                            DanhSachMaDonHang = group.Select(dh => dh.MaDonHang).ToList(),
                            TongKhoiLuong = allKienHangs.Sum(kh => kh.KhoiLuong ?? 0),
                            TongTheTich = allKienHangs.Sum(kh => kh.TheTich ?? 0)
                        };
                    })
                    // Có thể lọc thêm: Chỉ lấy cụm có từ N đơn hàng trở lên (nếu request yêu cầu)
                    // .Where(c => c.SoLuongDonHang >= (request.MinOrdersPerCluster ?? 1))
                    .OrderByDescending(c => c.SoLuongDonHang)
                    .ToList();

                // 3. Xử lý Signal/Cache (Giữ nguyên logic của bạn)
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new
                {
                    TotalClusters = clusters.Count,
                    TotalOrders = donHangs.Count,
                    Clusters = clusters
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực hiện thuật toán gom nhóm đơn hàng H3");
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
    }
}