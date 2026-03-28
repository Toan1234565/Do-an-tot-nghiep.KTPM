using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1;
using QuanLyKhachHang.Models1.CauHinhTichDiem;
using Tmdt.Shared.Services;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace QuanLyKhachHang.ControllersAPI
{
    [Route("api/cauhinhtichdiem")]
    [ApiController]
    public class CauHinhTichDiem : ControllerBase
    {
        private readonly ILogger<CauHinhTichDiem> _logger;
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private const int PageSize = 10; // Đưa vào hằng số
        private const string PointConfigCacheKey = "GlobalPointConfig";
        private readonly ISystemService _sys;
        private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();

        public CauHinhTichDiem(ILogger<CauHinhTichDiem> logger, TmdtContext context, IMemoryCache cache, ISystemService sys)
        {
            _logger = logger;
            _context = context;
            _cache = cache;
            _sys = sys;
        }
        [HttpGet("lay-cau-hinh")]
        public async Task<IActionResult> LayCauHinh()
        {
            try
            {
                if (!_cache.TryGetValue(PointConfigCacheKey, out CauHinhTichDiemModels? result))
                {
                    // Truy vấn DB
                    var query = await _context.CauHinhTichDiems.AsNoTracking()
                        .Select(kh => new CauHinhTichDiemModels
                        {
                            TyLeTichDiem = kh.TyLeTichDiem,
                            GiaTriDiem = kh.GiaTriDiem,
                            DiemToiThieuDeDung = kh.DiemToiThieuDeDung,
                            ChoPhepDungDiem = kh.ChoPhepDungDiem,
                            NgayCapNhat = kh.NgayCapNhat
                        })
                        .FirstOrDefaultAsync();

                    if (query == null)
                    {
                        return NotFound(new { success = false, message = "Chưa có dữ liệu cấu hình tích điểm." });
                    }

                    // Lưu vào cache
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                    _cache.Set(PointConfigCacheKey, query, cacheOptions);
                    result = query;
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy cấu hình tích điểm.");
                return StatusCode(500, "Lỗi hệ thống khi lấy cấu hình tích điểm.");
            }
        }

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
        [HttpPut("cap-nhat")]
        public async Task<IActionResult> CapNhat([FromBody] CapNhatCauHinhRequest request)
        {
            // 1. Kiểm tra tính hợp lệ của dữ liệu đầu vào
            if (request.TyLeTichDiem < 0 || request.GiaTriDiem < 0)
            {
                return BadRequest("Tỷ lệ và giá trị điểm không được nhỏ hơn 0.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 2. Lấy bản ghi cấu hình hiện có (thường chỉ có 1 dòng duy nhất trong bảng này)
                var cauHinh = await _context.CauHinhTichDiems.FirstOrDefaultAsync();

                var datacu = new Dictionary<string, object?>
                {
                    { "Tỷ lệ tích điểm", cauHinh?.TyLeTichDiem },
                    { "Giá trị giảm", cauHinh?.GiaTriDiem },
                    { "Điểm tối thiểu dùng", cauHinh?.DiemToiThieuDeDung },
                    { "Cho phép dùng điểm", cauHinh?.ChoPhepDungDiem }
                };

                if (cauHinh == null)
                {
                    // Nếu chưa có thì tạo mới (Trường hợp khởi tạo hệ thống)
                    cauHinh = new Models.CauHinhTichDiem
                    {
                        TyLeTichDiem = request.TyLeTichDiem,
                        GiaTriDiem = request.GiaTriDiem,
                        DiemToiThieuDeDung = request.DiemToiThieuDeDung,
                        ChoPhepDungDiem = request.ChoPhepDungDiem,
                        NgayCapNhat = DateTime.Now
                    };
                    _context.CauHinhTichDiems.Add(cauHinh);
                }
                else
                {
                    // 3. Cập nhật các giá trị mới
                    cauHinh.TyLeTichDiem = request.TyLeTichDiem;
                    cauHinh.GiaTriDiem = request.GiaTriDiem;
                    cauHinh.DiemToiThieuDeDung = request.DiemToiThieuDeDung;
                    cauHinh.ChoPhepDungDiem = request.ChoPhepDungDiem;
                    cauHinh.NgayCapNhat = DateTime.Now;

                    _context.CauHinhTichDiems.Update(cauHinh);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var datamoi = new Dictionary<string, object?>
                {
                    { "Tỷ lệ tích điểm", cauHinh.TyLeTichDiem },
                    { "Giá trị giảm", cauHinh.GiaTriDiem },
                    { "Điểm tối thiểu dùng", cauHinh.DiemToiThieuDeDung },
                    { "Cho phép dùng điểm", cauHinh.ChoPhepDungDiem }
                };
                // 2. Lọc ra chỉ những trường bị thay đổi
                var (diffCu, diffMoi) = LocThayDoi.GetChanges(datacu, datamoi);

                // 3. Nếu có thay đổi thì mới ghi log (hoặc ghi log với dữ liệu đã lọc)
                if (diffMoi.Count > 0)
                {
                   

                    await _sys.GhiLogVaResetCacheAsync(
                        "Quản lý khuyến mãi",
                        "Cập nhật cấu hình điểm thưởng",
                        "Cấu hình điểm",
                        "",
                        diffCu,  // Chỉ chứa các giá trị cũ của trường bị sửa
                        diffMoi  // Chỉ chứa các giá trị mới của trường bị sửa
                    );
                }

                ClearPriceRegionCache();

                _logger.LogInformation("Đã cập nhật cấu hình tích điểm thành công và làm mới Cache.");

                return Ok(new
                {
                    Success = true,
                    Message = "Cập nhật cấu hình thành công.",
                    Data = cauHinh
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi cập nhật cấu hình tích điểm.");
                return StatusCode(500, "Lỗi hệ thống khi lưu cấu hình.");
            }
        }

        [HttpGet("so-du/{searchTerm}")]
        public async Task<IActionResult> GetSoDu(string searchTerm)
        {
            try
            {
                // Kiểm tra nếu searchTerm là số thì ưu tiên tìm theo Mã, nếu không thì tìm theo Tên/SĐT
                bool isNumeric = int.TryParse(searchTerm, out int maKH);

                var query = _context.DiemThuongs
                    .Include(d => d.MaKhachHangNavigation)
                    .AsNoTracking();

                // Thực hiện lọc dựa trên searchTerm
                var diem = await query
                    .Where(d => (isNumeric && d.MaKhachHang == maKH) ||
                                d.MaKhachHangNavigation.SoDienThoai.Contains(searchTerm) ||
                                d.MaKhachHangNavigation.TenLienHe.Contains(searchTerm) ||
                                d.MaKhachHangNavigation.TenCongTy.Contains(searchTerm))
                    .Select(d => new DiemThuongModels
                    {
                        MaKhachHang = d.MaKhachHang,
                        // Ưu tiên hiển thị Tên Công Ty và Tên Liên Hệ để dễ nhận diện
                        TenKhachHang = $"{d.MaKhachHangNavigation.TenCongTy} ({d.MaKhachHangNavigation.TenLienHe})",
                        TongDiemTichLuy = d.TongDiemTichLuy ?? 0,
                        DiemDaDung = d.DiemDaDung ?? 0,
                        SoDuHienTai = (d.TongDiemTichLuy ?? 0) - (d.DiemDaDung ?? 0),
                        NgayCapNhatCuoi = d.NgayCapNhatCuoi
                    })
                    .FirstOrDefaultAsync();

                if (diem == null)
                {
                    return Ok(new { success = false, message = "Không tìm thấy khách hàng hoặc khách hàng chưa có điểm." });
                }

                return Ok(new { success = true, data = diem });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tra cứu điểm với từ khóa: {term}", searchTerm);
                return StatusCode(500, "Lỗi hệ thống.");
            }
        }

        [HttpGet("lich-su-diem/{searchTerm}")]
        public async Task<IActionResult> GetLichSuDiem(string searchTerm)
        {
            try
            {
                // 1. Kiểm tra xem searchTerm là Mã (số) hay Tên/SĐT (chuỗi)
                bool isNumeric = int.TryParse(searchTerm, out int maKH);

                // 2. Tìm danh sách điểm thưởng dựa trên điều kiện linh hoạt
                // Lưu ý: Nếu bạn có bảng Nhật ký giao dịch riêng, hãy join vào bảng đó.
                // Ở đây đang lấy từ bảng DiemThuong theo logic của bạn.
                var query = _context.DiemThuongs
                    .Include(d => d.MaKhachHangNavigation)
                    .AsNoTracking()
                    .Where(d => (isNumeric && d.MaKhachHang == maKH) ||
                                d.MaKhachHangNavigation.SoDienThoai.Contains(searchTerm) ||
                                d.MaKhachHangNavigation.TenLienHe.Contains(searchTerm) ||
                                d.MaKhachHangNavigation.TenCongTy.Contains(searchTerm));

                var lichSu = await query
                    .Select(d => new DiemThuongModels
                    {
                        TenKhachHang =  d.MaKhachHangNavigation.TenLienHe,
                        TongDiemTichLuy = d.TongDiemTichLuy ?? 0,
                        DiemDaDung = d.DiemDaDung ?? 0,
                        SoDuHienTai = (d.TongDiemTichLuy ?? 0) - (d.DiemDaDung ?? 0),
                        NgayCapNhatCuoi = d.NgayCapNhatCuoi
                    })
                    .ToListAsync();

                // 3. Trả về kết quả
                if (lichSu == null || !lichSu.Any())
                {
                    return Ok(new { success = true, data = new List<DiemThuongModels>(), message = "Không tìm thấy lịch sử giao dịch." });
                }

                return Ok(new { success = true, data = lichSu });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy lịch sử điểm với từ khóa: {term}", searchTerm);
                return StatusCode(500, "Lỗi hệ thống khi truy xuất lịch sử.");
            }
        }

        [HttpGet("thong-ke-tong-quan")]
        public async Task<IActionResult> GetThongKeTongQuan()
        {
            try
            {
                // 1. Tổng số khách hàng đã tham gia tích điểm
                var tongKhachHang = await _context.DiemThuongs.CountAsync();

                // 2. Tổng số điểm đã phát ra và tổng số điểm đã sử dụng
                var stats = await _context.DiemThuongs
                    .GroupBy(x => 1)
                    .Select(g => new
                    {
                        TongDiemPhatHanh = g.Sum(x => x.TongDiemTichLuy ?? 0),
                        TongDiemDaDung = g.Sum(x => x.DiemDaDung ?? 0)
                    })
                    .FirstOrDefaultAsync();

                // 3. Cấu hình hiện tại
                var cauHinh = await _context.CauHinhTichDiems.AsNoTracking().FirstOrDefaultAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        TongKhachHangThamGia = tongKhachHang,
                        TongDiemHeThong = stats?.TongDiemPhatHanh ?? 0,
                        TongDiemDaSuDung = stats?.TongDiemDaDung ?? 0,
                        DiemConTonTrongHeThong = (stats?.TongDiemPhatHanh ?? 0) - (stats?.TongDiemDaDung ?? 0),
                        CauHinhHienTai = cauHinh
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy thống kê tổng quan tích điểm.");
                return StatusCode(500, "Lỗi hệ thống.");
            }
        }

        [HttpPost("doi-diem-thuong")]
        public async Task<IActionResult> DoiDiemThuong([FromBody] DoiDiemRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Lấy cấu hình tích điểm mới nhất
                var cauHinh = await _context.CauHinhTichDiems.AsNoTracking().FirstOrDefaultAsync();
                if (cauHinh == null || !cauHinh.ChoPhepDungDiem)
                    return BadRequest("Chức năng dùng điểm hiện đang tạm khóa.");

                // 2. Lấy thông tin điểm của khách hàng
                var diemThuong = await _context.DiemThuongs
                    .FirstOrDefaultAsync(d => d.MaKhachHang == request.MaKhachHang);

                if (diemThuong == null || (diemThuong.TongDiemTichLuy - (diemThuong.DiemDaDung ?? 0)) < request.SoDiemMuonDung)
                    return BadRequest("Số dư điểm không đủ để thực hiện giao dịch.");

                // 3. Kiểm tra hạn mức tối thiểu để được sử dụng
                int diemHienCo = (diemThuong.TongDiemTichLuy ?? 0) - (diemThuong.DiemDaDung ?? 0);
                if (diemHienCo < cauHinh.DiemToiThieuDeDung)
                    return BadRequest($"Bạn cần tối thiểu {cauHinh.DiemToiThieuDeDung} điểm để bắt đầu đổi.");

                if (request.SoDiemMuonDung < cauHinh.DiemToiThieuDeDung)
                    return BadRequest($"Mỗi lần đổi phải dùng ít nhất {cauHinh.DiemToiThieuDeDung} điểm.");

                // 4. Tính toán số tiền quy đổi
                // Công thức: Số tiền giảm = Số điểm * Giá trị 1 điểm
                decimal soTienGiam = request.SoDiemMuonDung * cauHinh.GiaTriDiem;

                // Kiểm tra không cho giảm quá tổng tiền đơn hàng
                if (soTienGiam > request.TongTienDonHang)
                {
                    soTienGiam = request.TongTienDonHang;
                    // Tính lại số điểm thực tế cần dùng để giảm hết đơn hàng (làm tròn lên)
                    request.SoDiemMuonDung = (int)Math.Ceiling(soTienGiam / cauHinh.GiaTriDiem);
                }

                // 5. Cập nhật vào DB bảng DiemThuong
                diemThuong.DiemDaDung = (diemThuong.DiemDaDung ?? 0) + request.SoDiemMuonDung;
                diemThuong.NgayCapNhatCuoi = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    Success = true,
                    SoDiemDaDung = request.SoDiemMuonDung,
                    SoTienDuocGiam = soTienGiam,
                    SoDiemConLai = (diemThuong.TongDiemTichLuy ?? 0) - diemThuong.DiemDaDung
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi đổi điểm cho khách hàng {MaKH}", request.MaKhachHang);
                return StatusCode(500, "Lỗi hệ thống khi xử lý đổi điểm.");
            }
        }
        [HttpPost("tich-diem-don-hang")]
        public async Task<IActionResult> TichDiemDonHang([FromBody] TichDiemRequest request)
        {
            // Sử dụng transaction để đảm bảo tính toàn vẹn dữ liệu
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Lấy cấu hình tích điểm hiện tại
                var cauHinh = await _context.CauHinhTichDiems.AsNoTracking().FirstOrDefaultAsync();
                if (cauHinh == null)
                    return BadRequest("Chưa thiết lập cấu hình tích điểm.");

                // 2. Tính số điểm được cộng
                // Công thức: Số điểm = (Số tiền thanh toán * Tỷ lệ tích điểm)
                // Ví dụ: Thanh toán 100,000đ, Tỷ lệ 0.01 (1%) => Được 1,000 điểm
                int diemDuocCong = (int)Math.Floor(request.SoTienThanhToan * (5/100));

                if (diemDuocCong <= 0)
                    return Ok(new { Message = "Số tiền không đủ hạn mức tích điểm.", DiemCong = 0 });

                // 3. Cập nhật hoặc Tạo mới bản ghi điểm thưởng cho khách hàng
                var diemThuong = await _context.DiemThuongs
                    .FirstOrDefaultAsync(d => d.MaKhachHang == request.MaKhachHang);

                if (diemThuong == null)
                {
                    // Nếu khách hàng chưa bao giờ có điểm, tạo bản ghi mới
                    diemThuong = new DiemThuong
                    {
                        MaKhachHang = request.MaKhachHang,
                        TongDiemTichLuy = diemDuocCong,
                        DiemDaDung = 0,
                        NgayCapNhatCuoi = DateTime.Now
                    };
                    _context.DiemThuongs.Add(diemThuong);
                }
                else
                {
                    // Nếu đã có, cộng dồn vào tổng tích lũy
                    diemThuong.TongDiemTichLuy = (diemThuong.TongDiemTichLuy ?? 0) + diemDuocCong;
                    diemThuong.NgayCapNhatCuoi = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Đã tích {Diem} điểm cho khách hàng {MaKH} từ đơn hàng {MaDH}",
                    diemDuocCong, request.MaKhachHang, request.MaDonHang);

                return Ok(new
                {
                    Success = true,
                    DiemDuocCong = diemDuocCong,
                    TongDiemHienTai = (diemThuong.TongDiemTichLuy ?? 0) - (diemThuong.DiemDaDung ?? 0)
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi tích điểm cho khách hàng {MaKH}", request.MaKhachHang);
                return StatusCode(500, "Lỗi hệ thống khi xử lý tích điểm.");
            }
        }

       

    }
}
