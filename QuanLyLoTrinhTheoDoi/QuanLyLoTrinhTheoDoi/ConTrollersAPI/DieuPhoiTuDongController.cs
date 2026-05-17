using Google.OrTools.ConstraintSolver;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuanLyLoTrinhTheoDoi.Models;
using QuanLyLoTrinhTheoDoi.Models12;
using QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh;
using QuanLyLoTrinhTheoDoi.Models12.LienServer.cs;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.KhoBai;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.PhuongTien;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyLoTrinhTheoDoi.ConTrollersAPI
{
    [ApiController]
    [Route("api/DieuPhoiThongMinh")]
    public class DieuPhoiTuDongController : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DieuPhoiTuDongController> _logger;
        private readonly IServiceProvider _serviceProvider;
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public DieuPhoiTuDongController(IServiceProvider serviceProvide, TmdtContext context, IMemoryCache cache, IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider,
            ILogger<DieuPhoiTuDongController> logger)
        {
            _serviceProvider = serviceProvider;
            _context = context;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpPost("chay-he-thong-tu-dong")]
        public async Task<IActionResult> ChayHeThongDieuPhoiTuDong([FromBody] DieuPhoiRequest request)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TmdtContext>();
            var ptTxService = scope.ServiceProvider.GetRequiredService<IPhuongTienTaiXeService>();
            var donHangService = scope.ServiceProvider.GetRequiredService<IDonHangService>();
            var khoBaiService = scope.ServiceProvider.GetRequiredService<IKhoBaiService>();
            var phuongTienService = scope.ServiceProvider.GetRequiredService<IPhuongTienServiceClient>();
            var nhanVienService = scope.ServiceProvider.GetRequiredService<INhanVienService>();

            string trangThaiGom = string.IsNullOrEmpty(request?.TrangThaiDonHang) ? "Chờ lấy hàng" : request.TrangThaiDonHang;

            // Giới hạn khung giờ vàng cho chặng First-Mile và Last-Mile nội đô
            if (trangThaiGom == "Chờ lấy hàng" || trangThaiGom == "Chờ giao hàng")
            {
                var gioHienTai = TimeOnly.FromDateTime(DateTime.Now);
                var gioBatDau = new TimeOnly(8, 0);
                var gioKetThuc = new TimeOnly(18, 0);

                if (gioHienTai < gioBatDau || gioHienTai > gioKetThuc)
                {
                    return Ok(new
                    {
                        status = "Pending",
                        message = "Ngoài khung giờ hoạt động (08:00 - 18:00). Hệ thống tạm hoãn sang ca sáng."
                    });
                }
            }

            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                var clusterReq = new QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang.ClusterRequest { TrangThaiDonHang = trangThaiGom };
                var responseData = await donHangService.TuDongGomNhomDonHangAsync(clusterReq);

                if (responseData?.Clusters == null || !responseData.Clusters.Any())
                    return NotFound($"Không có cụm đơn hàng nào thuộc chặng [{trangThaiGom}] cần xử lý.");

                if (request?.Limit > 0)
                {
                    int currentTotal = 0;
                    responseData.Clusters = responseData.Clusters
                        .TakeWhile(c => {
                            bool keep = currentTotal < request.Limit;
                            currentTotal += c.SoLuongDonHang;
                            return keep;
                        }).ToList();
                }

                var clustersByWarehouse = new Dictionary<int, List<QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang.ClusterResult>>();
                var clusterNghiepVuMap = new Dictionary<QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang.ClusterResult, string>();
                var warehouseInfoMap = new Dictionary<int, KhoTimDuocDto>();
                var allAddressIds = new List<int>();

                if (trangThaiGom == "Chờ trung chuyển")
                {
                    var tinhToanTrungChuyenClusters = new List<QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang.ClusterResult>();

                    foreach (var c in responseData.Clusters)
                    {
                        var cleanOrderLocations = new List<DynamicOrderLocationDto>();

                        foreach (var maDH in c.DanhSachMaDonHang)
                        {
                            var viTriDto = await donHangService.GetViTriHienTaiDonHangAsync(maDH);
                            int maKhoThucTe = (int)(viTriDto?.MaKhoHienTai ?? 0);

                            var thongTinGiaoHang = await donHangService.GetThongTinGiaoHangAsync(maDH);

                            if (maKhoThucTe == 0)
                            {
                                maKhoThucTe = PhanTichKhoTuDiaChi(thongTinGiaoHang?.MaDiaChiLayHang ?? 0);
                            }

                            // 🔥 ĐỊNH VÙNG ĐÍCH CHÍNH XÁC: Phân rã dựa trên MaDiaChiNhanHang thực tế của từng đơn
                            string mienDichThucTeCuaDon = "Central";
                            int maDiaChiNhan = thongTinGiaoHang?.MaDiaChiNhanHang ?? 0;

                            if (maDiaChiNhan == 2826 || (maDiaChiNhan >= 2822 && maDiaChiNhan <= 2823) || maDiaChiNhan == 2827 || maDiaChiNhan == 2829)
                            {
                                mienDichThucTeCuaDon = (maDiaChiNhan == 2826 || maDiaChiNhan == 2827 || maDiaChiNhan == 2829) ? "North" : "Central";
                            }
                            else if (maDiaChiNhan == 2824 || maDiaChiNhan == 2825 || (maDiaChiNhan >= 2833 && maDiaChiNhan <= 2837))
                            {
                                mienDichThucTeCuaDon = "South";
                            }

                            cleanOrderLocations.Add(new DynamicOrderLocationDto
                            {
                                MaDonHang = maDH,
                                MaKhoHienTai = maKhoThucTe,
                                MienDich = mienDichThucTeCuaDon,
                                MaDiaChiNhanHangThucTe = maDiaChiNhan
                            });
                        }

                        // Nhóm độc lập: Đơn đi Miền Bắc tách cụm riêng, đơn đi Miền Nam tách cụm riêng
                        var compositeGroups = cleanOrderLocations.GroupBy(x => new { x.MaKhoHienTai, x.MienDich });

                        foreach (var group in compositeGroups)
                        {
                            int khoXuatPhatThucTe = group.Key.MaKhoHienTai;
                            string mienDichThucTe = group.Key.MienDich;
                            var danhSachMaDonHangSach = group.Select(x => x.MaDonHang).ToList();

                            int maDiaChiKhoXuatPhat = GetMaDiaChiTuMaKho(khoXuatPhatThucTe);
                            if (!allAddressIds.Contains(maDiaChiKhoXuatPhat)) allAddressIds.Add(maDiaChiKhoXuatPhat);

                            int maKhoTrungTamDich = TimMaKhoChinhCuaVung(mienDichThucTe);
                            string nghiepVuXacDinh = (khoXuatPhatThucTe == maKhoTrungTamDich) ? "Chờ giao hàng" : "Chờ trung chuyển";

                            var mauDon = group.First();

                            var subCluster = new QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang.ClusterResult
                            {
                                MaVungH3 = mienDichThucTe, // Lưu giữ miền đích thực tế ("North", "Central", "South")
                                SoLuongDonHang = danhSachMaDonHangSach.Count,
                                MaDiaChiLayHang = maDiaChiKhoXuatPhat,
                                MaDiaChiCum = mauDon.MaDiaChiNhanHangThucTe,
                                MaDiaChiNhanHang = mauDon.MaDiaChiNhanHangThucTe,
                                DanhSachMaDonHang = danhSachMaDonHangSach,
                                TongKhoiLuong = c.TongKhoiLuong * ((double)danhSachMaDonHangSach.Count / c.SoLuongDonHang)
                            };

                            tinhToanTrungChuyenClusters.Add(subCluster);
                            clusterNghiepVuMap[subCluster] = nghiepVuXacDinh;
                        }
                    }
                    responseData.Clusters = tinhToanTrungChuyenClusters;
                }
                else
                {
                    allAddressIds = responseData.Clusters
                        .Select(c => (trangThaiGom == "Chờ lấy hàng") ? (c.MaDiaChiLayHang > 0 ? c.MaDiaChiLayHang : c.MaDiaChiCum) : c.MaDiaChiCum)
                        .Where(id => id > 0).Distinct().ToList();

                    foreach (var c in responseData.Clusters) clusterNghiepVuMap[c] = trangThaiGom;
                }

                var warehouseMap = await khoBaiService.TimKhoTheoLoAsync(new QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.KhoBai.BatchKhoRequest { MaDiaChis = allAddressIds });
                if (warehouseMap == null || !warehouseMap.Any()) return BadRequest("Lỗi đồng bộ dữ liệu với Microservice Kho bãi.");

                foreach (var c in responseData.Clusters)
                {
                    int idDiaChiKhoa = (trangThaiGom == "Chờ trung chuyển")
                        ? c.MaDiaChiLayHang
                        : ((trangThaiGom == "Chờ lấy hàng") ? (c.MaDiaChiLayHang > 0 ? c.MaDiaChiLayHang : c.MaDiaChiCum) : c.MaDiaChiCum);

                    if (warehouseMap.TryGetValue(idDiaChiKhoa, out var khoTuDong))
                    {
                        int maKho = khoTuDong.MaKho;
                        if (!clustersByWarehouse.ContainsKey(maKho))
                        {
                            clustersByWarehouse[maKho] = new List<QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang.ClusterResult>();
                            warehouseInfoMap[maKho] = khoTuDong;
                        }

                        var clusterChuanHoa = new QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang.ClusterResult
                        {
                            MaDiaChiCum = c.MaDiaChiCum,
                            MaDiaChiLayHang = c.MaDiaChiLayHang,
                            MaDiaChiNhanHang = c.MaDiaChiNhanHang,
                            SoLuongDonHang = c.SoLuongDonHang,
                            DanhSachMaDonHang = c.DanhSachMaDonHang,
                            TongKhoiLuong = c.TongKhoiLuong,
                            MaVungH3 = c.MaVungH3
                        };

                        clusterNghiepVuMap[clusterChuanHoa] = clusterNghiepVuMap[c];
                        clustersByWarehouse[maKho].Add(clusterChuanHoa);
                    }
                }

                var finalProcessedData = new List<object>();
                var skippedClusters = new List<object>();
                int maCaHienTai = xacDinhMaCaTheoGio(DateTime.Now);

                foreach (var entry in clustersByWarehouse)
                {
                    int maKhoId = entry.Key;
                    var danhSachCumCuaKho = entry.Value;
                    var khoInfo = warehouseInfoMap[maKhoId];
                    var groupsByNghiepVu = danhSachCumCuaKho.GroupBy(c => clusterNghiepVuMap[c]).ToList();

                    foreach (var nghiepVuGroup in groupsByNghiepVu)
                    {
                        string trangThaiHanhTrinhThucTe = nghiepVuGroup.Key;
                        var clustersTrongNhom = nghiepVuGroup.ToList();

                        int loaiXeYeuCau = (trangThaiHanhTrinhThucTe == "Chờ trung chuyển") ? 2 : 1;
                        var xeCuaKho = await phuongTienService.GetXeSanSangDieuPhoiAsync(loaiXeYeuCau, maKhoId) ?? new List<QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.PhuongTien.PhuongTienDTO>();

                        var clustersBppCompatible = clustersTrongNhom
                            .Select(c => new QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh.ClusterResult
                            {
                                MaDiaChiCum = c.MaDiaChiCum,
                                MaDiaChiNhanHang = c.MaDiaChiNhanHang,
                                MaDiaChiLayHang = c.MaDiaChiLayHang,
                                SoLuongDonHang = c.SoLuongDonHang,
                                DanhSachMaDonHang = c.DanhSachMaDonHang,
                                TongKhoiLuong = c.TongKhoiLuong,
                                MaVungH3 = c.MaVungH3
                            }).ToList();

                        var assignments = ApplyFirstFitDecreasingBPP(clustersBppCompatible, xeCuaKho);
                        var assignedClusterIds = assignments.SelectMany(a => a.Value).Select(c => c.MaDiaChiCum).ToList();
                        var unassignedClusters = clustersTrongNhom.Where(c => !assignedClusterIds.Contains(c.MaDiaChiCum)).ToList();
                        
                        foreach (var assign in assignments)
                        {
                            var xeSelected = assign.Key;
                            var clustersInXeBpp = assign.Value;
                            var clustersInXe = clustersTrongNhom.Where(c => clustersInXeBpp.Select(bx => bx.MaDiaChiCum).Contains(c.MaDiaChiCum)).ToList();

                            var mapping = await ptTxService.GetMappingByVehicleAsync(xeSelected.MaPhuongTien, maCaHienTai);
                            int? mappingPhuongTienTaiXeId = mapping?.MaPtTx;
                            string trangThaiBanDau = "Chờ khởi hành";

                            if (mapping != null)
                            {
                                var driverStatus = await nhanVienService.CheckDriverStatusAsync(mapping.MaNguoiDung);
                                if (driverStatus == null || !driverStatus.IsWorking)
                                {
                                    trangThaiBanDau = "Chờ điều phối thủ công";
                                    skippedClusters.Add(new { Kho = khoInfo.TenKho, LyDo = $"Xe {xeSelected.BienSo} chuyển duyệt tay do tài xế chưa check-in ca trực." });
                                }
                                else
                                {
                                    await nhanVienService.CapNhatTrangThaiTaiXeAsync(mapping.MaNguoiDung, true);
                                }
                            }
                            else
                            {
                                trangThaiBanDau = "Chờ điều phối thủ công";
                                skippedClusters.Add(new { Kho = khoInfo.TenKho, LyDo = $"Xe {xeSelected.BienSo} chưa được cấu hình tài xế cho ca làm việc hiện tại." });
                            }
                            double tongKhroiLuongXe = clustersInXe.Sum(c => c.TongKhoiLuong);
                            var loTrinhMoi = new LoTrinh
                            {
                                MaPtTx = mappingPhuongTienTaiXeId,
                                TrangThai = trangThaiBanDau,
                                ThoiGianBatDauKeHoach = DateTime.Now,
                                GhiChu = $"Hệ thống tự động gom chặng [{trangThaiHanhTrinhThucTe}] xuất phát tại {khoInfo.TenKho}",
                                LoTrinhTuyen = (trangThaiHanhTrinhThucTe == "Chờ trung chuyển"),
                                MaKhoQuanLy = maKhoId,
                                TongKhoiLuongKg = tongKhroiLuongXe
                            };

                            context.LoTrinhs.Add(loTrinhMoi);
                            await context.SaveChangesAsync();

                            await TaoDiemDungVaChiPhiMoi(scope.ServiceProvider, context, loTrinhMoi, khoInfo, clustersInXe, xeSelected, trangThaiHanhTrinhThucTe);

                            await phuongTienService.UpdateTrangThaiXeAsync(xeSelected.MaPhuongTien, new Models12.ThongTinLienServer.PhuongTien.UpdateTrangThaiXeDto { TrangThai = "Chờ khởi hành" });

                            string trangThaiDonHangMoi = trangThaiHanhTrinhThucTe == "Chờ lấy hàng" ? "Đã lên lộ trình" :
                                                         trangThaiHanhTrinhThucTe == "Chờ trung chuyển" ? "Đang luân chuyển miền" : "Đang giao hàng";

                            await donHangService.CapNhatTrangThaiNhieuDonHangAsync(new QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang.UpdateMultiStatusRequest
                            {
                                DanhSachMaDonHang = clustersInXe.SelectMany(x => x.DanhSachMaDonHang).Distinct().ToList(),
                                TrangThaiMoi = trangThaiDonHangMoi
                            });

                            finalProcessedData.Add(new { MaLoTrinh = loTrinhMoi.MaLoTrinh, KhoXuatPhat = khoInfo.TenKho, BienSoXe = xeSelected.BienSo, TrangThai = trangThaiBanDau });
                        }

                        foreach (var cluster in unassignedClusters)
                        {
                            // 1. Tìm một phương tiện trong kho có tải trọng đủ cân được tổng khối lượng của cụm này
                            // Ưu tiên xe đang ở trạng thái trống (Sẵn sàng) hoặc chưa được bốc vào lộ trình nào trước đó
                            var xePhuHopTaiTrong = xeCuaKho
                                .Where(x => x.TaiTrongToiDaKg >= cluster.TongKhoiLuong)
                                .OrderBy(x => x.TaiTrongToiDaKg) // Ưu tiên xe nhỏ nhất vừa đủ tải để tiết kiệm chi phí
                                .FirstOrDefault();

                            int? mappingPhuongTienTaiXeId = null;
                            string bienSoXeHienThi = "CHƯA_CÓ_XE";
                            string ghiChuLoTrinh = $"Hệ thống tự động gom chặng [{trangThaiHanhTrinhThucTe}] tại {khoInfo.TenKho} (Tồn đọng do thiếu xe tải khả dụng)";

                            if (xePhuHopTaiTrong != null)
                            {
                                bienSoXeHienThi = xePhuHopTaiTrong.BienSo;
                                ghiChuLoTrinh = $"Hệ thống tự động gom chặng [{trangThaiHanhTrinhThucTe}] tại {khoInfo.TenKho} - Đã gán xe {bienSoXeHienThi} (Chờ điều phối thủ công bổ sung tài xế)";

                                // Thử tìm cấu hình PtTx của xe này trong ca hiện tại
                                var mapping = await ptTxService.GetMappingByVehicleAsync(xePhuHopTaiTrong.MaPhuongTien, maCaHienTai);
                                if (mapping != null)
                                {
                                    // Nếu tìm thấy PtTx nhưng lý do lọt vào danh sách khuyết trước đó là do tài xế chưa check-in làm việc
                                    mappingPhuongTienTaiXeId = mapping.MaPtTx;
                                }
                                else
                                {
                                    // Nếu xe trống hoàn toàn chưa gán cặp với tài xế nào trong bảng phân ca
                                    // Ta chấp nhận tạo bản ghi tạm hoặc chỉ truyền thông tin Xe vào hàm tính chi phí định mức xăng dầu
                                    mappingPhuongTienTaiXeId = null;
                                }

                                // Xóa xe này khỏi danh sách khả dụng để các cụm khuyết phía sau không lấy trùng
                                xeCuaKho.Remove(xePhuHopTaiTrong);
                            }

                            var loTrinhKhuyet = new LoTrinh
                            {
                                TrangThai = "Chờ điều phối thủ công",
                                ThoiGianBatDauKeHoach = DateTime.Now,
                                GhiChu = ghiChuLoTrinh,
                                LoTrinhTuyen = (trangThaiHanhTrinhThucTe == "Chờ trung chuyển"),
                                MaKhoQuanLy = maKhoId,
                                TongKhoiLuongKg = cluster.TongKhoiLuong,
                                MaPtTx = mappingPhuongTienTaiXeId // Có thể có Id cặp PtTx (nhưng tài xế chưa duyệt) hoặc null hoàn toàn
                            };

                            context.LoTrinhs.Add(loTrinhKhuyet);
                            await context.SaveChangesAsync();

                            // 2. Truyền thông tin xe tìm được (nếu có) vào hàm tính toán điểm dừng và định mức chi phí xăng dầu
                            await TaoDiemDungVaChiPhiMoi(
                                scope.ServiceProvider,
                                context,
                                loTrinhKhuyet,
                                khoInfo,
                                new List<QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang.ClusterResult> { cluster },
                                xePhuHopTaiTrong, // Đã truyền vật lý xe vào thay vì để null giúp hàm TaoDiemDung tính toán được định mức (0.15m hoặc 0.22m)
                                trangThaiHanhTrinhThucTe
                            );

                            // 3. Nếu gán xe thành công, khóa trạng thái xe trên hệ thống tránh thất thoát điều phối
                            if (xePhuHopTaiTrong != null)
                            {
                                await phuongTienService.UpdateTrangThaiXeAsync(xePhuHopTaiTrong.MaPhuongTien, new Models12.ThongTinLienServer.PhuongTien.UpdateTrangThaiXeDto { TrangThai = "Chờ khởi hành" });
                            }

                            string trangThaiDonHangMoi = trangThaiHanhTrinhThucTe == "Chờ lấy hàng" ? "Đã lên lộ trình" :
                                                         trangThaiHanhTrinhThucTe == "Chờ trung chuyển" ? "Đang luân chuyển miền" : "Đang giao hàng";

                            await donHangService.CapNhatTrangThaiNhieuDonHangAsync(new QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang.UpdateMultiStatusRequest
                            {
                                DanhSachMaDonHang = cluster.DanhSachMaDonHang.Distinct().ToList(),
                                TrangThaiMoi = trangThaiDonHangMoi
                            });

                            finalProcessedData.Add(new
                            {
                                MaLoTrinh = loTrinhKhuyet.MaLoTrinh,
                                KhoXuatPhat = khoInfo.TenKho,
                                BienSoXe = bienSoXeHienThi,
                                TrangThai = "Chờ điều phối thủ công"
                            });
                        }
                    }
                }

                await transaction.CommitAsync();
                return Ok(new { status = skippedClusters.Any() ? "Warning" : "Success", data = finalProcessedData });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Lỗi Server điều phối: " + ex.Message);
            }
        }

        [NonAction]
        private async Task TaoDiemDungVaChiPhiMoi(
            IServiceProvider serviceProvider,
            TmdtContext context,
            LoTrinh loTrinh,
            KhoTimDuocDto khoInfo,
            List<QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang.ClusterResult> clusters,
            QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.PhuongTien.PhuongTienDTO? xe,
            string trangThaiGom)
        {
            var khoBaiService = serviceProvider.GetRequiredService<IKhoBaiService>();
            var diaChiService = serviceProvider.GetRequiredService<IDiaChiService>();
            var donHangService = serviceProvider.GetRequiredService<IDonHangService>();

            KhoTimDuocDto? resKhoDich = null;
            bool giaoThangTuKhoChinh = false;

            if (trangThaiGom == "Chờ lấy hàng")
            {
                if (khoInfo.LoaiKho == 1 || khoInfo.MaKho == 11 || khoInfo.MaKho == 15 || khoInfo.MaKho == 17)
                {
                    resKhoDich = khoInfo;
                }
                else
                {
                    string mienXuatPhat = GetRegionFromH3(khoInfo.MaVungH3);
                    int maKhoTongDich = TimMaKhoChinhCuaVung(mienXuatPhat);

                    var mapKhoTong = await khoBaiService.TimKhoTheoLoAsync(new BatchKhoRequest { MaDiaChis = new List<int> { 2827, 2831, 2833 } });
                    resKhoDich = mapKhoTong.Values.FirstOrDefault(k => k.MaKho == maKhoTongDich);
                }
            }
            else if (trangThaiGom == "Chờ trung chuyển")
            {
                // 🔥 ĐỒNG BỘ LOGIC: Đọc giá trị miền đích thực tế đã được phân loại chuẩn hóa từ MaVungH3 của sub-cluster
                string mienDichDonHang = clusters.FirstOrDefault()?.MaVungH3 ?? "North";
                int maKhoTrungTamDich = TimMaKhoChinhCuaVung(mienDichDonHang);

                if (khoInfo.MaKho == 11) // Xuất phát từ Tổng Miền Bắc
                {
                    if (mienDichDonHang.Equals("North", StringComparison.OrdinalIgnoreCase))
                    {
                        int maKhoVeTinh = TimMaKhoPhuPhuHop(clusters.FirstOrDefault()?.MaVungH3 ?? "");
                        if (maKhoVeTinh == 0 || maKhoVeTinh == 11)
                        {
                            resKhoDich = khoInfo;
                            giaoThangTuKhoChinh = true;
                        }
                        else
                        {
                            int maDiaChiDich = GetMaDiaChiTuMaKho(maKhoVeTinh);
                            var mapKhoVeTinh = await khoBaiService.TimKhoTheoLoAsync(new BatchKhoRequest { MaDiaChis = new List<int> { maDiaChiDich } });
                            resKhoDich = mapKhoVeTinh.Values.FirstOrDefault();
                        }
                    }
                    else // Các đơn hàng có mienDichDonHang thực tế là "Central" hoặc "South"
                    {
                        // Tuyến đi tiếp chặng nối tiếp: Tổng Bắc (11) -> đi vào Tổng Miền Trung (15) làm kho trung chuyển tiếp theo
                        int maKhoTrungTamKeBen = 15;
                        int maDiaChiKhoKeBen = GetMaDiaChiTuMaKho(maKhoTrungTamKeBen);
                        var mapKhoKeBen = await khoBaiService.TimKhoTheoLoAsync(new BatchKhoRequest { MaDiaChis = new List<int> { maDiaChiKhoKeBen } });
                        resKhoDich = mapKhoKeBen.Values.FirstOrDefault(k => k.MaKho == maKhoTrungTamKeBen);
                    }
                }
                else // Xuất phát từ các Tổng khác (Tổng Miền Trung - 15 hoặc Tổng Miền Nam - 17)
                {
                    if (khoInfo.MaKho != maKhoTrungTamDich)
                    {
                        int maKhoTrungTamKeBen = maKhoTrungTamDich;
                        if (khoInfo.MaKho == 17 && maKhoTrungTamDich == 11)
                        {
                            maKhoTrungTamKeBen = 15; // Phải đi từ Nam (17) -> qua Trung (15) trước khi ra Bắc
                        }

                        int maDiaChiKhoKeBen = GetMaDiaChiTuMaKho(maKhoTrungTamKeBen);
                        var mapKhoKeBen = await khoBaiService.TimKhoTheoLoAsync(new BatchKhoRequest { MaDiaChis = new List<int> { maDiaChiKhoKeBen } });
                        resKhoDich = mapKhoKeBen.Values.FirstOrDefault(k => k.MaKho == maKhoTrungTamKeBen);
                    }
                    else
                    {
                        int maKhoVeTinh = TimMaKhoPhuPhuHop(mienDichDonHang);
                        if (maKhoVeTinh == 0)
                        {
                            resKhoDich = khoInfo;
                            giaoThangTuKhoChinh = true;
                        }
                        else
                        {
                            int maDiaChiDich = GetMaDiaChiTuMaKho(maKhoVeTinh);
                            var mapKhoVeTinh = await khoBaiService.TimKhoTheoLoAsync(new BatchKhoRequest { MaDiaChis = new List<int> { maDiaChiDich } });
                            resKhoDich = mapKhoVeTinh.Values.FirstOrDefault();
                        }
                    }
                }
            }
            else
            {
                resKhoDich = khoInfo;
            }

            var listIds = new List<int> { khoInfo.MaDiaChi };
            var diemDungTrungGianIds = new List<int>();

            if (trangThaiGom == "Chờ lấy hàng")
            {
                diemDungTrungGianIds = clusters.Select(c => c.MaDiaChiLayHang > 0 ? c.MaDiaChiLayHang : c.MaDiaChiCum).Where(id => id > 0).Distinct().ToList();
            }
            // Tách biệt hoàn toàn logic của "Chờ giao hàng" và "Chờ trung chuyển"
            else if (trangThaiGom == "Chờ giao hàng")
            {
                diemDungTrungGianIds = clusters
                    .Select(c => c.MaDiaChiNhanHang > 0 ? c.MaDiaChiNhanHang : c.MaDiaChiCum)
                    .Where(id => id > 0 && id != khoInfo.MaDiaChi)
                    .Distinct()
                    .ToList();
            }
            else if (trangThaiGom == "Chờ trung chuyển")
            {
                // Nghiệp vụ Hub-to-Hub liên miền: Không đi giao lẻ cho khách!
                // Điểm trung gian chỉ xuất hiện nếu lộ trình đi xuyên qua nhiều Kho Tổng (Ví dụ: Từ Tổng Nam 17 -> ghé Tổng Trung 15 -> rồi mới ra Tổng Bắc 11)
                diemDungTrungGianIds = new List<int>();

                if (khoInfo.MaKho == 17 && resKhoDich?.MaKho == 11)
                {
                    // Nếu đi từ Nam ra Bắc, bắt buộc phải chọn Kho Tổng Miền Trung làm điểm dừng trung chuyển dọc đường
                    int maDiaChiKhoTrung = GetMaDiaChiTuMaKho(15);
                    diemDungTrungGianIds.Add(maDiaChiKhoTrung);
                }
            }

            listIds.AddRange(diemDungTrungGianIds);
            if (resKhoDich != null) listIds.Add(resKhoDich.MaDiaChi);

            var uniqueIds = listIds.Distinct().ToList();
            var toaDoData = await diaChiService.GetToaDoDanhSachAsync(uniqueIds);

            if (toaDoData == null || !toaDoData.Any())
                throw new Exception("Không thể lấy dữ liệu tọa độ xác thực từ Microservice Địa chính.");

            var orderedToaDo = new List<QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DiaChi.ToaDoDiaChiResponseDto>();
            foreach (var id in listIds)
            {
                var td = toaDoData.FirstOrDefault(t => t.MaDiaChi == id);
                if (td != null) orderedToaDo.Add(td);
            }

            int n = orderedToaDo.Count;
            if (n < 2) throw new Exception("Dữ liệu chuỗi điểm dừng không hợp lệ. Cần tối thiểu điểm xuất phát và kết thúc.");

            long[,] distanceMatrix = new long[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j) distanceMatrix[i, j] = 0;
                    else
                    {
                        double d = TinhKhoangCach(orderedToaDo[i].ViDo ?? 0, orderedToaDo[i].KinhDo ?? 0, orderedToaDo[j].ViDo ?? 0, orderedToaDo[j].KinhDo ?? 0);
                        distanceMatrix[i, j] = (long)(d * 1.3 * 1000);
                    }
                }
            }

            var path = SolveTSPWithOrTools(distanceMatrix, 0, n - 1);

            double tongKm = 0;
            for (int i = 0; i < path.Count; i++)
            {
                int idx = path[i];
                if (i > 0) tongKm += (distanceMatrix[path[i - 1], idx] / 1.3) / 1000.0;

                string loaiDungStr = "Điểm giao nhận hàng";
                if (idx == 0)
                {
                    loaiDungStr = khoInfo.LoaiKho == 1 ? "Kho Tổng xuất phát" : "Kho vệ tinh xuất phát";
                }
                else if (idx == n - 1)
                {
                    if (resKhoDich != null)
                    {
                        loaiDungStr = giaoThangTuKhoChinh ? "Kho Tổng kết thúc lộ trình trực tiếp" : (resKhoDich.LoaiKho == 1 ? "Kho Tổng đích vùng" : "Kho vệ tinh trung chuyển");
                    }
                }
                else
                {
                    loaiDungStr = (trangThaiGom == "Chờ lấy hàng") ? "Điểm bốc hàng hộ kinh doanh" : "Địa chỉ phân phối cụm dân cư (Giao trực tiếp)";
                }

                await context.DiemDungs.AddAsync(new DiemDung
                {
                    MaLoTrinh = loTrinh.MaLoTrinh,
                    MaDiaChi = orderedToaDo[idx].MaDiaChi,
                    ThuTuDung = i + 1,
                    LoaiDung = loaiDungStr,
                    EtaKeHoach = DateTime.Now.AddMinutes(30 + (i * 40))
                });
            }

            var tatCaMaDonHang = clusters.SelectMany(c => c.DanhSachMaDonHang).Distinct().ToList();
            foreach (var maDH in tatCaMaDonHang)
            {
                await context.ChiTietLoTrinhKienHangs.AddAsync(new ChiTietLoTrinhKienHang
                {
                    MaLoTrinh = loTrinh.MaLoTrinh,
                    MaDonHang = maDH,
                    TrangThaiTrenXe = (trangThaiGom == "Chờ lấy hàng") ? "Chờ bốc xếp" : "Đã xếp góc thùng xe"
                });
            }

            decimal giaXang = await GetCurrentFuelPriceAsync("DO");
            decimal dinhMuc = (xe?.TaiTrongToiDaKg > 5000) ? 0.22m : 0.15m;

            await context.ChiPhiLoTrinhs.AddAsync(new ChiPhiLoTrinh
            {
                MaLoTrinh = loTrinh.MaLoTrinh,
                SoTien = Math.Round((decimal)tongKm * dinhMuc * giaXang, 0),
                LoaiChiPhi = "XANG_DAU",
                GhiChu = giaoThangTuKhoChinh
                    ? $"Tuyến giao thẳng từ Kho Chính: {khoInfo.TenKho} -> Phân phối trực tiếp người dùng. Quãng đường: {Math.Round(tongKm, 1)} km."
                    : (resKhoDich != null ? $"Tuyến chặng [{trangThaiGom}]: {khoInfo.TenKho} -> {resKhoDich.TenKho}. Quãng đường tính toán: {Math.Round(tongKm, 1)} km." : "Lộ trình không xác định kho đích.")
            });

            await context.SaveChangesAsync();
        }

        [NonAction]
        public int xacDinhMaCaTheoGio(DateTime checkTime)
        {
            var currentTime = TimeOnly.FromDateTime(checkTime);
            var danhSachCa = new List<CaTrucConfig>
            {
                new() { MaCa = 1, TenCa = "Ca Sáng tiêu chuẩn", GioBatDau = new TimeOnly(6, 0), GioKetThuc = new TimeOnly(14, 0), Priority = 1 },
                new() { MaCa = 2, TenCa = "Ca Chiều tiêu chuẩn", GioBatDau = new TimeOnly(14, 0), GioKetThuc = new TimeOnly(22, 0), Priority = 1 },
                new() { MaCa = 3, TenCa = "Ca Đêm tiêu chuẩn", GioBatDau = new TimeOnly(22, 0), GioKetThuc = new TimeOnly(6, 0), Priority = 1 },
                new() { MaCa = 8, TenCa = "Ca Chuyến dài (Linh hoạt 24h)", GioBatDau = new TimeOnly(0, 0), GioKetThuc = new TimeOnly(23, 59, 59), Priority = 4 }
            };

            var matches = danhSachCa.Where(ca => {
                if (ca.GioBatDau < ca.GioKetThuc)
                    return currentTime >= ca.GioBatDau && currentTime <= ca.GioKetThuc;
                else
                    return currentTime >= ca.GioBatDau || currentTime <= ca.GioKetThuc;
            })
            .OrderBy(ca => ca.Priority)
            .ThenBy(ca => {
                var duration = ca.GioKetThuc.ToTimeSpan() - ca.GioBatDau.ToTimeSpan();
                return duration.Ticks < 0 ? duration.Add(TimeSpan.FromDays(1)) : duration;
            }).ToList();

            return matches.FirstOrDefault()?.MaCa ?? 8;
        }

        [NonAction]
        private List<int> SolveTSPWithOrTools(long[,] distanceMatrix, int startIndex, int endIndex)
        {
            int numLocations = distanceMatrix.GetLength(0);
            if (numLocations <= 1) return new List<int> { 0 };

            RoutingIndexManager manager = new RoutingIndexManager(numLocations, 1, new int[] { startIndex }, new int[] { endIndex });
            RoutingModel routing = new RoutingModel(manager);

            int transitCallbackIndex = routing.RegisterTransitCallback((long fromIndex, long toIndex) =>
            {
                var fromNode = manager.IndexToNode(fromIndex);
                var toNode = manager.IndexToNode(toIndex);
                return distanceMatrix[fromNode, toNode];
            });

            routing.SetArcCostEvaluatorOfAllVehicles(transitCallbackIndex);

            Assignment solution = routing.Solve();
            var path = new List<int>();

            if (solution != null)
            {
                long index = routing.Start(0);
                while (!routing.IsEnd(index))
                {
                    path.Add(manager.IndexToNode((int)index));
                    index = solution.Value(routing.NextVar(index));
                }
                path.Add(manager.IndexToNode((int)index));
            }
            return path;
        }

        // ==========================================
        // Helper Mapper - Cập nhật dữ liệu từ bảng mới
        // ==========================================
        [NonAction]
        public int GetMaDiaChiTuMaKho(int maKho)
        {
            return maKho switch
            {
                11 => 2827, // Kho Tổng Miền Bắc
                12 => 2828, // Kho Cảng Hải Phòng
                13 => 2829, // Kho Trung chuyển Bắc Giang
                14 => 2830, // Kho Tiếp vận Nghệ An
                15 => 2831, // Kho Tổng Miền Trung
                16 => 2832, // Kho Cao Nguyên Đắk Lắk
                17 => 2833, // Kho Tổng Miền Nam
                18 => 2835, // Kho Công nghiệp Bình Dương
                19 => 2836, // Kho Cảng Cái Mép
                _ => 2800 + maKho // Fallback về thuật toán cũ nếu dính mã lạ ngoài dataset
            };
        }

        
        private Dictionary<PhuongTienDTO, List<Models12.DieuPhoiLoTrinh.ClusterResult>> ApplyFirstFitDecreasingBPP(List<Models12.DieuPhoiLoTrinh.ClusterResult> clusters, List<PhuongTienDTO> vehicles)
        {
            var assignments = new Dictionary<PhuongTienDTO, List<Models12.DieuPhoiLoTrinh.ClusterResult>>();
            var sortedClusters = clusters.OrderByDescending(c => c.TongKhoiLuong).ToList();

            foreach (var cluster in sortedClusters)
            {
                foreach (var xe in vehicles)
                {
                    if (!assignments.ContainsKey(xe)) assignments[xe] = new List<Models12.DieuPhoiLoTrinh.ClusterResult>();

                    var load = assignments[xe].Sum(c => c.TongKhoiLuong);
                    if (load + cluster.TongKhoiLuong <= (double)xe.TaiTrongToiDaKg)
                    {
                        assignments[xe].Add(cluster);
                        break;
                    }
                }
            }
            return assignments.Where(a => a.Value.Any()).ToDictionary(a => a.Key, a => a.Value);
        }

        private double TinhKhoangCach(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371;
            double dLat = (Math.PI / 180) * (lat2 - lat1);
            double dLon = (Math.PI / 180) * (lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos((Math.PI / 180) * lat1) * Math.Cos((Math.PI / 180) * lat2) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private string GetRegionFromH3(string h3Index)
        {
            if (string.IsNullOrEmpty(h3Index)) return "Unknown";
            if (h3Index.StartsWith("886")) return "South";
            if (h3Index.StartsWith("87") || h3Index.StartsWith("882")) return "North";
            if (h3Index.StartsWith("887")) return "Central";
            return "North";
        }

        private int TimMaKhoPhuPhuHop(string destinationH3) => 10;
        
        private int TimMaKhoChinhCuaVung(string region)
        {
            if (string.IsNullOrWhiteSpace(region)) return 11; // Mặc định trả về Kho miền Bắc nếu chuỗi rỗng

            return region.Trim().ToUpper() switch
            {
                "NORTH" or "BAC" or "MIEN BAC" => 11,
                "CENTRAL" or "TRUNG" or "MIEN TRUNG" => 15,
                "SOUTH" or "NAM" or "MIEN NAM" => 17,
                _ => 11 // Giá trị dự phòng (Fallback) nếu không khớp miền nào
            };
        }

        private async Task<decimal> GetCurrentFuelPriceAsync(string fuelType) => fuelType == "DO" ? 21500m : 23000m;
        [NonAction]
        private int PhanTichKhoTuDiaChi(int maDiaChi)
        {
            // Logic phân tách nghiệp vụ thực tế dựa trên dải ID địa chính của hệ thống bạn
            // Ví dụ: Nhóm địa chỉ miền Bắc từ 2801 -> 2814, Miền Trung 2815 -> 2824, Miền Nam >= 2825
            if (maDiaChi >= 2800 && maDiaChi <= 2814) return 11; // Mã Kho Tổng Miền Bắc
            if (maDiaChi >= 2815 && maDiaChi <= 2824) return 15; // Mã Kho Tổng Miền Trung
            if (maDiaChi >= 2825) return 17;                     // Mã Kho Tổng Miền Nam

            return 11; // Mặc định trả về Kho chính nếu không nằm trong dải quét
        }
        // 2. Thêm 2 hàm Mock (hoặc viết logic gọi HttpClient) ở cuối Class để không bị lỗi build
        private async Task<Dictionary<int, string>> LayThongTinNguoiDungBulk(List<int> userIds)
        {
            // Đây là hàm mẫu, bạn có thể thay thế bằng logic gọi sang Microservice Nhân Viên
            return await Task.FromResult(new Dictionary<int, string>());
        }

        private async Task<Dictionary<int, string>> LayThongTinPhuongTienBulk(List<int> vehicleIds)
        {
            // Đây là hàm mẫu, bạn có thể thay thế bằng logic gọi sang Microservice Phương Tiện
            return await Task.FromResult(new Dictionary<int, string>());
        }
        /// <summary>
        /// API 1: Lấy danh sách lộ trình có trạng thái "Chờ điều phối thủ công" công kèm thông tin liên server
        /// URL: GET /api/dieuphoilotrinh/cho-dieu-phoi-thu-cong
        /// <summary>
        /// API: Lấy danh sách lộ trình phục vụ giao diện điều phối trực quan
        /// </summary>
        [HttpGet("cho-dieu-phoi-thu-cong")]
        public async Task<IActionResult> GetDanhSachChoDieuPhoiThuCong(
        [FromQuery] DateTime? ngayDieuPhoi,
        [FromQuery] int? maKhoQuanLy, // 1. Thêm tham số lọc theo mã kho ở đây
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        {
            try
            {
                string trangThaiTarget = "Chờ điều phối thủ công";
                var query = _context.LoTrinhs.AsQueryable().Where(lt => lt.TrangThai == trangThaiTarget);

                // Lọc theo ngày điều phối (nếu có)
                if (ngayDieuPhoi.HasValue)
                {
                    query = query.Where(lt => lt.ThoiGianBatDauKeHoach >= ngayDieuPhoi.Value.Date
                                       && lt.ThoiGianBatDauKeHoach <= ngayDieuPhoi.Value.Date.AddDays(1).AddTicks(-1));
                }

                // 2. BỔ SUNG: Lọc theo mã kho quản lý (nếu có truyền vào)
                if (maKhoQuanLy.HasValue)
                {
                    query = query.Where(lt => lt.MaKhoQuanLy == maKhoQuanLy.Value);
                }

                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                var rawData = await query
                    .OrderByDescending(tg => tg.ThoiGianBatDauKeHoach)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(sc => new
                    {
                        sc.MaLoTrinh,
                        sc.ThoiGianBatDauKeHoach,
                        sc.TrangThai,
                        sc.GhiChu,
                        sc.MaKhoQuanLy,
                        // Lấy thêm trường tổng khối lượng từ bảng LoTrinh phục vụ tính toán DTO
                        TongKhoiLuongKg = sc.TongKhoiLuongKg,
                        MaNguoiDung = sc.MaPtTxNavigation != null ? (int?)sc.MaPtTxNavigation.MaNguoiDung : null,
                        MaPhuongTien = sc.MaPtTxNavigation != null ? (int?)sc.MaPtTxNavigation.MaPhuongTien : null,
                        TongSoDonHang = sc.ChiTietLoTrinhKienHangs.Count(),
                        TongSoDiemDung = sc.DiemDungs.Count(),
                        TenLoaiHang = "Hàng hóa tổng hợp", // Gán cứng tạm thời hoặc tính toán dựa trên danh sách kiện flat                                      
                        TenDiemDen = sc.DiemDungs.OrderBy(d => d.MaDiemDung).Select(d => d.LoaiDung).LastOrDefault() ?? "Điểm đích"
                    })
                    .ToListAsync();

                // Chuyển đổi sang Model chính thức để mapping liên server
                var data = rawData.Select(sc => {
                    // Quy đổi Kg sang Tấn để phục vụ logic phân loại xe hiển thị yêu cầu
                    double khoiLuongTan = (sc.TongKhoiLuongKg ?? 0) / 1000.0;

                    return new LoTrinhDieuPhoiThuCongModels
                    {
                        MaLoTrinh = sc.MaLoTrinh,
                        ThoiGianBatDauKeHoach = sc.ThoiGianBatDauKeHoach,
                        TrangThai = sc.TrangThai,
                        MaNguoiDung = sc.MaNguoiDung,
                        MaPhuongTien = sc.MaPhuongTien,
                        TongSoDonHang = sc.TongSoDonHang,
                        TongSoDiemDung = sc.TongSoDiemDung,
                        TenLoaiHang = sc.TenLoaiHang,
                        MaKho = sc.MaKhoQuanLy,

                        // HOÀN THÀNH: Gán khối lượng hiển thị dạng double (đơn vị gốc Kg)
                        KhoiLuongHienThi = sc.TongKhoiLuongKg ?? 0,

                        // Tuyến đi bắt đầu bằng Kho quản lý (Sẽ map lại tên kho chính xác ở vòng lặp for phía dưới)
                        KhoXuatPhat = "Đang tải...",
                        TuyenDuongHienThi = $"Kho Tổng -> {sc.TenDiemDen}",

                        // HOÀN THÀNH: Đã sửa điều kiện phân loại xe dựa trên khối lượng tấn quy đổi từ Kg
                        YeuCauXe = khoiLuongTan > 5 ? "Tải bạt 8T" : (khoiLuongTan > 2 ? "Tải thùng 3.5T" : "Van 1.5T")
                    };
                }).ToList();

                if (data.Any())
                {
                    // 1. Gom các ID để gọi liên Microservice
                    var userIds = data.Where(x => x.MaNguoiDung.HasValue).Select(x => x.MaNguoiDung.Value).Distinct().ToList();
                    var vehicleIds = data.Where(x => x.MaPhuongTien.HasValue).Select(x => x.MaPhuongTien.Value).Distinct().ToList();

                    var usersTask = userIds.Any() ? LayThongTinNguoiDungBulk(userIds) : Task.FromResult(new Dictionary<int, string>());
                    var vehiclesTask = vehicleIds.Any() ? LayThongTinPhuongTienBulk(vehicleIds) : Task.FromResult(new Dictionary<int, string>());

                    await Task.WhenAll(usersTask, vehiclesTask);

                    var userDict = usersTask.Result;
                    var vehicleDict = vehiclesTask.Result;

                    // 2. Map dữ liệu trả về và xử lý cứu cánh thông tin hiển thị trực quan
                    for (int i = 0; i < data.Count; i++)
                    {
                        var item = data[i];
                        var rawItem = rawData[i];

                        // Đổ tên tài xế thực tế nếu đã gán thành công
                        if (item.MaNguoiDung.HasValue && userDict.ContainsKey(item.MaNguoiDung.Value))
                            item.TenTaiXeThucHien = userDict[item.MaNguoiDung.Value];

                        // Đổ biển số xe thực tế
                        if (item.MaPhuongTien.HasValue && vehicleDict.ContainsKey(item.MaPhuongTien.Value))
                        {
                            item.BienSoXe = vehicleDict[item.MaPhuongTien.Value];
                        }
                        else
                        {
                            // THỦ THUẬT: Nếu MaPtTx đang null (Chờ duyệt tay) nhưng trong Ghi Chú hệ thống tự động
                            // có ghi vết "Đã gán xe [Biển Số]", bóc tách ra để hiển thị dạng gợi ý lên UI
                            item.BienSoXe = BocTachBienSoTuGhiChu(rawItem.GhiChu);
                        }

                        // Cập nhật lại Tên Tuyến đường hoàn chỉnh
                        string tenKhoGoc = rawItem.MaKhoQuanLy == 1 ? "Kho Tổng HN" : "Kho Tổng Miền Trung";
                        item.KhoXuatPhat = tenKhoGoc;
                        item.TuyenDuongHienThi = $"{tenKhoGoc} → {rawItem.TenDiemDen}";
                    }
                }

                return Ok(new { TotalItems = totalItems, TotalPages = totalPages, CurrentPage = page, Data = data });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi GetDanhSachChoDieuPhoiThuCong: {ex.Message}");
                return StatusCode(500, new { error = "Lỗi hệ thống", detail = ex.Message });
            }
        }

        // Hàm bổ trợ bóc tách biển số xe từ chuỗi text Ghi chú hệ thống tự động gom chặng sinh ra
        private string BocTachBienSoTuGhiChu(string ghiChu)
        {
            if (string.IsNullOrEmpty(ghiChu)) return null;
            try
            {
                // Tìm định dạng biển số xe cứu cánh (Ví dụ chứa cụm: xe 51C-66677 hoặc xe 29C-888.99)
                if (ghiChu.Contains("xe "))
                {
                    int index = ghiChu.IndexOf("xe ") + 3;
                    string sub = ghiChu.Substring(index).Trim();
                    string[] words = sub.Split(' ');
                    if (words.Length > 0) return words[0].Replace("(", "").Replace(")", "");
                }
            }
            catch { }
            return null; // Trả về null nếu không bóc tách được text
        }
        /// <summary>
        /// API 2: Xử lý nút "Lưu Lệnh" từ Modal - Gán phương tiện tài xế và chuyển trạng thái lộ trình
        /// URL: POST /api/dieuphoilotrinh/luu-dieu-phoi-thu-cong
        /// </summary>
        [HttpPost("luu-dieu-phoi-thu-cong")]
        public async Task<IActionResult> LuuDieuPhoiThuCong([FromBody] LuuDieuPhoiThuCongDto dto)
        {
            if (dto == null || dto.MaLoTrinh <= 0 || dto.MaPhuongTien <= 0 || dto.MaTaiXeChinh <= 0)
            {
                return BadRequest(new { success = false, message = "Dữ liệu điều phối đầu vào không hợp lệ." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Kiểm tra sự tồn tại của Lộ trình
                    var loTrinh = await _context.LoTrinhs
                        .FirstOrDefaultAsync(lt => lt.MaLoTrinh == dto.MaLoTrinh);

                    if (loTrinh == null)
                        return NotFound(new { success = false, message = $"Không tìm thấy lộ trình mã {dto.MaLoTrinh}" });

                    // 2. Tạo hoặc Cập nhật bảng trung gian PhuongTienTaiXe (MaPtTxNavigation)
                    // Kiểm tra xem cặp Xe-Tài xế này đã tồn tại cấu hình Active chưa
                    var phuongTienTaiXe = await _context.PhuongTienTaiXes
                        .FirstOrDefaultAsync(x => x.MaPhuongTien == dto.MaPhuongTien
                                             && x.MaNguoiDung == dto.MaTaiXeChinh
                                             && x.IsActive == true);

                    if (phuongTienTaiXe == null)
                    {
                        // Nếu chưa có thì thêm mới bản ghi liên kết phương tiện tài xế vào db nội bộ
                        phuongTienTaiXe = new PhuongTienTaiXe
                        {
                            MaPhuongTien = dto.MaPhuongTien,
                            MaNguoiDung = dto.MaTaiXeChinh,
                            MaNguoiDungPhu = dto.MaTaiXePhu,
                            IsActive = true,
                           
                        };
                        _context.PhuongTienTaiXes.Add(phuongTienTaiXe);
                        await _context.SaveChangesAsync(); // Lưu để sinh MaPtTx
                    }
                    else
                    {
                        // Nếu có rồi thì cập nhật thêm tài xế phụ hoặc ghi chú mới (nếu có thay đổi)
                        if (dto.MaTaiXePhu.HasValue) phuongTienTaiXe.MaNguoiDungPhu = dto.MaTaiXePhu;                       
                        _context.PhuongTienTaiXes.Update(phuongTienTaiXe);
                    }

                    // 3. Cập nhật thông tin vào Lộ Trình và Chuyển trạng thái sang "Đã gán" hoặc "Chờ khởi hành"
                    loTrinh.MaPtTx = phuongTienTaiXe.MaPtTx;
                    loTrinh.TrangThai = "Chờ khởi hành"; // Chuyển trạng thái sau khi đã gán xe thành công

                    _context.LoTrinhs.Update(loTrinh);
                    await _context.SaveChangesAsync();

                    // 4. Commit Transaction
                    await transaction.CommitAsync();

                    // 5. Xóa Cache cũ để giao diện được cập nhật mới lập tức
                    _resetCacheSignal.Cancel();
                    _resetCacheSignal = new CancellationTokenSource();

                    return Ok(new { success = true, message = "Lưu lệnh điều phối phương tiện thành công!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Lỗi tại LuuDieuPhoiThuCong: {ex.Message}");
                    return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi lưu lệnh", detail = ex.Message });
                }
            }
        }
    }



    // CHỈ GIỮ LẠI Request đầu vào duy nhất cho API endpoint này
    public class DieuPhoiRequest
    {
        public string? TrangThaiDonHang { get; set; }
        public int Limit { get; set; }
    }
    public class DynamicOrderLocationDto
    {
        public int MaDonHang { get; set; }
        public int MaKhoHienTai { get; set; }
        public string MienDich { get; set; } = string.Empty; // Tránh null miên dịch
        public int MaDiaChiNhanHangThucTe { get; set; }
    }
    public class LuuDieuPhoiThuCongDto
    {
        public int MaLoTrinh { get; set; }
        public int MaPhuongTien { get; set; }
        public int MaTaiXeChinh { get; set; }
        public int? MaTaiXePhu { get; set; } // Có thể null
        public string? GhiChu { get; set; }
    }

}