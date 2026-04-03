using Azure.Core;
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
                    .Select(dh => new DSDonHangModels
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


        // xem chi tiet don hang o quan ly don  hang
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
                        MaDonHang = dh.MaDonHang,
                        MaKhachHang = dh.MaKhachHang,
                        TenDonHang = dh.TenDonHang,
                        ThoiGianTao = dh.ThoiGianTao,
                        MaDiaChiLayHang = dh.MaDiaChiLayHang,
                        TrangThaiHienTai = dh.TrangThaiHienTai,
                        MaLoaiDv = dh.MaLoaiDv,
                        MaHopDongNgoai = dh.MaHopDongNgoai,
                        GhiChuDacBiet = dh.GhiChuDacBiet,
                        LaDonGiaoThang = dh.LaDonGiaoThang,
                       
                        MaDiaChiNhanHang = dh.MaDiaChiNhanHang ?? 0,
                        TenNguoiNhan = dh.TenNguoiNhan,
                        SdtNguoiNhan = dh.SdtNguoiNhan,
                        TongTienDuKien = dh.TongTienDuKien,
                        TongTienThucTe = dh.TongTienThucTe,
                        MaMucDoDv = dh.MaMucDoDv,
                        TrangThaiThanhToanTong = dh.TrangThaiThanhToanTong,
                        TenPhuongThucTT = dh.MaPtttNavigation.LoaiThanhToan,
                        MaKhoHienTai = dh.MaKhoHienTai,
                        MaVungH3Giao =dh.MaVungH3Giao,
                        MaVungH3Nhan = dh.MaVungH3Nhan,
                        
                        KienHangs = dh.KienHangs.Select(kh => new KienHangModels
                        {
                            MaVach = kh.MaVach,
                            KhoiLuong = kh.KhoiLuong,
                            TheTich = kh.TheTich,
                            DaThuGom = kh.DaThuGom,
                            SoTien = kh.SoTien,
                            MaBangGiaVung = kh.MaBangGiaVung,
                            //SoLuongKienHang = kh.SoLuongKienHang,
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

                if (!resKh.IsSuccessStatusCode)
                {
                    var errorRaw = await resKh.Content.ReadAsStringAsync();
                    _logger.LogError($"API Khách hàng trả về lỗi 400: {errorRaw}");
                    return BadRequest(new { message = "Thông tin khách hàng không hợp lệ", detail = errorRaw });
                }

                var khData = await resKh.Content.ReadFromJsonAsync<JsonElement>();
                int maKhachHang = khData.GetProperty("maKhachHang").GetInt32();

                // --- BƯỚC 2: XỬ LÝ ĐỊA CHỈ & LẤY MÃ VÙNG H3 ---
                var resDcLay = await client.PostAsJsonAsync($"{baseServiceUrl}/quanlydiachi/check_dia_chi", request.DiaChiLay);
                if (!resDcLay.IsSuccessStatusCode) return BadRequest("Địa chỉ LẤY không hợp lệ.");

                var dataDcLay = await resDcLay.Content.ReadFromJsonAsync<JsonElement>();
                int maDcLay = dataDcLay.GetProperty("maDiaChi").GetInt32();
                if (maDcLay <= 0) return BadRequest("Mã địa chỉ không hợp lệ (ID=0).");
                string maH3Nhan = dataDcLay.GetProperty("maVungH3").GetString() ?? "";

                var resDcGiao = await client.PostAsJsonAsync($"{baseServiceUrl}/quanlydiachi/check_dia_chi", request.DiaChiGiao);
                if (!resDcGiao.IsSuccessStatusCode) return BadRequest("Địa chỉ GIAO không hợp lệ.");

                var dataDcGiao = await resDcGiao.Content.ReadFromJsonAsync<JsonElement>();
                int maDcGiao = dataDcGiao.GetProperty("maDiaChi").GetInt32();
                if (maDcGiao <= 0) return BadRequest("Mã địa chỉ không hợp lệ (ID=0).");
                string maH3Giao = dataDcGiao.GetProperty("maVungH3").GetString() ?? "";

                // --- BƯỚC 3: TÌM KHO PHỤ TRÁCH ---
                int? maKhoGanNhat = null;
                var resKho = await client.GetAsync($"https://localhost:7286/api/quanlykhobai/tim-kho-gan-nhat/{maDcLay}");
                if (resKho.IsSuccessStatusCode)
                {
                    var khoJson = await resKho.Content.ReadFromJsonAsync<JsonElement>();
                    maKhoGanNhat = khoJson.GetProperty("maKho").GetInt32();
                }

                // --- BƯỚC 4: TÍNH TOÁN GIÁ & HỆ SỐ ---
                decimal tongTienGocCacKien = 0;
                var danhSachGiaGoc = new List<decimal>();

                // --- BƯỚC 4: TÍNH TOÁN GIÁ & HỆ SỐ ---
                foreach (var kien in request.DanhSachKienHang)
                {
                    var payloadGia = new
                    {
                        ThanhPhoLay = request.DiaChiLay.ThanhPho?.Trim(),
                        ThanhPhoGiao = request.DiaChiGiao.ThanhPho?.Trim(),
                        KhoiLuongTong = kien.KhoiLuong,
                        TheTichTong = kien.TheTich,
                        MaLoaiHang = kien.MaLoaiHang
                        // Lưu ý: Không truyền MaBangGiaVung vào payload nếu API phân tích không dùng nó làm tham số đầu vào để lọc
                    };

                    var resGiaVung = await client.PostAsJsonAsync($"https://localhost:7149/api/quanlybangiavung/phan-tich-dich-vu-phu-hop", payloadGia);

                    if (resGiaVung.IsSuccessStatusCode)
                    {
                        var options = await resGiaVung.Content.ReadFromJsonAsync<List<JsonElement>>();
                        if (options != null && options.Count > 0)
                        {
                            // SỬA TẠI ĐÂY: Tìm đúng Option mà khách hàng đã chọn dựa trên MaBangGiaVung
                            var selectedOption = options.FirstOrDefault(o => o.GetProperty("maBangGia").GetInt32() == kien.MaBangGiaVung);

                            // Nếu không tìm thấy mã khớp (do dữ liệu cũ hoặc lệch), mặc định lấy cái đầu tiên nhưng nên báo lỗi
                            if (selectedOption.ValueKind == JsonValueKind.Undefined)
                            {
                                return BadRequest(new { message = $"Mã bảng giá {kien.MaBangGiaVung} không còn hiệu lực cho kiện hàng này." });
                            }

                            decimal giaDonVi = selectedOption.GetProperty("tongTienDuKien").GetDecimal();
                            int soLuong = (kien.SoLuongKienHang ?? 0) > 0 ? kien.SoLuongKienHang.Value : 1;
                            decimal tongGiaKien = giaDonVi * soLuong;

                            danhSachGiaGoc.Add(tongGiaKien);
                            tongTienGocCacKien += tongGiaKien;
                        }
                        else return BadRequest(new { message = "Không tìm thấy bảng giá phù hợp." });
                    }
                }

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

                // --- BƯỚC 5: LƯU ĐƠN HÀNG, KIỆN HÀNG, HÓA ĐƠN ---
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

                // --- BƯỚC 6: XỬ LÝ THANH TOÁN & QR CODE ---
                string paymentUrl = "";
                string qrCodeUrl = ""; // Thêm biến chứa link QR Code

                // Giả sử MaPTTT == 2 là Chuyển khoản ngân hàng
                if (request.MaPTTT == 2)
                {
                    string bankId = "MB"; // Mã ngân hàng (Thay bằng mã NH thực tế của bạn, ví dụ: MB, VCB, CTG, TCB)
                    string accountNo = "0833508903"; // Số tài khoản nhận tiền
                    string accountName = "NGUYEN DUC TOAN"; // Tên chủ tài khoản
                    string addInfo = $"Thanh toan don hang {newDonHang.MaDonHang}"; // Nội dung CK

                    // Build link API VietQR
                    qrCodeUrl = $"https://img.vietqr.io/image/{bankId}-{accountNo}-compact2.png?amount={(long)tongTienThucTe}&addInfo={Uri.EscapeDataString(addInfo)}&accountName={Uri.EscapeDataString(accountName)}";
                }
                // MaPTTT == 3 là Ví Momo
                else if (request.MaPTTT == 3)
                {
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
                        {
                            paymentUrl = urlElement.GetString();
                        }

                        // Trích xuất thêm mã QR mà MoMo trả về
                        if (resultMomo.TryGetProperty("qrCodeUrl", out var qrElement))
                        {
                            qrCodeUrl = qrElement.GetString();
                        }
                    }
                }

                // --- LUỒNG HỎA TỐC (Mức độ 3) ---
                if (request.MaMucDoDv?.ToString() == "3")
                {
                    try
                    {
                        var rabbitMQ = new RabbitMQProducer();
                        var message = new
                        {
                            MaDonHang = newDonHang.MaDonHang,
                            MaDiaChiLayHang = maDcLay,
                            MaDiaChiGiaoHang = maDcGiao,
                            MaKhoVao = maKhoGanNhat ?? request.MaKhoHienTai,
                            TongKhoiLuong = request.DanhSachKienHang.Sum(k => k.KhoiLuong),
                            TongTheTich = request.DanhSachKienHang.Sum(k => k.TheTich),
                            ThoiGian = DateTime.Now
                        };
                        await rabbitMQ.SendOrderMessageAsync(message);
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
                    QrCodeUrl = qrCodeUrl // Frontend sẽ hứng biến này để nhúng vào thẻ <img>
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Fatal Error] TaoDonHang: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi hệ thống", detail = ex.Message });
            }
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
                            MaDiaChiLayHang = representativeOrder.MaDiaChiLayHang ,
                            MaDiaChiCum = representativeOrder.MaDiaChiLayHang ,
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