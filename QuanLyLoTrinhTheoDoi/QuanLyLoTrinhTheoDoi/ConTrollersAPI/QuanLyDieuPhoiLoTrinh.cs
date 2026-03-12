using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuanLyLoTrinhTheoDoi.Models;
using QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh;
using System.Net.Http;

namespace QuanLyLoTrinhTheoDoi.ConTrollersAPI
{
    [Route("api/dieuphoilotrinh")]
    [ApiController]
    public class QuanLyDieuPhoiLoTrinh : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyDieuPhoiLoTrinh> _logger;
        private readonly IServiceProvider _serviceProvider;

        public QuanLyDieuPhoiLoTrinh(TmdtContext context, IMemoryCache cache, IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider,
            ILogger<QuanLyDieuPhoiLoTrinh> logger )
        {
            _context = context;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        // Tài xế xem danh sách việc cần làm
        [HttpGet("tai-xe/{maTaiXe}")]
        public async Task<IActionResult> GetLoTrinhTaiXe(int maTaiXe)
        {
            return Ok(await _context.LoTrinhs
                .Include(l => l.DiemDungs)
                .Where(l => l.MaTaiXeChinh == maTaiXe && l.TrangThai != "Hoàn thành")
                .ToListAsync());
        }

        // Admin xem tổng quát bản đồ
        [HttpGet("tat-ca-lo-trinh")]
        public async Task<IActionResult> GetAllLoTrinh()
        {
            return Ok(await _context.LoTrinhs.AsNoTracking().ToListAsync());
        }

        // Cập nhật vị trí GPS từ App Tài xế
        [HttpPost("cap-nhat-toa-do")]
        public async Task<IActionResult> PostNhatKy([FromBody] NhatKyTheoDoi log)
        {
            log.ThoiGian = DateTime.Now;
            _context.NhatKyTheoDois.Add(log);

            // Lưu cache 5 phút để Dashboard lấy nhanh
            _cache.Set($"LastPos_TX_{log.MaTaiXe}", log, TimeSpan.FromMinutes(5));

            await _context.SaveChangesAsync();
            return Ok();
        }

        // Xác nhận đã đến điểm (Kho/Khách)
        [HttpPut("den-diem-dung/{maDiemDung}")]
        public async Task<IActionResult> DenDiemDung(int maDiemDung)
        {
            var diem = await _context.DiemDungs.FindAsync(maDiemDung);
            if (diem == null) return NotFound();

            diem.ThoiGianDenThucTe = DateTime.Now;

            // Kiểm tra nếu là điểm cuối thì hoàn thành lộ trình
            var isLast = !await _context.DiemDungs.AnyAsync(d => d.MaLoTrinh == diem.MaLoTrinh && d.ThuTuDung > diem.ThuTuDung);
            if (isLast)
            {
                var lt = await _context.LoTrinhs.FindAsync(diem.MaLoTrinh);
                if (lt != null) lt.TrangThai = "Hoàn thành";
            }

            await _context.SaveChangesAsync();
            return Ok(new { status = "Success" });
        }

        [HttpPost("tu-dong-gom-nhom")]
        public async Task<IActionResult> TuDongGomNhomDonHang()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TmdtContext>();

            var clientDH = _httpClientFactory.CreateClient("DonHangApi");
            var clientKho = _httpClientFactory.CreateClient("KhoApi");
            var clientPT = _httpClientFactory.CreateClient("PhuongTienApi");
            var clientNS = _httpClientFactory.CreateClient("NhanSuApi");

            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. Lấy danh sách đơn hàng chờ điều phối
                var resDonHang = await clientDH.PostAsJsonAsync("api/quanlydonhang/cho-dieu-phoi", new { });
                if (!resDonHang.IsSuccessStatusCode) return StatusCode((int)resDonHang.StatusCode, "Lỗi kết nối Server Đơn hàng");

                var responseData = await resDonHang.Content.ReadFromJsonAsync<DonHangResponse>();
                if (responseData?.Clusters == null || !responseData.Clusters.Any())
                    return NotFound(new { Message = "Không có đơn hàng nào cần điều phối." });

                var finalProcessedData = new List<object>();

                foreach (var c in responseData.Clusters)
                {
                    // 2. Tìm kho gần nhất
                    var resKho = await clientKho.GetAsync($"api/quanlykhobai/tim-kho-gan-nhat/{c.MaDiaChiCum}");
                    if (!resKho.IsSuccessStatusCode) continue;
                    var kho = await resKho.Content.ReadFromJsonAsync<KhoGanNhatResponse>();
                    if (kho == null) continue;

                    // 3. Tìm Xe và Tài xế
                    var taskXe = clientPT.GetFromJsonAsync<List<VehicleFreeDto>>($"api/quanlyxe/xe-san-sang-dieu-phoi?maKho={kho.MaKho}&khoiLuongHang={c.TongKhoiLuong}");
                    var taskTX = clientNS.GetFromJsonAsync<List<DriverAvailableDto>>($"api/quanlytaixe/lich-trinh-tai-xe?maKho={kho.MaKho}");

                    await Task.WhenAll(taskXe, taskTX);
                    var xePhuHop = taskXe.Result?.OrderBy(v => v.TaiTrongToiDaKg).FirstOrDefault();
                    var txChon = taskTX.Result?.OrderByDescending(t => t.DiemUyTin).FirstOrDefault();

                    if (xePhuHop == null || txChon == null) continue;

                    // 4. LƯU LỘ TRÌNH VÀ CHI TIẾT (Local DB)
                    var loTrinhMoi = new LoTrinh
                    {
                        MaPhuongTien = xePhuHop.MaPhuongTien,
                        MaTaiXeChinh = txChon.MaNguoiDung,
                        TrangThai = "Đang hoạt động", // Cập nhật trạng thái lộ trình
                        ThoiGianBatDauKeHoach = DateTime.Now
                    };
                    context.LoTrinhs.Add(loTrinhMoi);
                    await context.SaveChangesAsync();

                    foreach (var maDon in c.DanhSachMaDonHang)
                    {
                        context.ChiTietLoTrinhKienHangs.Add(new ChiTietLoTrinhKienHang
                        {
                            MaLoTrinh = loTrinhMoi.MaLoTrinh,
                            MaDonHang = maDon,
                            TrangThaiTrenXe = "Đã tiếp nhận"
                        });
                    }

                    context.DiemDungs.Add(new DiemDung
                    {
                        MaLoTrinh = loTrinhMoi.MaLoTrinh,
                        MaDiaChi = c.MaDiaChiCum,
                        ThuTuDung = 1,
                        LoaiDung = "Pickup",
                        EtaKeHoach = DateTime.Now.AddMinutes(30)
                    });

                    // 5. GỌI API CẬP NHẬT TRẠNG THÁI LIÊN SERVER

                    // A. Cập nhật trạng thái Xe thành "Đang hoạt động"
                    // Trong file QuanLyDieuPhoiLoTrinh.cs
                    
                    xePhuHop.TrangThai = "Đang hoạt động";

                    await clientPT.PutAsJsonAsync($"api/quanlyxe/capnhatxe/{xePhuHop.MaPhuongTien}", xePhuHop);

                    // B. Cập nhật trạng thái Tài xế thành "Đang hoạt động"
                    
                    await clientNS.PatchAsJsonAsync("api/quanlytaixe/cap-nhat-trang-thai", new
                    {
                        MaNguoiDung = txChon.MaNguoiDung,
                        TrangThaiMoi = "Đang hoạt động"
                    });

                    // C. Cập nhật trạng thái toàn bộ Đơn hàng trong cụm thành "Đã được tiếp nhận"
                    // Giả sử server Đơn hàng có API nhận danh sách ID đơn hàng để update hàng loạt
                    await clientDH.PutAsJsonAsync("api/quanlydonhang/cap-nhat-trang-thai-nhieu", new
                    {
                        DanhSachMaDonHang = c.DanhSachMaDonHang,
                        TrangThaiMoi = "Đã được tiếp nhận"
                    });

                    finalProcessedData.Add(new
                    {
                        MaLoTrinh = loTrinhMoi.MaLoTrinh,
                        BienSoXe = xePhuHop.BienSo,
                        TenTaiXe = txChon.HoTen,
                        SoLuongDonHang = c.SoLuongDonHang,
                        KhoPhuTrach = kho.TenKho,
                        TrangThaiHienTai = "Đang hoạt động"
                    });
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    status = "Success",
                    message = "Hệ thống đã phân xe, tài xế và kích hoạt lộ trình.",
                    totalProcessed = finalProcessedData.Count,
                    data = finalProcessedData
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { error = "Lỗi xử lý điều phối", detail = ex.Message });
            }
        }
    }
}