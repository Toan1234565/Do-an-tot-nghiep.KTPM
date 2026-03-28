using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyDonHang.Models;
using QuanLyDonHang.Models1;
using QuanLyDonHang.Models1.QuanLyDieuPhoiGomHang;
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
        string apiDiaChi = "https://localhost:7149/api/quanlydiachi";
        string apiKhachHang = "https://localhost:7149/api/quanlykhachhang";
        string apiVung = "https://localhost:7149/api/quanlybangiavung";
        string BaseUrl = "https://localhost:7149/api";

        // Thông tin tài khoản test Momo (Bạn có thể đăng ký tại developers.momo.vn)
        string endpoint = "https://test-payment.momo.vn/v2/gateway/api/create";
        string partnerCode = "MOMOBKUN20180529"; // Mã đối tác
        string accessKey = "klm05673asj91kj";    // Access Key
        string secretKey = "at67bcvdghas78nhj";   // Secret Key

        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();
        public DonHang(TmdtContext context, ILogger<DonHang> logger, IMemoryCache cache, IHttpClientFactory httpClientFactory, HttpClient httpClient)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _httpClient = httpClient;
        }
        [HttpGet("danhsachdonhang")]
        public async Task<IActionResult> getdanhsach([FromQuery] string? searchTerm, [FromQuery] string? trangthai, [FromQuery] int page = 1, [FromQuery] int pageSize = 15, [FromQuery] DateTime? batday = null, [FromQuery] DateTime? ketthuc = null)
        {

            // Đảm bảo page tối thiểu là 1
            if (page < 1) page = 1;

            // cacheKey phải bao gồm cả pageSize nếu bạn cho phép thay đổi nó
            string cacheKey = $"donhang_{searchTerm}_{trangthai}_{page}_{pageSize}_{batday:yyyyMMdd}_{ketthuc:yyyyMMdd}";



            // 1. Kiểm tra Cache trước
            if (_cache.TryGetValue(cacheKey, out object cachedData))
            {
                return Ok(cachedData);
            }

            try
            {
                var query = _context.DonHangs.Include(kh => kh.KienHangs).AsQueryable();

                // 2. Lọc theo từ khóa
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    // Lưu ý: Không nên ToString() trong Where nếu MaDonHang là kiểu số, EF Core sẽ tự xử lý.
                    query = query.Where(dh => dh.MaDonHang.ToString().Contains(searchTerm) || dh.TenDonHang.Contains(searchTerm));
                }

                // 3. Lọc theo trạng thái
                if (!string.IsNullOrEmpty(trangthai) && trangthai != "Tất cả")
                {
                    query = query.Where(dh => dh.TrangThaiHienTai == trangthai);
                }

                // 4. Lọc theo ngày
                if (batday.HasValue)
                {
                    query = query.Where(dh => dh.ThoiGianTao >= batday.Value);
                }
                if (ketthuc.HasValue)
                {
                    // Nếu muốn tính hết cả ngày kết thúc, bạn nên cộng thêm 1 ngày hoặc dùng <= ketthuc.Value.AddDays(1)
                    query = query.Where(dh => dh.ThoiGianTao <= ketthuc.Value);
                }

                var totalItems = await query.CountAsync();
                var danhsach = await query
                    .OrderByDescending(dh => dh.ThoiGianTao)
                    .Skip((page - 1) * pageSize) // Phân trang tại đây
                    .Take(pageSize)
                    .Select(dh => new DonHangModels
                    {
                        MaDonHang = dh.MaDonHang,
                        MaKhachHang = dh.MaKhachHang,

                        MaDiaChiLayHang = dh.MaDiaChiLayHang,
                        // Kiểm tra null để tránh lỗi nếu KienHangs trống                     
                        TenDonHang = dh.TenDonHang,
                        ThoiGianTao = dh.ThoiGianTao,
                        TrangThaiHienTai = dh.TrangThaiHienTai,
                        MaLoaiDv = dh.MaLoaiDv,
                        LaDonGiaoThang = dh.LaDonGiaoThang,
                        MaDiaChiNhanHang = dh.MaDiaChiNhanHang ?? 0,

                        KienHangs = dh.KienHangs.Select(kh => new KienHangModels
                        {
                            MaVach = kh.MaVach,

                            KhoiLuong = kh.KhoiLuong,
                            TheTich = kh.TheTich,
                            DaThuGom = kh.DaThuGom,
                            SoTien = kh.SoTien,
                        }).ToList()
                    })
                    .ToListAsync();

                var result = new
                {
                    TotalItems = totalItems,
                    PageSize = pageSize,
                    CurrentPage = page,
                    Data = danhsach
                };

                // 5. Lưu vào Cache
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                _cache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách đơn hàng");
                return StatusCode(500, "Đã xảy ra lỗi máy chủ.");
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
                    .Select(dh => new ChiTietDonHang
                    {
                        TenNguoiNhan = dh.TenNguoiNhan,
                        SdtNguoiNhan = dh.SdtNguoiNhan,
                        MaDiaChiLayHang = dh.MaDiaChiLayHang,
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

        [HttpGet("thongtindonhang/{madonhang}")]
        public async Task<IActionResult> GetThongTinDonHang(int? madonhang)
        {
            // 1. Kiểm tra đầu vào (Input Validation)
            if (!madonhang.HasValue || madonhang <= 0)
            {
                return BadRequest("Mã đơn hàng không hợp lệ.");
            }

            try
            {
                string cacheKey = $"thongtindonhang_{madonhang}";

                // 2. Kiểm tra Cache
                if (_cache.TryGetValue(cacheKey, out object cachedData))
                {
                    return Ok(cachedData);
                }

                // 3. Truy vấn Database (Dùng try-catch để bắt lỗi kết nối DB)
                var donhang = await _context.DonHangs
                    .Where(dh => dh.MaDonHang == madonhang)
                    .Include(kh => kh.KienHangs)
                    .ThenInclude(dm => dm.MaLoaiHangNavigation)
                    .Select(dh => new DonHangModels
                    {
                        MaKhachHang = dh.MaKhachHang,
                        TenDonHang = dh.TenDonHang,
                        ThoiGianTao = dh.ThoiGianTao,
                        MaDiaChiLayHang = dh.MaDiaChiLayHang,
                        TrangThaiHienTai = dh.TrangThaiHienTai,
                        MaLoaiDv = dh.MaLoaiDv,
                        MaHopDongNgoai = dh.MaHopDongNgoai,
                        GhiChuDacBiet = dh.GhiChuDacBiet,
                        LaDonGiaoThang = dh.LaDonGiaoThang,
                        MaVung = dh.MaVung,
                        MaDiaChiNhanHang = dh.MaDiaChiNhanHang ?? 0,
                        TenNguoiNhan = dh.TenNguoiNhan,
                        SdtNguoiNhan = dh.SdtNguoiNhan,
                        TongTienDuKien = dh.TongTienDuKien,
                        TongTienThucTe = dh.TongTienThucTe,
                        MaMucDoDv = dh.MaMucDoDv,
                        KienHangs = dh.KienHangs.Select(kh => new KienHangModels
                        {
                            MaVach = kh.MaVach,

                            KhoiLuong = kh.KhoiLuong,
                            TheTich = kh.TheTich,
                            DaThuGom = kh.DaThuGom,
                            SoTien = kh.SoTien,

                            MaKhoHienTai = kh.MaKhoHienTai,
                            MaLoaiHangNavigation = new DanhMucLoaiHangModels
                            {
                                TenLoaiHang = kh.MaLoaiHangNavigation.TenLoaiHang,
                                MoTa = kh.MaLoaiHangNavigation.MoTa
                            }

                        }).ToList()
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

        [HttpGet("danhsachdonhangtheokhachhang/{makhachhang}")]
        public async Task<IActionResult> GetDonHangByKhachHang(int makhachhang, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            // 1. Kiểm tra đầu vào
            if (makhachhang <= 0) return BadRequest("Mã khách hàng không hợp lệ.");
            if (page < 1) page = 1;

            // 2. Thiết lập Cache Key
            string cacheKey = $"donhang_khachhang_{makhachhang}_p{page}_s{pageSize}";

            if (_cache.TryGetValue(cacheKey, out object cachedData))
            {
                return Ok(cachedData);
            }

            try
            {
                // 3. Truy vấn dữ liệu
                var query = _context.DonHangs
                    .Where(dh => dh.MaKhachHang == makhachhang)
                    .Include(dh => dh.KienHangs)
                    .AsQueryable();

                var totalItems = await query.CountAsync();

                var danhsach = await query
                    .OrderByDescending(dh => dh.ThoiGianTao)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(dh => new DonHangModels
                    {
                        MaDonHang = dh.MaDonHang,
                        MaKhachHang = dh.MaKhachHang,
                        TenDonHang = dh.TenDonHang,
                        ThoiGianTao = dh.ThoiGianTao,
                        TrangThaiHienTai = dh.TrangThaiHienTai,
                        MaLoaiDv = dh.MaLoaiDv,
                        LaDonGiaoThang = dh.LaDonGiaoThang,
                        MaVung = dh.MaVung,
                        MaDiaChiLayHang = dh.MaDiaChiLayHang,                       
                       
                        TenNguoiNhan = dh.TenNguoiNhan,
                        SdtNguoiNhan = dh.SdtNguoiNhan,
                        MaDiaChiNhanHang = dh.MaDiaChiNhanHang ?? 0,
                        KienHangs = dh.KienHangs.Select(kh => new KienHangModels
                        {
                            MaVach = kh.MaVach,
                            KhoiLuong = kh.KhoiLuong,
                            SoTien = kh.SoTien,
                            DaThuGom = kh.DaThuGom
                        }).ToList()
                    })
                    .ToListAsync();

                var result = new
                {
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / pageSize),
                    CurrentPage = page,
                    Data = danhsach
                };

                // 4. Lưu Cache
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                _cache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
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

            string baseServiceUrl = "https://localhost:7149/api";
            var client = _httpClientFactory.CreateClient();
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                
                // --- BƯỚC 1: ĐỒNG BỘ KHÁCH HÀNG & ĐỊA CHỈ ---
                var resKh = await client.PostAsJsonAsync($"{baseServiceUrl}/quanlykhachhang/check_so_dien_thoai", new
                {
                    SoDienThoai = request.SoDienThoai,
                    TenLienHe = request.TenKhachHang,
                    DiaChi = request.DiaChiLay
                });

                // KIỂM TRA LỖI TRƯỚC KHI ĐỌC JSON
                if (!resKh.IsSuccessStatusCode)
                {
                    // Đọc dưới dạng chuỗi để xem lỗi thực sự là gì (thường là chữ 'S' nằm ở đây)
                    var errorRaw = await resKh.Content.ReadAsStringAsync();
                    _logger.LogError($"API Khách hàng trả về lỗi 400: {errorRaw}");
                    return BadRequest(new { message = "Thông tin khách hàng không hợp lệ", detail = errorRaw });
                }

                // Nếu thành công thì mới đọc Json
                var khData = await resKh.Content.ReadFromJsonAsync<JsonElement>();
                int maKhachHang = khData.GetProperty("maKhachHang").GetInt32();

                // --- BƯỚC 2: XỬ LÝ ĐỊA CHỈ & LẤY MÃ VÙNG H3 ---
                // Gọi Service Địa chỉ cho bên LẤY
                var resDcLay = await client.PostAsJsonAsync($"{baseServiceUrl}/quanlydiachi/check_dia_chi", request.DiaChiLay);
                if (!resDcLay.IsSuccessStatusCode) return BadRequest("Địa chỉ LẤY không hợp lệ.");

                // ĐỌC OBJECT: { maDiaChi, maVungH3 }
                var dataDcLay = await resDcLay.Content.ReadFromJsonAsync<JsonElement>();
                int maDcLay = dataDcLay.GetProperty("maDiaChi").GetInt32();
                if (maDcLay <= 0)
                {
                    return BadRequest("Mã địa chỉ không hợp lệ (ID=0).");
                }

                string maH3Nhan = dataDcLay.GetProperty("maVungH3").GetString() ?? "";

                // Gọi Service Địa chỉ cho bên GIAO
                var resDcGiao = await client.PostAsJsonAsync($"{baseServiceUrl}/quanlydiachi/check_dia_chi", request.DiaChiGiao);
                if (!resDcGiao.IsSuccessStatusCode) return BadRequest("Địa chỉ GIAO không hợp lệ.");

                var dataDcGiao = await resDcGiao.Content.ReadFromJsonAsync<JsonElement>();
                int maDcGiao = dataDcGiao.GetProperty("maDiaChi").GetInt32();
                if (maDcGiao <= 0)
                {
                    return BadRequest("Mã địa chỉ không hợp lệ (ID=0).");
                }
                string maH3Giao = dataDcGiao.GetProperty("maVungH3").GetString() ?? "";

                // --- BƯỚC 3: TÌM KHO PHỤ TRÁCH (Dùng ID địa chỉ để tính khoảng cách) ---
                int? maKhoGanNhat = null;
                var resKho = await client.GetAsync($"https://localhost:7286/api/quanlykhobai/tim-kho-gan-nhat/{maDcLay}");
                if (resKho.IsSuccessStatusCode)
                {
                    var khoJson = await resKho.Content.ReadFromJsonAsync<JsonElement>();
                    maKhoGanNhat = khoJson.GetProperty("maKho").GetInt32();
                }

                // --- BƯỚC 2: TÍNH TOÁN GIÁ CƠ BẢN TỪNG KIỆN ---
                decimal tongTienGocCacKien = 0;
                var danhSachGiaGoc = new List<decimal>();

                foreach (var kien in request.DanhSachKienHang)
                {
                    var payloadGia = new
                    {
                        ThanhPhoLay = request.DiaChiLay.ThanhPho,
                        ThanhPhoGiao = request.DiaChiGiao.ThanhPho,
                        KhoiLuongTong = kien.KhoiLuong,
                        TheTichTong = kien.TheTich,
                        MaLoaiHang = kien.MaLoaiHang,
                        MaBangGiaVung = kien.MaBangGiaVung
                    };

                    var resGiaVung = await client.PostAsJsonAsync($"{baseServiceUrl}/quanlybangiavung/phan-tich-dich-vu-phu-hop", payloadGia);

                    if (resGiaVung.IsSuccessStatusCode)
                    {
                        var options = await resGiaVung.Content.ReadFromJsonAsync<List<JsonElement>>();
                        if (options != null && options.Count > 0)
                        {
                            decimal giaDonVi = options[0].GetProperty("tongTienDuKien").GetDecimal();
                            int soLuong = (kien.SoLuongKienHang ?? 0) > 0 ? kien.SoLuongKienHang.Value : 1;
                            decimal tongGiaKien = giaDonVi * soLuong;

                            danhSachGiaGoc.Add(tongGiaKien);
                            tongTienGocCacKien += tongGiaKien;
                        }
                        else return BadRequest(new { message = $"Không tìm thấy bảng giá cho loại hàng {kien.MaLoaiHang}." });
                    }
                    else return BadRequest(new { message = "Lỗi khi kết nối API tính giá." });
                }

                // --- BƯỚC 3: HỆ SỐ DỊCH VỤ & GIẢM GIÁ ---
                decimal heSoDichVu = 1.0m;
                var resMucDo = await client.GetAsync($"{baseServiceUrl}/mucdichvu/get-by-id/{request.MaMucDoDv}");
                if (resMucDo.IsSuccessStatusCode)
                {
                    var mucDoData = await resMucDo.Content.ReadFromJsonAsync<JsonElement>();
                    heSoDichVu = mucDoData.GetProperty("heSoNhiPhan").GetDecimal();
                }

                decimal tongTienDuKien = tongTienGocCacKien * heSoDichVu;
                decimal soTienGiam = 0;
                int? maKhuyenMai = null;

                if (!string.IsNullOrEmpty(request.MaGiamGia))
                {
                    var resKM = await client.PostAsJsonAsync($"{baseServiceUrl}/quanlykhuyenmai/ap-dung", new
                    {
                        Code = request.MaGiamGia,
                        TongTienDonHang = tongTienDuKien,
                        MaKhachHang = maKhachHang
                    });
                    if (resKM.IsSuccessStatusCode)
                    {
                        var kmResponse = await resKM.Content.ReadFromJsonAsync<JsonElement>();
                        var kmData = kmResponse.GetProperty("data");
                        soTienGiam = kmData.GetProperty("soTienGiam").GetDecimal();
                        maKhuyenMai = kmData.GetProperty("maKhuyenMai").GetInt32();
                    }
                }

                decimal tongTienThucTe = Math.Max(0, tongTienDuKien - soTienGiam);

                // --- BƯỚC 4: LƯU ĐƠN HÀNG ---
                var newDonHang = new QuanLyDonHang.Models.DonHang
                {
                    TenDonHang = request.TenDonHang ?? $"Đơn {DateTime.Now:HHmm}",
                    MaKhachHang = maKhachHang,
                    MaDiaChiNhanHang = maDcLay,
                    MaDiaChiLayHang = maDcGiao,

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

                // --- BƯỚC 5: LƯU KIỆN HÀNG ---
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
                        MaLoaiHang = kienReq.MaLoaiHang,
                        MaBangGiaVung = kienReq.MaBangGiaVung,
                        MaVach = "BILL" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper(),
                        SoTien = danhSachGiaGoc[i],
                        DaThuGom = false
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
                await transaction.CommitAsync();

                var responseData = new
                {
                    Success = true,
                    MaDonHang = newDonHang.MaDonHang,
                    MaKho = maKhoGanNhat,
                    TongTien = tongTienThucTe
                };

                // --- LUỒNG HỎA TỐC (Mức độ 3) ---
                if (request.MaMucDoDv?.ToString() == "3")
                {
                    try
                    {
                        var rabbitMQ = new RabbitMQProducer(); // Tốt nhất nên dùng DI (Dependency Injection)
                        var message = new
                        {
                            MaDonHang = newDonHang.MaDonHang,
                            MaKhoVao = maKhoGanNhat,
                            MaDiaChiLay = maDcLay,
                            TongKhoiLuong = request.DanhSachKienHang.Sum(k => k.KhoiLuong),
                            TongTheTich = request.DanhSachKienHang.Sum(k => k.TheTich),
                            ThoiGian = DateTime.Now
                        };
                        await rabbitMQ.SendOrderMessageAsync(message);
                    }
                    catch (Exception ex)
                    {
                        // Chỉ log lỗi RabbitMQ, không làm fail cả request vì DB đã lưu thành công
                        _logger.LogError($"Lỗi gửi tin nhắn RabbitMQ cho đơn {newDonHang.MaDonHang}: {ex.Message}");
                    }
                }

                
                // --- BƯỚC 6: TẠO LINK THANH TOÁN MOMO TRỰC TIẾP ---
                string paymentUrl = "";

                if (request.MaPTTT == 3) // Giả sử ID 3 là thanh toán qua Ví Momo
                {
                    // 1. Chuẩn bị dữ liệu gửi sang Momo
                    string orderId = newDonHang.MaDonHang.ToString() + "_" + DateTime.Now.Ticks; // Đảm bảo Unique
                    string requestId = Guid.NewGuid().ToString();
                    string orderInfo = "Thanh toán đơn hàng #" + newDonHang.MaDonHang;
                    string redirectUrl = "https://localhost:7149/api/thanhtoan/momo-callback"; // Link quay lại web sau khi thanh toán
                    string ipnUrl = "https://your-domain.com/api/thanhtoan/momo-ipn"; // Link Momo gọi ngầm để cập nhật DB
                    string amount = ((long)tongTienThucTe).ToString();
                    string extraData = ""; // Có thể để trống hoặc lưu thông tin thêm dạng Base64

                    // 2. Tạo chuỗi dữ liệu để ký tên (Raw Signature) theo thứ tự bảng chữ cái của Key
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

                    // 3. Ký số SHA256 với Secret Key
                    string signature = "";
                    using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
                    {
                        byte[] hashValue = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawHash));
                        signature = BitConverter.ToString(hashValue).Replace("-", "").ToLower();
                    }

                    // 4. Tạo Object JSON để gửi POST sang Momo
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

                    // 5. Gọi API Momo để lấy link thanh toán (PayUrl)
                    var responseMomo = await client.PostAsJsonAsync(endpoint, message);
                    if (responseMomo.IsSuccessStatusCode)
                    {
                        var resultMomo = await responseMomo.Content.ReadFromJsonAsync<JsonElement>();
                        // Momo trả về 'payUrl' nếu thành công
                        if (resultMomo.TryGetProperty("payUrl", out var urlElement))
                        {
                            paymentUrl = urlElement.GetString();
                        }
                    }
                }

                

                return Ok(new
                {
                    Success = true,
                    MaDonHang = newDonHang.MaDonHang,
                    H3 = maH3Nhan,
                    TongTien = tongTienThucTe,
                    PaymentUrl = paymentUrl // Trả về URL để FE redirect người dùng đi thanh toán (nếu rỗng thì thôi)
                });
            }


            catch (Exception ex)
            {

                _logger.LogError($"[Fatal Error] TaoDonHang: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi hệ thống", detail = ex.Message });
            }
        }

        private bool VerifyMomoSignature(MomoIPNRequest request)
        {
            // 1. Thông tin SecretKey (Phải khớp với key lúc bạn gửi đi)
            string secretKey = "at67bcvdghas78nhj";

            // 2. Tạo chuỗi rawHash theo đúng thứ tự Momo quy định (A-Z)
            // Lưu ý: Không được thiếu bất kỳ trường nào dưới đây
            string rawHash = $"accessKey={accessKey}" + // Bạn cần biến accessKey ở đây
                             $"&amount={request.amount}" +
                             $"&extraData={request.extraData}" +
                             $"&message={request.message}" +
                             $"&orderId={request.orderId}" +
                             $"&orderInfo={request.orderInfo}" +
                             $"&partnerCode={request.partnerCode}" +
                             $"&requestId={request.requestId}" +
                             $"&responseTime={request.responseTime}" +
                             $"&resultCode={request.resultCode}" +
                             $"&transId={request.transId}";

            // 3. Tính toán lại chữ ký từ chuỗi rawHash
            string checkSignature = "";
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
            {
                byte[] hashValue = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawHash));
                checkSignature = BitConverter.ToString(hashValue).Replace("-", "").ToLower();
            }

            // 4. So sánh chữ ký mình vừa tính với chữ ký Momo gửi sang
            return checkSignature == request.signature;
        }

        [HttpPost("momo-ipn")]
        public async Task<IActionResult> MomoIPN([FromBody] MomoIPNRequest request)
        {
            // 1. Kiểm tra chữ ký (Signature) để đảm bảo tin nhắn này đúng là từ Momo gửi, không phải hacker
            bool isValid = VerifyMomoSignature(request);
            if (!isValid) return BadRequest();

            // 2. Kiểm tra mã kết quả (resultCode == 0 là thành công)
            if (request.resultCode == 0)
            {
                // Tìm đơn hàng dựa trên MaDonHang (TxnRef) mà Momo gửi về
                var donHang = await _context.DonHangs.FirstOrDefaultAsync(d => d.MaDonHang == request.orderId);
                var hoaDon = await _context.HoaDons.FirstOrDefaultAsync(h => h.MaDonHang == request.orderId);

                if (donHang != null && hoaDon != null)
                {
                    // BƯỚC CẬP NHẬT TRẠNG THÁI:

                    // 1. Cập nhật bảng Hóa đơn (Lưu vết giao dịch chi tiết)
                    hoaDon.TrangThaiThanhToan = "Thanh_Cong";
                    hoaDon.MaGiaoDichNgoai = request.transId; // Lưu mã của Momo để đối soát
                    hoaDon.NgayThanhToan = DateTime.Now;

                    // 2. Cập nhật bảng Đơn hàng (Để quản lý tổng thể)
                    donHang.TrangThaiThanhToanTong = "Đã thanh toán";
                    donHang.TrangThaiHienTai = "Đang xử lý"; // Có thể chuyển luôn sang trạng thái chuẩn bị kho

                    await _context.SaveChangesAsync();
                }
            }

            // Trả về cho Momo biết bạn đã nhận được thông tin (để họ không gọi lại nữa)
            return NoContent();
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

                            // QUAN TRỌNG: Bắn mã địa chỉ về để phía Điều phối tìm được Kho gần nhất
                            // Sử dụng MaDiaChiNhanHang (nơi lấy hàng) làm điểm mốc tìm kho
                            MaDiaChiCum = representativeOrder.MaDiaChiNhanHang ?? 0,
                            MaDiaChiLayHang = representativeOrder.MaDiaChiNhanHang ?? 0,
                            MaDiaChiGiao = representativeOrder.MaDiaChiLayHang,

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
    }
}