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
                var query = _context.LoTrinhs.AsQueryable();

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
                /// 1. Lấy danh sách cụm đơn hàng (ClusterResult chứa MaVungH3: string)
                var resDonHang = await clientDH.PostAsJsonAsync("api/quanlydonhang/cho-dieu-phoi", new { });
                var responseData = await resDonHang.Content.ReadFromJsonAsync<DonHangResponse>();
                if (responseData?.Clusters == null) return NotFound();

                var finalProcessedData = new List<object>();
                // 2. GOM NHÓM CÁC CỤM THEO KHO (Mỗi vùng/cụm thuộc về 1 kho phụ trách)
                var clustersByWarehouse = new Dictionary<int, List<ClusterResult>>();
                var warehouseInfoMap = new Dictionary<int, KhoGanNhatResponse>();



                foreach (var c in responseData.Clusters)
                {
                    // 1. Lấy ID địa chỉ trực tiếp từ ClusterResult (Đã gán từ API DonHang)
                    // Ưu tiên MaDiaChiLayHang (vì đây là đơn Pickup), nếu không có dùng MaDiaChiCum
                    int idDiaChiThucTe = c.MaDiaChiLayHang > 0 ? c.MaDiaChiLayHang : c.MaDiaChiCum;

                    // 2. Logic dự phòng: Nếu vì lý do nào đó ID vẫn bằng 0, mới gọi API chi tiết
                    if (idDiaChiThucTe <= 0)
                    {
                        int maDonHangDaiDien = c.DanhSachMaDonHang?.FirstOrDefault() ?? 0;
                        if (maDonHangDaiDien > 0)
                        {
                            var resDhDetail = await clientDH.GetAsync($"api/quanlydonhang/get-by-id/{maDonHangDaiDien}");
                            if (resDhDetail.IsSuccessStatusCode)
                            {
                                var dhDto = await resDhDetail.Content.ReadFromJsonAsync<DonHangDto>();
                                idDiaChiThucTe = dhDto?.MaDiaChiGiao ?? 0;
                            }
                        }
                    }

                    // 3. Kiểm tra cuối cùng: Nếu vẫn không có địa chỉ thì bỏ qua cụm này
                    if (idDiaChiThucTe <= 0)
                    {
                        _logger.LogWarning($"Cụm vùng {c.MaVungH3} bị bỏ qua do không xác định được mã địa chỉ thực tế.");
                        continue;
                    }

                    // 4. Gọi API Kho để tìm kho gần nhất phục vụ địa chỉ này
                    try
                    {
                        var resKho = await clientKho.GetAsync($"api/quanlykhobai/tim-kho-gan-nhat/{idDiaChiThucTe}");

                        if (!resKho.IsSuccessStatusCode)
                        {
                            _logger.LogError($"API Kho trả về lỗi {resKho.StatusCode} cho địa chỉ {idDiaChiThucTe}");
                            continue;
                        }

                        var kho = await resKho.Content.ReadFromJsonAsync<KhoGanNhatResponse>();

                        if (kho != null && kho.MaKho > 0)
                        {
                            // Nếu kho này chưa có trong bản đồ gom nhóm, khởi tạo mới
                            if (!clustersByWarehouse.ContainsKey(kho.MaKho))
                            {
                                clustersByWarehouse[kho.MaKho] = new List<ClusterResult>();
                                warehouseInfoMap[kho.MaKho] = kho;
                            }

                            // Gán thông tin cụm vào danh sách chờ xử lý của kho đó
                            clustersByWarehouse[kho.MaKho].Add(new ClusterResult
                            {
                                MaVungH3 = c.MaVungH3,
                                SoLuongDonHang = c.SoLuongDonHang,
                                TongKhoiLuong = c.TongKhoiLuong,
                                TongTheTich = c.TongTheTich,
                                DanhSachMaDonHang = c.DanhSachMaDonHang,
                                MaDiaChiLayHang = idDiaChiThucTe // Giữ lại ID địa chỉ để dùng cho lộ trình sau này
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Lỗi khi tìm kho cho cụm {c.MaVungH3}: {ex.Message}");
                    }
                }

                // 3. XỬ LÝ ĐIỀU PHỐI RIÊNG BIỆT CHO TỪNG KHO
                foreach (var entry in clustersByWarehouse)
                {
                    int maKho = entry.Key;
                    var danhSachCumCuaKho = entry.Value;
                    var khoInfo = warehouseInfoMap.ContainsKey(maKho) ? warehouseInfoMap[maKho] : null;

                    _logger.LogInformation($"--- Bắt đầu điều phối cho Kho: {khoInfo?.TenKho ?? maKho.ToString()} ---");

                    // Gọi API lấy xe và tài xế THEO KHO
                    var xeCuaKho = await clientPT.GetFromJsonAsync<List<VehicleFreeDto>>($"api/quanlyxe/xe-san-sang-dieu-phoi?maKho={maKho}");
                    var txCuaKho = await clientNS.GetFromJsonAsync<List<DriverAvailableDto>>($"api/quanlytaixe/lich-trinh-tai-xe?maKho={maKho}");

                    if (xeCuaKho == null || !xeCuaKho.Any() || txCuaKho == null || !txCuaKho.Any())
                    {
                        _logger.LogWarning($"Kho {maKho} không đủ tài nguyên (Xe: {xeCuaKho?.Count ?? 0}, TX: {txCuaKho?.Count ?? 0})");
                        continue;
                    }

                    // 4. Chạy thuật toán Bin Packing (FirstFitDecreasing) để xếp cụm vào xe
                    var assignments = ApplyFirstFitDecreasingBPP(danhSachCumCuaKho, xeCuaKho);

                    foreach (var assign in assignments)
                    {
                        var xe = assign.Key;
                        var clustersForThisVehicle = assign.Value;

                        if (!clustersForThisVehicle.Any()) continue;

                        // Chọn tài xế có điểm uy tín cao nhất hiện có của kho này
                        var txChon = txCuaKho.OrderByDescending(t => t.DiemUyTin).FirstOrDefault();
                        if (txChon == null)
                        {
                            _logger.LogWarning($"Hết tài xế cho xe {xe.BienSo} tại kho {maKho}");
                            break;
                        }
                        txCuaKho.Remove(txChon);

                        // 5. LƯU LỘ TRÌNH (Local DB - Service Điều Phối)
                        var loTrinhMoi = new LoTrinh
                        {
                            MaPhuongTien = xe.MaPhuongTien,
                            MaTaiXeChinh = txChon.MaNguoiDung,
                            TrangThai = "Đang hoạt động",
                            ThoiGianBatDauKeHoach = DateTime.Now,
                            GhiChu = $"Lộ trình tự động từ kho {khoInfo?.TenKho}"
                        };

                        context.LoTrinhs.Add(loTrinhMoi);
                        await context.SaveChangesAsync(); // Lưu để lấy MaLoTrinh (Identity)

                        // Lấy danh sách tất cả mã đơn hàng được xếp vào xe này
                        var allMaDonInVehicle = clustersForThisVehicle.SelectMany(c => c.DanhSachMaDonHang).ToList();

                        // Lưu chi tiết kiện hàng của lộ trình
                        foreach (var maDon in allMaDonInVehicle)
                        {
                            context.ChiTietLoTrinhKienHangs.Add(new ChiTietLoTrinhKienHang
                            {
                                MaLoTrinh = loTrinhMoi.MaLoTrinh,
                                MaDonHang = maDon,
                                TrangThaiTrenXe = "Đã tiếp nhận"
                            });
                        }

                        // Lưu các điểm dừng dựa trên mã vùng H3
                        int thuTu = 1;
                        foreach (var cluster in clustersForThisVehicle)
                        {
                            context.DiemDungs.Add(new DiemDung
                            {
                                MaLoTrinh = loTrinhMoi.MaLoTrinh,
                                MaVungH3 = cluster.MaVungH3, 
                                ThuTuDung = thuTu++,
                                LoaiDung = "Pickup",
                                MaDiaChi = cluster.MaDiaChiLayHang, // Gán mã địa chỉ thực tế để sau này có thể tra cứu
                                EtaKeHoach = DateTime.Now.AddMinutes(30 * thuTu)
                            });
                        }
                        await context.SaveChangesAsync();

                        // 6. ĐỒNG BỘ TRẠNG THÁI LIÊN SERVER (Cập nhật các Microservices khác)
                        try
                        {
                            await clientPT.PostAsJsonAsync($"api/quanlyxe/cap-nhat-trang-thai-xe/{xe.MaPhuongTien}",
                            new { TrangThai = "Chờ khởi hành" });

                            // Cập nhật trạng thái tài xế: 
                            // Lưu ý: Phải khớp với Class UpdateTaiXeTrangTai mà API TaiXe đang dùng
                            await clientNS.PostAsJsonAsync("api/quanlytaixe/cap-nhat-trang-thai",
                            new
                            {
                                    MaNguoiDung = txChon.MaNguoiDung,
                                    TrangThaiMoi = "Đang hoạt động"
                            });

                            // Cập nhật hàng loạt đơn hàng sang trạng thái 'Đã được tiếp nhận/Đang lấy hàng'
                            await clientDH.PutAsJsonAsync("api/quanlydonhang/cap-nhat-trang-thai-nhieu", new
                            {
                                DanhSachMaDonHang = allMaDonInVehicle,
                                TrangThaiMoi = "Đã được tiếp nhận"
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Lỗi đồng bộ liên server cho lộ trình {loTrinhMoi.MaLoTrinh}: {ex.Message}");
                        }

                        finalProcessedData.Add(new
                        {
                            MaLoTrinh = loTrinhMoi.MaLoTrinh,
                            Xe = xe.BienSo,
                            TaiXe = txChon.HoTen,
                            Kho = khoInfo?.TenKho ?? maKho.ToString(),
                            SoDonHang = allMaDonInVehicle.Count,
                            TrongTaiSuDung = clustersForThisVehicle.Sum(c => c.TongKhoiLuong)
                        });
                    }
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