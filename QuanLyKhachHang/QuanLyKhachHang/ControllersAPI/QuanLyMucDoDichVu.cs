using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.Index.HPRtree;
using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1.QuanLyMucDoDichVu;
using System;
using System.Linq;
using System.Threading.Tasks;
using Tmdt.Shared.Services;
using System.Data;
using static QuanLyKhachHang.Models1.QuanLyMucDoDichVu.YeuCauPhanTich;

namespace QuanLyKhachHang.ControllersAPI
{
    [Route("api/mucdichvu")]
    [ApiController]
    public class QuanLyMucDoDichVu : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<QuanLyMucDoDichVu> _logger; // Sửa lại ILogger
        private readonly IMemoryCache _cache;
        private readonly IFlightService _flightService;
        private readonly ISystemService _sys;
        private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();
        private const string ServiceLevelCacheKey = "ServiceLevelList";

        public QuanLyMucDoDichVu(TmdtContext context, ILogger<QuanLyMucDoDichVu> logger, IMemoryCache cache, IFlightService flightService, ISystemService sys)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _flightService = flightService;
            _sys = sys;
        }

        // HÀM DÙNG CHUNG ĐỂ LÀM MỚI CACHE
        private void ClearPriceRegionCache()
        {
            // Hủy token cũ -> Tất cả cache gắn với token này sẽ tự động bị xóa
            if (!_resetCacheToken.IsCancellationRequested)
            {
                _resetCacheToken.Cancel();
                _resetCacheToken.Dispose();
            }
            // Tạo token mới cho các lượt cache tiếp theo
            _resetCacheToken = new CancellationTokenSource();
            _logger.LogInformation("Đã làm mới toàn bộ Cache Bảng giá vùng.");
        }

        [HttpGet("dsmucdichvu")]
        public async Task<IActionResult> DanhSach([FromQuery] string? search, [FromQuery] string? trangThai = null)
        {
            // Tạo cache key duy nhất dựa trên các tham số lọc
            string cacheKey = $"{ServiceLevelCacheKey}_{trangThai}_{search}";

            if (!_cache.TryGetValue(cacheKey, out object listService))
            {
                try
                {
                    var query = _context.MucDoDichVus.AsNoTracking();

                    // 1. Logic lọc theo Trạng thái
                    if (!string.IsNullOrEmpty(trangThai))
                    {
                        if (trangThai == "active") // Đang hoạt động
                        {
                            query = query.Where(m => m.TrangThai == true);
                        }
                        else if (trangThai == "paused") // Vô hiệu hóa
                        {
                            query = query.Where(m => m.TrangThai == false);
                        }
                    }

                    // 2. Logic lọc theo Từ khóa tìm kiếm (Tên hoặc Mã)
                    if (!string.IsNullOrEmpty(search))
                    {
                        string searchLower = search.ToLower();
                        query = query.Where(m => m.TenDichVu.ToLower().Contains(searchLower)
                                              || m.MaDichVu.ToString().Contains(searchLower));
                    }

                    listService = await query
                        .OrderByDescending(m => m.NgayBatDau)
                        .Select(m => new MucDoDichVuModels
                        {
                            MaDichVu = m.MaDichVu,
                            TenDichVu = m.TenDichVu,
                            ThoiGianCamKet = m.ThoiGianCamKet,
                            HeSoNhiPhan = m.HeSoNhiPhan,
                            TrangThai = m.TrangThai,
                            LaCaoCap = m.LaCaoCap,
                            NgayBatDau = m.NgayBatDau,
                        })
                        .ToListAsync();

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

                    _cache.Set(cacheKey, listService, cacheOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi lọc danh sách.");
                    return StatusCode(500, "Lỗi hệ thống.");
                }
            }
            return Ok(listService);
        }

        [Authorize]
        [HttpPost("themmoi")]
        public async Task<IActionResult> ThemMoi([FromBody] MucDoDichVuModels model)
        {
            if (model == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            try
            {
                _logger.LogInformation("Đang thêm mới mức độ dịch vụ: {TenDichVu}", model.TenDichVu);

                // Ánh xạ từ Model vào Entity (Giả sử Entity của bạn là MucDoDichVu)
                var entity = new MucDoDichVu
                {
                    TenDichVu = model.TenDichVu,
                    ThoiGianCamKet = model.ThoiGianCamKet,
                    HeSoNhiPhan = model.HeSoNhiPhan,
                    TrangThai = model.TrangThai,
                    LaCaoCap = model.LaCaoCap,
                    NgayBatDau = DateTime.Now

                };

                _context.MucDoDichVus.Add(entity);
                await _context.SaveChangesAsync();

                // QUAN TRỌNG: Xóa Cache sau khi dữ liệu thay đổi
                _cache.Remove(ServiceLevelCacheKey);
                _logger.LogInformation("Đã xóa Cache {Key} do có dữ liệu mới.", ServiceLevelCacheKey);

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý mức độ dịch vụ",
                   "Thêm mới mức độ dịch vụ: " + model.TenDichVu,                 
                   "Thêm mới",
                    model.MaDichVu.ToString(),
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>
                    {
                        {"Tên dịch vụ ", model.TenDichVu },
                        { "Hệ số ", model.HeSoNhiPhan },
                        { "Trạng thái ", model.TrangThai }
                    }
               );
                ClearPriceRegionCache();
                return Ok(new { message = "Thêm mới thành công!", data = entity });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm mới mức độ dịch vụ.");
                return StatusCode(500, "Lỗi hệ thống khi lưu dữ liệu.");
            }
        }
        // Thêm các Method này vào trong class QuanLyMucDoDichVu

        [Authorize]
        [HttpPut("chinhsua")]
        public async Task<IActionResult> ChinhSua([FromBody] MucDoDichVuModels model)
        {
            if (model == null || model.MaDichVu <= 0) return BadRequest("Dữ liệu không hợp lệ.");

            try
            {
                var existingService = await _context.MucDoDichVus.FirstOrDefaultAsync(m => m.MaDichVu == model.MaDichVu);

                if (existingService == null) return NotFound("Không tìm thấy mức dịch vụ.");

                // CHẶN CHỈNH SỬA: Nếu mức dịch vụ đã bị vô hiệu hóa (TrangThai = false)
                if (existingService.TrangThai == false)
                {
                    return BadRequest("Không thể chỉnh sửa mức dịch vụ đã bị vô hiệu hóa hoặc đã hết hạn.");
                }

                bool isCoreDataChanged = existingService.ThoiGianCamKet != model.ThoiGianCamKet ||
                                         existingService.HeSoNhiPhan != model.HeSoNhiPhan;
                // Lưu lại dữ liệu cũ để ghi log trước khi thay đổi
                var duLieuCu = new Dictionary<string, object>
                {
                    { "Tên dịch vụ", existingService.TenDichVu },
                    { "Thời gian cam kết", existingService.ThoiGianCamKet },
                    { "Hệ số nhi phân", existingService.HeSoNhiPhan },
                    { "Trạng thái", existingService.TrangThai }
                };
                if (isCoreDataChanged)
                {
                    existingService.TrangThai = false;
                    existingService.NgayKetThuc = DateTime.Now;

                    var newServiceVersion = new MucDoDichVu
                    {
                        TenDichVu = model.TenDichVu,
                        ThoiGianCamKet = model.ThoiGianCamKet,
                        HeSoNhiPhan = model.HeSoNhiPhan,
                        LaCaoCap = model.LaCaoCap,
                        TrangThai = true,
                        NgayBatDau = DateTime.Now,
                        MaBangCu = existingService.MaDichVu
                    };
                    _context.MucDoDichVus.Add(newServiceVersion);
                }
                else
                {
                    existingService.TenDichVu = model.TenDichVu;
                    existingService.LaCaoCap = model.LaCaoCap;
                    existingService.TrangThai = model.TrangThai;
                }
                var dulieumoi = new Dictionary<string, object>
                {
                    { "Tên dịch vụ", model.TenDichVu },
                   
                    { "Hệ số nhi phân", model.HeSoNhiPhan }
                    
                };
                await _context.SaveChangesAsync();
                _cache.Remove(ServiceLevelCacheKey);

                // Ghi log chi tiết và Reset Cache hệ thống (Redis)
                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý mức độ dịch vụ",
                    "Chỉnh sửa mức độ dịch vụ", // Hành động
                                   // Bảng tác động
                    $"Cập nhật dịch vụ ID: {model.MaDichVu}", // Mô tả
                    "1",              // ID bản ghi
                    duLieuCu,
                    dulieumoi

                );
                ClearPriceRegionCache();
                return Ok(new { success = true, message = "Cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chỉnh sửa.");
                return StatusCode(500, "Lỗi hệ thống.");
            }
        }

        [Authorize]
        [HttpPut("vohieuhoa/{id}")]
        public async Task<IActionResult> VoHieuHoa(int id)
        {
            try
            {
                var service = await _context.MucDoDichVus.FindAsync(id);
                if (service == null) return NotFound("Không tìm thấy dịch vụ.");

                var datacu = new Dictionary<string, object>
                {
                    { "Tên dịch vụ", service.TenDichVu },
                    
                    { "Hệ số nhi phân", service.HeSoNhiPhan },
                    
                };

                service.TrangThai = false;
                service.NgayKetThuc = DateTime.Now;

                await _context.SaveChangesAsync();
                _cache.Remove(ServiceLevelCacheKey);

                await _sys.GhiLogVaResetCacheAsync(
                    "Quản lý mức độ dịch vụ",
                    "Vô hiệu hóa mức độ dịch vụ: " + service.TenDichVu,
                    "Quản lý mức độ dịch vụ",
                    id.ToString(),
                    datacu,
                    new Dictionary<string, object>
                    {
                        { "Trạng thái", "Ngừng hoạt động" }                    
                    }
                );

                return Ok(new { success = true, message = "Đã vô hiệu hóa mức dịch vụ." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi vô hiệu hóa.");
                return StatusCode(500, "Lỗi hệ thống.");
            }
        }

        [HttpGet("lichsu/{maDichVu}")]
        public async Task<IActionResult> XemLichSu(int maDichVu)
        {
            try
            {
                // 1. Tìm bản ghi đang được chọn
                var current = await _context.MucDoDichVus.AsNoTracking().FirstOrDefaultAsync(m => m.MaDichVu == maDichVu);
                if (current == null) return NotFound("Không tìm thấy dịch vụ.");

                // 2. Tìm ID gốc (Root ID) bằng cách truy ngược MaBangCu lên trên cùng
                int rootId = maDichVu;
                var temp = current;
                while (temp?.MaBangCu != null)
                {
                    rootId = temp.MaBangCu.Value;
                    temp = await _context.MucDoDichVus.AsNoTracking().FirstOrDefaultAsync(m => m.MaDichVu == rootId);
                }

                // 3. Lấy toàn bộ danh sách có liên quan đến ID gốc này
                // Bao gồm: Bản ghi gốc và tất cả các bản ghi có chuỗi liên kết dẫn tới nó
                // Để đơn giản và hiệu quả, ta lấy bản ghi gốc và các bản ghi có cùng TenDichVu hoặc liên kết MaBangCu
                var history = await _context.MucDoDichVus
                    .AsNoTracking()
                    .Where(m => m.MaDichVu == rootId || m.MaBangCu == rootId ||
                                _context.MucDoDichVus.Any(p => p.MaDichVu == m.MaBangCu && p.MaDichVu == rootId))
                    .OrderByDescending(m => m.NgayBatDau)
                    .ToListAsync();

                // Cách tiếp cận tối ưu hơn nếu chuỗi lịch sử dài (Dùng đệ quy trong bộ nhớ hoặc Common Table Expression nếu dùng SQL trực tiếp)
                // Nhưng với cấu hình dịch vụ, chuỗi thay đổi thường ngắn (< 20 bản ghi), nên logic trên là đủ dùng.

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy lịch sử.");
                return StatusCode(500, "Lỗi hệ thống.");
            }
        }


        public static class TinhKhoangCach
        {
            public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
            {
                var r = 6371; // Bán kính Trái Đất (km)
                var dLat = (lat2 - lat1) * (Math.PI / 180);
                var dLon = (lon2 - lon1) * (Math.PI / 180);
                var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                        Math.Cos(lat1 * (Math.PI / 180)) * Math.Cos(lat2 * (Math.PI / 180)) *
                        Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                return r * c;
            }
        }
        private async Task<decimal> TinhGiaLogicNoiBo(string thanhPhoLay, string thanhPhoGiao, double? khoiLuong, double? theTich, double soKm, int maLoaiHang)
        {
            // 1. Tính trọng lượng quy đổi
            double trongLuongTheTich = (theTich ?? 0) / 6000.0;
            decimal trongLuongDeTinh = (decimal)Math.Max((double)(khoiLuong ?? 0), trongLuongTheTich);

            // 2. Tìm bảng giá phù hợp (Ưu tiên MaLoaiHang và Tuyến đường)
            var bg = await _context.BangGiaVungs
                .AsNoTracking()
                .Where(b => b.IsActive == true &&
                            b.KhuVucLay == thanhPhoLay &&
                            b.KhuVucGiao == thanhPhoGiao &&
                            b.MaLoaiHang == maLoaiHang &&
                            trongLuongDeTinh >= (b.TrongLuongToiThieuKg ?? 0) &&
                            (b.TrongLuongToiDaKg == null || trongLuongDeTinh <= b.TrongLuongToiDaKg))
                .FirstOrDefaultAsync();

            if (bg == null) return 0; // Hoặc ném Exception nếu không tìm thấy giá

            // 3. Tính toán theo loại hình (Bưu kiện hoặc Vận tải chuyến)
            if (bg.LoaiTinhGia == 2) // VẬN TẢI CHUYẾN (Theo Km)
            {
                decimal kmTinhPhi = Math.Max((decimal)soKm, (decimal)(bg.KmToiThieu ?? 0));
                return (kmTinhPhi * (bg.DonGiaKm ?? 0)) + (bg.PhiDungDiem ?? 0);
            }
            else // BƯU KIỆN (Mặc định)
            {
                decimal giaCoBan = bg.DonGiaCoBan ?? 0;
                decimal khoiLuongVuot = Math.Max(0, trongLuongDeTinh - (decimal)(bg.TrongLuongToiThieuKg ?? 0));
                return giaCoBan + (khoiLuongVuot * (bg.PhuPhiMoiKg ?? 0));
            }
        }
        /// <summary>
        /// Tìm sân bay gần nhất từ Database dựa trên tọa độ
        /// </summary>
        private async Task<(string Iata, double Lat, double Lon)> GetNearestAirportFromDb(double userLat, double userLon)
        {
            // Lấy danh sách sân bay đang hoạt động kèm tọa độ từ bảng Dia_Chi
            var airports = await _context.SanBays
                .Where(s => s.TrangThai == true)
                .Select(s => new {
                    s.IataCode,
                    Lat = (double)s.MaDiaChiNavigation.ViDo,
                    Lon = (double)s.MaDiaChiNavigation.KinhDo
                })
                .ToListAsync();

            if (!airports.Any()) return (null, 0, 0);

            // Tính toán khoảng cách và tìm cái gần nhất
            var nearest = airports
                .Select(s => new {
                    s.IataCode,
                    s.Lat,
                    s.Lon,
                    Distance = TinhKhoangCach.CalculateDistance(userLat, userLon, s.Lat, s.Lon)
                })
                .OrderBy(s => s.Distance)
                .FirstOrDefault();

            return (nearest.IataCode, nearest.Lat, nearest.Lon);
        }

        [HttpPost("phantich-vanchuyen")]
        public async Task<IActionResult> PhanTichVanchuyen([FromBody] YeuCauPhanTich request)
        {
            if (request == null) return BadRequest("Dữ liệu không được trống.");

            try
            {
                // 1. Tìm thông tin sân bay (Giả sử bạn đã có hàm GetNearestAirportFromDb lấy từ DB cục bộ)
                var originInfo = await GetNearestAirportFromDb(request.LatGui, request.LonGui);
                var destInfo = await GetNearestAirportFromDb(request.LatNhan, request.LonNhan);

                if (originInfo.Iata == null || destInfo.Iata == null)
                    return BadRequest("Không tìm thấy dữ liệu sân bay trong hệ thống.");

                // 2. Tính t1, t2 (Đường bộ)
                double kmT1 = TinhKhoangCach.CalculateDistance(request.LatGui, request.LonGui, originInfo.Lat, originInfo.Lon);
                double kmT2 = TinhKhoangCach.CalculateDistance(destInfo.Lat, destInfo.Lon, request.LatNhan, request.LonNhan);
                double t1 = kmT1 / 40.0;
                double t2 = kmT2 / 40.0;

                // 3. Các mốc cố định theo đặc tả
                double t3 = 2.0; // Thủ tục sân bay gửi
                double t5 = 1.0; // Thủ tục sân bay nhận
                double ruiRo = 1.0;

                // 4. Tìm chuyến bay và tính t4, t6
                DateTime gioHienTai = DateTime.Now;
                DateTime gioToiThieuCoTheBay = gioHienTai.AddHours(t1 + t3);

                var flight = await _flightService.GetEarliestFlight(originInfo.Iata, destInfo.Iata, gioToiThieuCoTheBay);

                if (flight.DepartureTime == null)
                    return NotFound("Không tìm thấy chuyến bay phù hợp trong ngày.");

                // t4: Thời gian bay thực tế từ API
                double t4 = (flight.ArrivalTime.Value - flight.DepartureTime.Value).TotalHours;

                // t6: Thời gian chờ từ lúc tiếp nhận đơn đến khi có chuyến (sau khi trừ t1, t3)
                double t6 = (flight.DepartureTime.Value - gioToiThieuCoTheBay).TotalHours;
                t6 = t6 < 0 ? 0 : t6;

                // 5. Tổng hợp kết quả
              
                double tongThoiGianT = t1 + t2 + t3 + t4 + t5 + t6 + ruiRo;

                var result = new FlightServiceCalculationResult
                {
                    SanBayGui = originInfo.Iata,
                    SanBayNhan = destInfo.Iata,
                    MaChuyenBay = flight.FlightNo ?? "N/A",
                    GioKhoiHanh = flight.DepartureTime,
                    ThoiGiaThoiGianDuongBo_t1_t2 = Math.Round(t1 + t2, 2),
                    ThoiGianThuTuc_t3_t5 = t3 + t5,
                    ThoiGianBay_t4 = t4,
                    ThoiGianChoChuyenBay_t6 = Math.Round(t6, 2),
                    ThoiGianRuiRo = ruiRo,
                    TongThoiGianDuKien = tongThoiGianT,
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        [HttpGet("get-by-id/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var dv = await _context.MucDoDichVus
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.MaDichVu == id);

            if (dv == null) return NotFound(new { message = "Mức độ dịch vụ không tồn tại." });

            return Ok(new
            {
                dv.MaDichVu,
                dv.TenDichVu,
                dv.HeSoNhiPhan,
                dv.ThoiGianCamKet,
                dv.LaCaoCap
            });
        }
        [HttpPost("tinh-gia-nhanh")]
        public async Task<IActionResult> TinhGiaNhanh([FromBody] YeuCauTinhGiaNhanh request)
        {
            try
            {
                // 1. Tính tổng giá gốc của các kiện hàng từ bảng giá vùng
                decimal tongGiaGoc = 0;
                foreach (var kien in request.DanhSachKien)
                {
                    tongGiaGoc += await TinhGiaLogicNoiBo(
                        request.ThanhPhoLay,
                        request.ThanhPhoGiao,
                        kien.KhoiLuong,
                        kien.TheTich,
                        0, // Giả định km mặc định hoặc lấy từ Google Maps API
                        kien.MaLoaiHang
                    );
                }

                if (tongGiaGoc == 0) return BadRequest("Không tìm thấy bảng giá vùng phù hợp.");

                // 2. Lấy danh sách các mức độ dịch vụ đang hoạt động
                var listMucDo = await _context.MucDoDichVus
                    .Where(m => m.TrangThai == true)
                    .ToListAsync();

                // 3. Phân tích thời gian bay (nếu có tọa độ sân bay)
                // Lưu ý: Ở đây bạn có thể gọi nội bộ hàm PhanTichVanchuyen hoặc mock thời gian

                var ketQuaDichVu = listMucDo.Select(m => new {
                    MaMucDo = m.MaDichVu,
                    TenDichVu = m.TenDichVu,
                    GiaDuKien = Math.Round(tongGiaGoc * (decimal)m.HeSoNhiPhan, 0),
                    ThoiGianDuKien = m.ThoiGianCamKet, // Hoặc cộng thêm thời gian bay t4, t6 nếu là Hỏa Tốc
                    LaCaoCap = m.LaCaoCap
                }).OrderBy(x => x.GiaDuKien);

                return Ok(ketQuaDichVu);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi tính toán: {ex.Message}");
            }
        }
        [HttpPost("phantich-noithanh")]
        public async Task<IActionResult> PhanTichNoiThanh([FromBody] YeuCauPhanTich request)
        {
            if (request == null) return BadRequest("Dữ liệu không được trống.");

            try
            {
                // 1. Tính khoảng cách thực tế giữa điểm gửi và điểm nhận
                double distanceKm = TinhKhoangCach.CalculateDistance(
                    request.LatGui, request.LonGui,
                    request.LatNhan, request.LonNhan
                );

                // 2. Định nghĩa các tham số vận hành nội thành
                // Vận tốc trung bình trong thành phố (thường từ 20-30km/h do tắc đường/đèn đỏ)
                double vanTocNoiThanh = 25.0;

                // Thời gian lấy hàng và đóng gói (giờ)
                double tLayHang = 0.5;

                // Thời gian di chuyển thực tế
                double tDiChuyen = distanceKm / vanTocNoiThanh;

                // Thời gian xử lý trung chuyển/phân loại tại bưu cục nội thành
                double tXuLyKho = 1.0;

                // Hệ số rủi ro (giờ cao điểm, thời tiết)
                double ruiRoNoiThanh = 0.5;

                // 3. Tính tổng thời gian
                double tongThoiGian = tLayHang + tDiChuyen + tXuLyKho + ruiRoNoiThanh;

                // 4. Trả về kết quả
                var result = new
                {
                    LoaiVanChuyen = "Nội thành",
                    KhoangCachKm = Math.Round(distanceKm, 2),
                    ChiTietThoiGian = new
                    {
                        ThoiGianLayHang = tLayHang,
                        ThoiGianDiChuyen = Math.Round(tDiChuyen, 2),
                        ThoiGianXuLyTaiKho = tXuLyKho,
                        ThoiGianDuPhong = ruiRoNoiThanh
                    },
                    TongThoiGianDuKienGio = Math.Round(tongThoiGian, 2),
                    DonVi = "Giờ",
                    ThoiDiemHoanThanhDuKien = DateTime.Now.AddHours(tongThoiGian)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi phân tích vận chuyển nội thành.");
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }
        // Model hỗ trợ cho API trên
        public class YeuCauTinhGiaNhanh
        {
            public string? ThanhPhoLay { get; set; }
            public string? ThanhPhoGiao { get; set; }
            public List<KienHangSorce>? DanhSachKien { get; set; }
        }

        public class KienHangSorce
        {
            public double KhoiLuong { get; set; }
            public double TheTich { get; set; }
            public int MaLoaiHang { get; set; }
        }
    }
}  