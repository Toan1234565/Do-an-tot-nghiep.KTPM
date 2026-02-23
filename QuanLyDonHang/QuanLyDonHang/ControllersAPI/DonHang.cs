using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyDonHang.Models;
using QuanLyDonHang.Models1;
using System.Net.Http;
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

                        MaDiaChiGiao = dh.MaDiaChiGiao,
                        // Kiểm tra null để tránh lỗi nếu KienHangs trống                     
                        TenDonHang = dh.TenDonHang,
                        ThoiGianTao = dh.ThoiGianTao,
                        TrangThaiHienTai = dh.TrangThaiHienTai,
                        MaLoaiDv = dh.MaLoaiDv,
                        LaDonGiaoThang = dh.LaDonGiaoThang,
                        MaDiaChiLayHang = dh.MaDiaChiNhanHang,

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
                        MaDiaChiGiao = dh.MaDiaChiGiao,
                        TrangThaiHienTai = dh.TrangThaiHienTai,
                        MaLoaiDv = dh.MaLoaiDv,
                        MaHopDongNgoai = dh.MaHopDongNgoai,
                        GhiChuDacBiet = dh.GhiChuDacBiet,
                        LaDonGiaoThang = dh.LaDonGiaoThang,
                        MaVung = dh.MaVung,
                        MaDiaChiLayHang = dh.MaDiaChiNhanHang,
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
                            DaThanhToan = kh.DaThanhToan,
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
                        MaDiaChiGiao = dh.MaDiaChiGiao,
                        MaDiaChiLayHang = dh.MaDiaChiNhanHang,
                        TenNguoiNhan = dh.TenNguoiNhan,
                        SdtNguoiNhan = dh.SdtNguoiNhan,
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
            // Kiểm tra đầu vào cơ bản
            if (request == null || request.DanhSachKienHang == null || !request.DanhSachKienHang.Any())
                return BadRequest("Thông tin đơn hàng hoặc danh sách kiện hàng không được để trống.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // --- BƯỚC 1: XÁC THỰC KHÁCH HÀNG & LẤY TỌA ĐỘ (SERVER 2) ---
                var resKh = await _httpClient.PostAsJsonAsync("https://localhost:7149/api/quanlykhachhang/check_so_dien_thoai", new
                {
                    SoDienThoai = request.SoDienThoai,
                    TenLienHe = request.TenKhachHang,
                    DiaChi = request.DiaChiLay
                });

                if (!resKh.IsSuccessStatusCode) return StatusCode((int)resKh.StatusCode, "Lỗi xác thực khách hàng.");

                var dataKh = await resKh.Content.ReadFromJsonAsync<JsonElement>();
                int maKhachHang = dataKh.GetProperty("maKhachHang").GetInt32();

                // Cập nhật tọa độ nếu Server 2 trả về
                if (dataKh.TryGetProperty("toaDo", out JsonElement toaDo) && request.DiaChiLay != null)
                {
                    request.DiaChiLay.ViDo = toaDo.GetProperty("lat").GetDouble();
                    request.DiaChiLay.KinhDo = toaDo.GetProperty("lon").GetDouble();
                }

                // --- BƯỚC 2: XỬ LÝ MÃ ID ĐỊA CHỈ (SERVER 2) ---
                var resDcLay = await _httpClient.PostAsJsonAsync("https://localhost:7149/api/quanlydiachi/check_dia_chi", request.DiaChiLay);
                int maDcLay = await resDcLay.Content.ReadFromJsonAsync<int>();

                var resDcGiao = await _httpClient.PostAsJsonAsync("https://localhost:7149/api/quanlydiachi/check_dia_chi", request.DiaChiGiao);
                int maDcGiao = await resDcGiao.Content.ReadFromJsonAsync<int>();

                // --- BƯỚC 3: PHÂN TÍCH GIÁ CƯỚC (SERVER 2) ---
                var yeuCauPhanTich = new
                {
                    ThanhPhoLay = request.DiaChiLay?.ThanhPho,
                    ThanhPhoGiao = request.DiaChiGiao?.ThanhPho,
                    ViDoLay = request.DiaChiLay?.ViDo ?? 0,
                    KinhDoLay = request.DiaChiLay?.KinhDo ?? 0,
                    ViDoGiao = request.DiaChiGiao?.ViDo ?? 0,
                    KinhDoGiao = request.DiaChiGiao?.KinhDo ?? 0,
                    DanhSachKienHang = request.DanhSachKienHang.Select(k => new {
                        k.KhoiLuong,
                        k.TheTich,
                        MaLoaiHang = k.MaLoaiHang ?? 1,
                        SoLuongKienHang = k.SoLuongKienHang
                    }).ToList()
                };

                var resPhanTich = await _httpClient.PostAsJsonAsync("https://localhost:7149/api/mucdichvu/phan-tich-muc-do-phu-hop", yeuCauPhanTich);
                if (!resPhanTich.IsSuccessStatusCode) return BadRequest("Không thể tính toán giá cước.");

                var dataGia = await resPhanTich.Content.ReadFromJsonAsync<JsonElement>();
                var luaChonDichVu = dataGia.GetProperty("luaChonDichVu").EnumerateArray();

                // Tìm đúng MaMucDo khách chọn hoặc lấy mặc định cái đầu tiên
                var dvDuocChon = luaChonDichVu.FirstOrDefault(x => x.GetProperty("maMucDo").GetInt32() == request.MaMucDoChon);
                if (dvDuocChon.ValueKind == JsonValueKind.Undefined) dvDuocChon = luaChonDichVu.First();

                decimal tongTienGocDonHang = dvDuocChon.GetProperty("giaDuKien").GetDecimal();
                int maDichVuFinal = dvDuocChon.GetProperty("maMucDo").GetInt32();

                // --- BƯỚC 4: XỬ LÝ KHUYẾN MÃI & ĐIỂM THƯỞNG (GỌI SERVER 2) ---
                decimal soTienGiamKM = 0;
                decimal soTienGiamDiem = 0;

                // 4.1. Check Voucher
                if (!string.IsNullOrEmpty(request.MaGiamGia))
                {
                    var resKM = await _httpClient.GetAsync($"https://localhost:7149/api/KhuyenMai/GetByCode?code={request.MaGiamGia}");
                    if (resKM.IsSuccessStatusCode)
                    {
                        var kmData = await resKM.Content.ReadFromJsonAsync<JsonElement>();
                        DateTime now = DateTime.Now;

                        // Đọc an toàn các trường từ JSON
                        DateTime ngayBatDau = kmData.GetProperty("ngayBatDau").GetDateTime();
                        DateTime ngayKetThuc = kmData.GetProperty("ngayKetThuc").GetDateTime();
                        decimal donHangToiThieu = kmData.TryGetProperty("donHangToiThieu", out JsonElement dhtt) ? dhtt.GetDecimal() : 0;
                        int soLuongDaDung = kmData.GetProperty("soLuongDaDung").GetInt32();
                        int? soLuongToiDa = kmData.TryGetProperty("soLuongToiDa", out JsonElement sltd) && sltd.ValueKind != JsonValueKind.Null ? sltd.GetInt32() : null;

                        if (now >= ngayBatDau && now <= ngayKetThuc && tongTienGocDonHang >= donHangToiThieu && (soLuongToiDa == null || soLuongDaDung < soLuongToiDa))
                        {
                            string kieuGiam = kmData.GetProperty("kieuGiamGia").GetString();
                            decimal giaTriGiam = kmData.GetProperty("giaTriGiam").GetDecimal();
                            soTienGiamKM = (kieuGiam == "Phần trăm") ? (tongTienGocDonHang * giaTriGiam / 100) : giaTriGiam;
                        }
                    }
                }

                // 4.2. Check Điểm thưởng
                if (request.SoDiemDoi > 0)
                {
                    var resDiem = await _httpClient.GetAsync($"https://localhost:7149/api/quanlykhachhang/CheckDiem?maKhachHang={maKhachHang}");
                    if (resDiem.IsSuccessStatusCode)
                    {
                        var dataDiem = await resDiem.Content.ReadFromJsonAsync<JsonElement>();
                        if (dataDiem.GetProperty("diemHienCo").GetInt32() >= request.SoDiemDoi)
                        {
                            soTienGiamDiem = request.SoDiemDoi * 1000; // Quy đổi 1đ = 1000 VNĐ
                        }
                    }
                }

                // --- BƯỚC 5: TỔNG HỢP CHI PHÍ ---
                decimal tongPhaiTra = Math.Max(0, tongTienGocDonHang - (soTienGiamKM + soTienGiamDiem));

                // --- BƯỚC 6: LƯU ĐƠN HÀNG (SERVER 1) ---
                var newDonHang = new QuanLyDonHang.Models.DonHang
                {
                    MaKhachHang = maKhachHang,
                    ThoiGianTao = DateTime.Now,
                    MaDiaChiNhanHang = maDcLay,
                    MaDiaChiGiao = maDcGiao,
                    TrangThaiHienTai = "Chờ lấy hàng",
                    MaLoaiDv = maDichVuFinal,
                    TenDonHang = $"Đơn hàng {request.SoDienThoai}",
                    TenNguoiNhan = request.TenNguoiNhan,
                    SdtNguoiNhan = request.SdtNguoiNhan,
                    TongTienDuKien = tongTienGocDonHang,
                    TongTienThucTe = tongPhaiTra,
                    MaMucDoDv = maDichVuFinal,
                    GhiChuDacBiet = $"Gốc: {tongTienGocDonHang:N0} - KM: {soTienGiamKM:N0} - Điểm: {soTienGiamDiem:N0}"
                    //GhiChu = $"Gốc: {tongTienGocDonHang:N0} - KM: {soTienGiamKM:N0} - Điểm: {soTienGiamDiem:N0}"
                };
                _context.DonHangs.Add(newDonHang);
                await _context.SaveChangesAsync();

                // --- BƯỚC 7: LƯU KIỆN HÀNG (SERVER 1) ---
                foreach (var kienReq in request.DanhSachKienHang)
                {
                    _context.KienHangs.Add(new KienHang
                    {
                        MaDonHang = newDonHang.MaDonHang,
                        KhoiLuong = kienReq.KhoiLuong,
                        TheTich = kienReq.TheTich,
                        SoLuongKienHang = kienReq.SoLuongKienHang,
                        MaLoaiHang = kienReq.MaLoaiHang,
                        SoTien = Math.Round(tongPhaiTra / request.DanhSachKienHang.Count, 2),
                        MaVach = "SPX" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
                    });
                }
                await _context.SaveChangesAsync();

                // --- BƯỚC 8: ĐỒNG BỘ HẬU MÃI (SERVER 2) ---
                // Gọi API duy nhất để trừ voucher, trừ điểm đã dùng và cộng điểm mới
                await _httpClient.PostAsJsonAsync("https://localhost:7149/api/cauhinhtichdiem/xu-ly-hau-mai", new
                {
                    MaKhachHang = maKhachHang,
                    MaDonHang = newDonHang.MaDonHang,
                    MaGiamGia = request.MaGiamGia,
                    SoDiemDaDoi = request.SoDiemDoi,
                    SoTienThanhToan = tongPhaiTra
                });

                await transaction.CommitAsync();
                return Ok(new
                {
                    Success = true,
                    MaDonHang = newDonHang.MaDonHang,
                    TongTien = tongPhaiTra,
                    GiamGia = soTienGiamKM + soTienGiamDiem
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }
    }
}