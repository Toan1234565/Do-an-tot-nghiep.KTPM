using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using QuanLyLoTrinhTheoDoi.Models;
using QuanLyLoTrinhTheoDoi.Models12;
using QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh;
using QuanLyLoTrinhTheoDoi.Models12.QuanLyLoTrinh.cs;
using System.Net.Http;
using System.Security.Cryptography.Xml;

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
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public QuanLyDieuPhoiLoTrinh(TmdtContext context, IMemoryCache cache, IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider,
            ILogger<QuanLyDieuPhoiLoTrinh> logger )
        {
            _context = context;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        [HttpGet("danhsachlotrinh")]
        public async Task<IActionResult> GetAllLoTrinh(
            [FromQuery] DateTime? batdau,
            [FromQuery] DateTime? ketthuc,
            [FromQuery] string? TrangThai = "Chờ khởi hành",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                // 1. Khởi tạo Cache Key
                var cacheKey = $"GetAllLoTrinh_{batdau?.ToString("yyyyMMdd")}_{ketthuc?.ToString("yyyyMMdd")}_{TrangThai}_{page}_{pageSize}";

                // Kiểm tra MemoryCache
                if (_cache.TryGetValue(cacheKey, out var cachedData))
                {
                    _logger.LogInformation("Lấy dữ liệu lộ trình từ cache.");
                    return Ok(cachedData);
                }

                // 2. Xây dựng Query
                var query = _context.LoTrinhs
                    .Where(lt => lt.LoTrinhTuyen == true)
                    .AsQueryable();

                // Lọc theo khoảng ngày (Nếu có)
                if (batdau.HasValue)
                {
                    var startDate = batdau.Value.Date;
                    query = query.Where(lt => lt.ThoiGianBatDauKeHoach >= startDate);
                }
                if (ketthuc.HasValue)
                {
                    var endDate = ketthuc.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(lt => lt.ThoiGianBatDauKeHoach <= endDate);
                }

                // Lọc theo Trạng Thái (Nếu không phải "Tất cả")
                if (!string.IsNullOrEmpty(TrangThai) && TrangThai != "Tất cả")
                {
                    query = query.Where(lt => lt.TrangThai == TrangThai);
                }

                // 3. Tính toán tổng số bản ghi trước khi phân trang
                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                // 4. Lấy dữ liệu và Map DTO
                var data = await query
                    .OrderByDescending(tg => tg.ThoiGianBatDauKeHoach)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(sc => new LoTrinhModels
                    {
                        MaLoTrinh = sc.MaLoTrinh,
                        MaPhuongTien = sc.MaPhuongTien,
                        MaTaiXeChinh = sc.MaTaiXeChinh,
                        MaTaiXePhu = sc.MaTaiXePhu,
                        ThoiGianBatDauKeHoach = sc.ThoiGianBatDauKeHoach,
                        ThoiGianBatDauThucTe = sc.ThoiGianBatDauThucTe,
                        TrangThai = sc.TrangThai,
                        

                        // Tính tổng số lượng nhanh
                        TongSoDonHang = sc.ChiTietLoTrinhKienHangs.Count(),
                        TongSoDiemDung = sc.DiemDungs.Count()

                        
                    }).ToListAsync();

                var result = new
                {
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    CurrentPage = page,
                    PageSize = pageSize,
                    Data = data
                };

                // 5. Lưu vào Cache (Hết hạn sau 5 phút)
               
                var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));
                _cache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách lộ trình: {ex.Message}");
                return StatusCode(500, new { error = "Lỗi hệ thống", detail = ex.Message });
            }
        }

        [HttpGet("chi-tiet-lo-trinh/{maLoTrinh}")]
        public async Task<IActionResult> GetChiTietLoTrinh(int maLoTrinh)
        {
            try
            {
                // 1. Lấy dữ liệu từ Database
                var loTrinh = await _context.LoTrinhs
                    .Include(lt => lt.ChiTietLoTrinhKienHangs)
                    .Include(lt => lt.DiemDungs)
                    .Include(lt => lt.ChiPhiLoTrinhs)
                    .Include(lt => lt.SuCos)
                    .FirstOrDefaultAsync(lt => lt.MaLoTrinh == maLoTrinh);

                if (loTrinh == null)
                    return NotFound(new { success = false, message = "Lộ trình không tồn tại" });

                // 2. Mapping sang DTO/Model
                var result = new LoTrinhModels
                {
                    MaLoTrinh = loTrinh.MaLoTrinh,
                    MaPhuongTien = loTrinh.MaPhuongTien,
                    MaTaiXeChinh = loTrinh.MaTaiXeChinh,
                    MaTaiXePhu = loTrinh.MaTaiXePhu,
                    ThoiGianBatDauKeHoach = loTrinh.ThoiGianBatDauKeHoach,
                    ThoiGianBatDauThucTe = loTrinh.ThoiGianBatDauThucTe,
                    TrangThai = loTrinh.TrangThai,

                    TongSoDonHang = loTrinh.ChiTietLoTrinhKienHangs.Count(),
                    TongSoDiemDung = loTrinh.DiemDungs.Count(),

                    // Map danh sách con
                    DiemDungs = loTrinh.DiemDungs.Select(dd => new DiemDungModels
                    {
                        MaDiaChi = dd.MaDiaChi,
                        ThuTuDung = dd.ThuTuDung,
                        LoaiDung = dd.LoaiDung
                    }).ToList(),

                    ChiTietLoTrinhKienHangs = loTrinh.ChiTietLoTrinhKienHangs.Select(ct => new ChiTietLoTrinhModels
                    {
                        MaDonHang = ct.MaDonHang,
                        TrangThaiTrenXe = ct.TrangThaiTrenXe
                    }).ToList(),

                    ChiPhiLoTrinhs = loTrinh.ChiPhiLoTrinhs.Select(cp => new ChiPhiLoTrinhModels
                    {
                        SoTien = cp.SoTien,
                        LoaiChiPhi = cp.LoaiChiPhi,

                        GhiChu = cp.GhiChu
                    }).ToList()
                };
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết lộ trình {MaLoTrinh}", maLoTrinh);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống nội bộ" });
            }
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
                // 1. Lấy danh sách cụm đơn hàng
                var resDonHang = await clientDH.PostAsJsonAsync("api/quanlydonhang/cho-dieu-phoi", new { });
                var responseData = await resDonHang.Content.ReadFromJsonAsync<DonHangResponse>();
                if (responseData?.Clusters == null || !responseData.Clusters.Any()) return NotFound("Không có đơn hàng chờ điều phối.");

                // --- TỐI ƯU BƯỚC 2 & 4: GOM TẤT CẢ ID ĐỊA CHỈ ĐỂ GỌI BATCH API ---
                var allAddressIds = responseData.Clusters
                    .Select(c => c.MaDiaChiLayHang > 0 ? c.MaDiaChiLayHang : c.MaDiaChiCum)
                    .Where(id => id > 0).Distinct().ToList();

                // CHỈ GỌI API KHO 1 LẦN DUY NHẤT (Batch Call)
                var resBatchKho = await clientKho.PostAsJsonAsync("api/quanlykhobai/tim-kho-theo-lo", new { MaDiaChis = allAddressIds });
                if (!resBatchKho.IsSuccessStatusCode) return BadRequest("Lỗi kết nối Service Kho.");

                var warehouseMap = await resBatchKho.Content.ReadFromJsonAsync<Dictionary<string, KhoGanNhatResponse>>();
                // Lưu ý: JSON Key thường là string khi deserialize Dictionary

                var clustersByWarehouse = new Dictionary<int, List<ClusterResult>>();
                var warehouseInfoMap = new Dictionary<int, KhoGanNhatResponse>();

                // 2. Phân loại cụm đơn hàng vào từng kho dựa trên kết quả Batch
                foreach (var c in responseData.Clusters)
                {
                    int idDiaChi = c.MaDiaChiLayHang > 0 ? c.MaDiaChiLayHang : c.MaDiaChiCum;

                    if (warehouseMap != null && warehouseMap.TryGetValue(idDiaChi.ToString(), out var kho))
                    {
                        if (!clustersByWarehouse.ContainsKey(kho.MaKho))
                        {
                            clustersByWarehouse[kho.MaKho] = new List<ClusterResult>();
                            warehouseInfoMap[kho.MaKho] = kho;
                        }
                        c.MaDiaChiLayHang = idDiaChi; // Cập nhật lại ID thực tế
                                                      // Thay vì add trực tiếp, ta khởi tạo đối tượng ClusterResult mới từ dữ liệu Api
                        clustersByWarehouse[kho.MaKho].Add(new ClusterResult
                        {
                            MaVungH3 = c.MaVungH3,
                            SoLuongDonHang = c.SoLuongDonHang,
                            TongKhoiLuong = c.TongKhoiLuong,
                            TongTheTich = c.TongTheTich,
                            DanhSachMaDonHang = c.DanhSachMaDonHang,
                            MaDiaChiLayHang = idDiaChi // Sử dụng biến idDiaChi đã xác định
                        });
                    }
                }

                var finalProcessedData = new List<object>();
                var skippedClusters = new List<object>();

                // 3. XỬ LÝ ĐIỀU PHỐI CHI TIẾT CHO TỪNG KHO
                foreach (var entry in clustersByWarehouse)
                {
                    int maKhoId = entry.Key;
                    var danhSachCumCuaKho = entry.Value;
                    var khoInfo = warehouseInfoMap[maKhoId];

                    // Lấy danh sách Xe và Tài xế Sẵn sàng tại kho này
                    // Lưu ý: Truyền đúng maKhoId thu được từ Service Kho
                    var xeCuaKho = await clientPT.GetFromJsonAsync<List<VehicleFreeDto>>($"api/quanlyxe/xe-san-sang-dieu-phoi?maKho={maKhoId}");
                    var txCuaKho = await clientNS.GetFromJsonAsync<List<DriverAvailableDto>>($"api/quanlytaixe/lich-trinh-tai-xe?maKho={maKhoId}");

                    // KIỂM TRA DỮ LIỆU ĐẦU VÀO TRƯỚC KHI CHẠY THUẬT TOÁN
                    if (xeCuaKho == null || !xeCuaKho.Any() || txCuaKho == null || !txCuaKho.Any())
                    {
                        skippedClusters.Add(new
                        {
                            Kho = khoInfo.TenKho,
                            MaKho = maKhoId,
                            LyDo = (xeCuaKho == null || !xeCuaKho.Any()) ? "Thiếu xe tải trọng phù hợp" : "Thiếu tài xế trong ca trực hiện tại",
                            DonHangAnhHuong = danhSachCumCuaKho.SelectMany(x => x.DanhSachMaDonHang).ToList()
                        });
                        continue;
                    }

                    // 4. Thuật toán Bin Packing (Gán cụm đơn hàng vào xe dựa trên tải trọng)
                    var assignments = ApplyFirstFitDecreasingBPP(danhSachCumCuaKho, xeCuaKho);

                    foreach (var assign in assignments)
                    {
                        var xeSelected = assign.Key;
                        var clustersInXe = assign.Value;

                        // Lấy tài xế có điểm uy tín cao nhất và chưa bị gán
                        var txChon = txCuaKho.OrderByDescending(t => t.DiemUyTin).FirstOrDefault();

                        if (txChon == null)
                        {
                            skippedClusters.Add(new
                            {
                                Kho = khoInfo.TenKho,
                                LyDo = "Hết tài xế để bàn giao xe tiếp theo",
                                XeBiTrong = xeSelected.BienSo
                            });
                            continue;
                        }

                        // Loại bỏ tài xế này khỏi danh sách chờ để không gán cho xe khác trong cùng vòng lặp
                        txCuaKho.Remove(txChon);

                        // 5. LƯU DỮ LIỆU LỘ TRÌNH VÀO DATABASE
                        // --- PHẦN TẠO LỘ TRÌNH ---
                        var loTrinhMoi = new LoTrinh
                        {
                            MaPhuongTien = xeSelected.MaPhuongTien,
                            MaTaiXeChinh = txChon.MaNguoiDung,
                            TrangThai = "Đang hoạt động",
                            ThoiGianBatDauKeHoach = DateTime.Now,
                            GhiChu = $"Điều phối tự động - {khoInfo.TenKho}",
                            LoTrinhTuyen = true,
                            MaKhoQuanLy = maKhoId // Đảm bảo gán cả kho quản lý nếu DB yêu cầu
                        };

                        context.LoTrinhs.Add(loTrinhMoi);
                        await context.SaveChangesAsync(); // BẮT BUỘC: Để lấy MaLoTrinh cho các bảng con

                        // --- PHẦN TẠO ĐIỂM DỪNG ---
                        var danhSachDiemDung = new List<DiemDung>();

                        // 1. Điểm xuất phát tại Kho
                        danhSachDiemDung.Add(new DiemDung
                        {
                            MaLoTrinh = loTrinhMoi.MaLoTrinh, // Giờ đã có ID thật (VD: 1031)
                            MaVungH3 = khoInfo.MaVungH3 ?? "",
                            ThuTuDung = 1,
                            LoaiDung =khoInfo.TenKho,
                            MaDiaChi = khoInfo.MaDiaChi,
                            EtaKeHoach = DateTime.Now
                        });

                        // 2. Các điểm Pickup đơn hàng
                        int thuTu = 2;
                        foreach (var cluster in clustersInXe)
                        {
                            danhSachDiemDung.Add(new DiemDung
                            {
                                MaLoTrinh = loTrinhMoi.MaLoTrinh,
                                MaVungH3 = cluster.MaVungH3,
                                ThuTuDung = thuTu++,
                                LoaiDung = "Pickup",
                                MaDiaChi = cluster.MaDiaChiLayHang,
                                EtaKeHoach = DateTime.Now.AddMinutes(30)
                            });

                            // Gắn kiện hàng vào lộ trình
                            foreach (var maDon in cluster.DanhSachMaDonHang)
                            {
                                context.ChiTietLoTrinhKienHangs.Add(new ChiTietLoTrinhKienHang
                                {
                                    MaLoTrinh = loTrinhMoi.MaLoTrinh,
                                    MaDonHang = maDon,
                                    TrangThaiTrenXe = "Đã tiếp nhận",
                                    ThoiGianCapNhat = DateTime.Now
                                });
                            }
                        }
                        danhSachDiemDung.Add(new DiemDung
                        {
                            MaLoTrinh = loTrinhMoi.MaLoTrinh, // Giờ đã có ID thật (VD: 1031)
                            MaVungH3 = khoInfo.MaVungH3 ?? "",
                            ThuTuDung = thuTu++,
                            LoaiDung = khoInfo.TenKho,
                            MaDiaChi = khoInfo.MaDiaChi,
                            EtaKeHoach = DateTime.Now
                        });

                        context.DiemDungs.AddRange(danhSachDiemDung);
                        await context.SaveChangesAsync(); // Lưu toàn bộ điểm dừng và kiện hàng

                        // 1. Tính tổng quãng đường
                        double tongKmDuKien = TinhTongKmDuKien(danhSachDiemDung);

                        decimal dinhMucNhienLieu = (decimal)(xeSelected.MucTieuHaoNhienLieu ?? 10.0);

                        // 3. Khai báo giá nhiên liệu hiện tại 
                        decimal giaNhienLieuHienTai = await GetCurrentFuelPriceAsync("DO");

                        // 4. Công thức tính toán: (Tổng Km / 100) * Định mức * Giá tiền thực tế
                        decimal chiPhiXangDauDuKien = (decimal)(tongKmDuKien / 100) * dinhMucNhienLieu * giaNhienLieuHienTai;

                        // 5. Ghi nhận vào DB
                        var chiPhiMoi = new ChiPhiLoTrinh
                        {
                            MaLoTrinh = loTrinhMoi.MaLoTrinh,
                            LoaiChiPhi = "Xăng dầu (Dự kiến theo giá thị trường)",
                            SoTien = Math.Round(chiPhiXangDauDuKien, 0),
                            GhiChu = $"Giá cập nhật: {giaNhienLieuHienTai:N0}đ/L | Quãng đường: {tongKmDuKien}km",
                            ChungTuKemTheo = null
                        };

                        context.ChiPhiLoTrinhs.Add(chiPhiMoi);
                        await context.SaveChangesAsync();

                        // 6. CẬP NHẬT TRẠNG THÁI LIÊN SERVICE (Async)
                        var updateTasks = new List<Task> {
                        clientPT.PostAsJsonAsync($"api/quanlyxe/cap-nhat-trang-thai-xe/{xeSelected.MaPhuongTien}", new { TrangThai = "Chờ khởi hành" }),
                        clientNS.PostAsJsonAsync("api/quanlytaixe/cap-nhat-trang-thai", new { MaNguoiDung = txChon.MaNguoiDung, TrangThaiMoi = "Đang hoạt động" }),
                        clientDH.PutAsJsonAsync("api/quanlydonhang/cap-nhat-trang-thai-nhieu", new {
                        DanhSachMaDonHang = clustersInXe.SelectMany(x => x.DanhSachMaDonHang).ToList(),
                        TrangThaiMoi = "Đã được tiếp nhận"
                    })
                };
                        await Task.WhenAll(updateTasks);

                        finalProcessedData.Add(new
                        {
                            LoTrinhId = loTrinhMoi.MaLoTrinh,
                            Xe = xeSelected.BienSo,
                            TaiXe = txChon.HoTen,
                            Kho = khoInfo.TenKho
                        });
                    }
                }

                await transaction.CommitAsync();
                return Ok(new
                {
                    status = skippedClusters.Any() ? "Warning" : "Success",
                    message = skippedClusters.Any() ? "Điều phối hoàn tất một phần, một số đơn hàng chưa có xe/tài xế." : "Đã điều phối toàn bộ đơn hàng.",
                    data = finalProcessedData,
                    unassignedReport = skippedClusters // Thông tin chi tiết về việc thiếu hụt
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, ex.Message);
            }
        }

        // Thêm một hàm tính tổng khoảng cách dự kiến (có thể tách ra Map/Routing Service)
        private double TinhTongKmDuKien(List<DiemDung> danhSachDiemDung)
        {
            double tongKm = 0;
            // Logic thực tế: Gọi API OSRM/Google Maps truyền danh sách Tọa độ của các DiemDung
            // Tạm thời mô phỏng: Giả sử mỗi điểm dừng cách nhau trung bình 5-10km
            if (danhSachDiemDung.Count > 1)
            {
                // Ví dụ: tính đại khái = (Số điểm dừng - 1) * 7.5km
                tongKm = (danhSachDiemDung.Count - 1) * 7.5;
            }
            return Math.Round(tongKm, 2);
        }
        [NonAction]
        public async Task<decimal> GetCurrentFuelPriceAsync(string fuelType = "DO")
        {
            try
            {
                // Giả sử bạn sử dụng một API trung gian hoặc đã viết Scraper từ Petrolimex
                // Nguồn tham khảo: https://www.petrolimex.com.vn/
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetFromJsonAsync<FuelPriceResponse>("https://api.example.com/v1/fuel-prices/vietnam");

                if (response != null)
                {
                    // Trả về giá Dầu Diesel (DO) cho xe tải hoặc Xăng tùy theo loại xe
                    return fuelType == "DO" ? response.DieselPrice : response.GasolinePrice;
                }
            }
            catch (Exception)
            {
                // Fallback: Nếu API ngoài lỗi, trả về giá dự phòng gần nhất để hệ thống không chết
                return 24500m;
            }
            return 24500m;
        }

        private Dictionary<VehicleFreeDto, List<ClusterResult>> ApplyFirstFitDecreasingBPP(List<ClusterResult> clusters, List<VehicleFreeDto> vehicles)
        {
            var assignments = new Dictionary<VehicleFreeDto, List<ClusterResult>>();
            var sortedClusters = clusters.OrderByDescending(c => c.TongKhoiLuong).ToList();

            foreach (var cluster in sortedClusters)
            {
                foreach (var xe in vehicles)
                {
                    if (!assignments.ContainsKey(xe)) assignments[xe] = new List<ClusterResult>();

                    var load = assignments[xe].Sum(c => c.TongKhoiLuong);
                    if (load + cluster.TongKhoiLuong <= xe.TaiTrongToiDaKg)
                    {
                        assignments[xe].Add(cluster);
                        break;
                    }
                }
            }
            return assignments.Where(a => a.Value.Any()).ToDictionary(a => a.Key, a => a.Value);
        }

       
    }
}