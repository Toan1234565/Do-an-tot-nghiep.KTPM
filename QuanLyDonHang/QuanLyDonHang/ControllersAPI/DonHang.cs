using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using QuanLyDonHang.Models;
using QuanLyDonHang.Models1;
using QuanLyDonHang.Models1.QuanLyDieuPhoiGomHang;
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
        string BaseUrl = "https://localhost:7149/api";
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

                if (!resKh.IsSuccessStatusCode) return BadRequest("Lỗi xác thực thông tin khách hàng.");
                var khData = await resKh.Content.ReadFromJsonAsync<JsonElement>();
                int maKhachHang = khData.GetProperty("maKhachHang").GetInt32();

                // Check địa chỉ lấy và giao
                var resDcLay = await client.PostAsJsonAsync($"{baseServiceUrl}/quanlydiachi/check_dia_chi", request.DiaChiLay);
                if (resDcLay.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var errorMsg = await resDcLay.Content.ReadFromJsonAsync<JsonElement>();
                    return BadRequest(new { message = $"Địa chỉ LẤY hàng không hợp lệ: {errorMsg.GetProperty("message").GetString()}" });
                }

                var resDcGiao = await client.PostAsJsonAsync($"{baseServiceUrl}/quanlydiachi/check_dia_chi", request.DiaChiGiao);
                if (resDcGiao.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var errorMsg = await resDcGiao.Content.ReadFromJsonAsync<JsonElement>();
                    return BadRequest(new { message = $"Địa chỉ GIAO hàng không hợp lệ: {errorMsg.GetProperty("message").GetString()}" });
                }
                
                var resH3Nhan = await client.PostAsJsonAsync($"{baseServiceUrl}/quanlydiachi/check_dia_chi", request.H3Nhan);
                var resH3Giao = await client.PostAsJsonAsync($"{baseServiceUrl}/quanlydiachi/check_dia_chi", request.H3Giao);
                if (!resDcLay.IsSuccessStatusCode || !resDcGiao.IsSuccessStatusCode)
                    return BadRequest("Lỗi xác thực địa chỉ lấy/giao hàng.");

                int maDcLay = await resDcLay.Content.ReadFromJsonAsync<int>();
                int maDcGiao = await resDcGiao.Content.ReadFromJsonAsync<int>();

                var dataNhan = await resH3Nhan.Content.ReadFromJsonAsync<JsonElement>();
                var dataGiao = await resH3Giao.Content.ReadFromJsonAsync<JsonElement>();
                string maH3Nhan = "";
                string maH3Giao = "";
                if (dataNhan.TryGetProperty("maVungH3", out var pH3Nhan)) maH3Nhan = pH3Nhan.GetString() ?? "";
                if (dataGiao.TryGetProperty("maVungH3", out var pH3Giao)) maH3Giao = pH3Giao.GetString() ?? "";




                // --- BƯỚC 1.5: TÌM KHO GẦN NHẤT (Thực hiện trước để lưu vào DB) ---
                int? maKhoGanNhat = null;
                try
                {
                    // Gọi Service Kho Bãi (Đảm bảo port 7286 đang chạy)
                    var resKho = await client.GetAsync($"https://localhost:7286/api/quanlykhobai/tim-kho-gan-nhat/{maDcLay}");
                    if (resKho.IsSuccessStatusCode)
                    {
                        var khoJson = await resKho.Content.ReadFromJsonAsync<JsonElement>();
                        if (khoJson.TryGetProperty("maKho", out var maKhoProp))
                        {
                            maKhoGanNhat = maKhoProp.GetInt32();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Lỗi tìm kho tự động: {ex.Message}. Sẽ xử lý điều phối sau.");
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
                    MaDiaChiGiao = maDcGiao,

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
                    MaVungH3Nhan = maH3Nhan
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

                return Ok(responseData);
            }


            catch (Exception ex)
            {

                _logger.LogError($"[Fatal Error] TaoDonHang: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi hệ thống", detail = ex.Message });
            }
        }

        [HttpPost("cho-dieu-phoi")]
        public async Task<IActionResult> TuDongGomNhomDonHang([FromBody] ClusterRequest request)
        {
            try
            {
                // 1. Eager Loading + AsNoTracking: Tối ưu bộ nhớ và tốc độ DB
                var donHangs = await _context.DonHangs
                    .Include(dh => dh.KienHangs)
                    .Where(dh => dh.TrangThaiHienTai == "Chờ lấy hàng"
                              && dh.MaDiaChiNhanHang != null 
                              
                              && dh.MaMucDoDv != 3)
                    .AsNoTracking()
                    .ToListAsync();

                if (!donHangs.Any())
                    return NotFound(new { message = "Không có đơn hàng cần thu gom." });

                // 2. Tính toán trực tiếp trên RAM bằng SelectMany
                var clusters = donHangs
                    .GroupBy(dh => dh.MaDiaChiNhanHang)
                    .Select(group => {
                        var allKienHangs = group.SelectMany(dh => dh.KienHangs).ToList();

                        return new ClusterResult
                        {
                            MaDiaChiCum = group.Key ?? 0,
                            SoLuongDonHang = group.Count(),
                            DanhSachMaDonHang = group.Select(dh => dh.MaDonHang).ToList(),
                            TongKhoiLuong = allKienHangs.Sum(kh => kh.KhoiLuong ?? 0),
                            TongTheTich = allKienHangs.Sum(kh => kh.TheTich ?? 0)
                        };
                    })
                    .OrderByDescending(c => c.SoLuongDonHang)
                    .ToList();

                // 3. Xử lý Cache (Nên dùng Redis để tối ưu hơn CancellationToken)
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new { TotalClusters = clusters.Count, Clusters = clusters });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gom nhóm.");
                return StatusCode(500, "Internal Server Error");
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