using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1.CauHinhTichDiem;

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

        public CauHinhTichDiem(ILogger<CauHinhTichDiem> logger, TmdtContext context, IMemoryCache cache)
        {
            _logger = logger;
            _context = context;
            _cache = cache;
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
