using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyLoTrinhTheoDoi;
using QuanLyLoTrinhTheoDoi.Models;
using QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

public class RoutingOrderConsumer : BackgroundService
{
    private readonly ILogger<RoutingOrderConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private IConnection? _connection;
    private IChannel? _channel;
    private const string QueueName = "order_queue";

    public RoutingOrderConsumer(
        ILogger<RoutingOrderConsumer> logger,
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
    }

    private async Task InitRabbitMQAsync()
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _logger.LogInformation("--- Đã kết nối RabbitMQ v7 (Routing Server) ---");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitRabbitMQAsync();
        if (_channel == null) return;

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var messageJson = Encoding.UTF8.GetString(body);

            try
            {
                var orderData = JsonSerializer.Deserialize<RoutingOrderMessage>(messageJson);
                if (orderData != null)
                {
                    _logger.LogInformation($"[Routing Server] Bắt đầu xử lý lộ trình đơn: {orderData.MaDonHang}");
                    await ProcessRoutingLogic(orderData);
                    await VanVhuyenNguyenChuyen(orderData);
                }

                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi xử lý tin nhắn: {ex.Message}");
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }


    private async Task ProcessRoutingLogic(RoutingOrderMessage data)
    {
        using var scope = _serviceProvider.CreateScope();
        var _context = scope.ServiceProvider.GetRequiredService<TmdtContext>();
        var clientDH = _httpClientFactory.CreateClient("DonHangApi");
        var clientKho = _httpClientFactory.CreateClient("KhoApi");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Lấy chi tiết đơn hàng để có MaDiaChiNhanHang và MaVungH3Giao
            var infoDH = await clientDH.GetFromJsonAsync<RoutingOrderMessage>($"api/quanlydonhang/thongtindonhang/{data.MaDonHang}");

            if (infoDH == null)
                throw new Exception($"Không tìm thấy đơn hàng {data.MaDonHang}");

            // 2. GỌI API MỚI: Lấy MaDiaChi từ MaKho (data.MaKhoVao)
            int? maKhoId = data.MaKhoVao;
            int maDiaChiKhoTiepNhan = 0;

            var resMaDiaChi = await clientKho.GetAsync($"api/quanlykhobai/MaDiaChiKho/{maKhoId}");
            if (resMaDiaChi.IsSuccessStatusCode)
            {
                // Đọc dưới dạng JsonElement
                var result = await resMaDiaChi.Content.ReadFromJsonAsync<JsonElement>();

                // Sử dụng GetProperty để lấy giá trị (lưu ý viết đúng hoa thường như API trả về)
                if (result.TryGetProperty("maDiaChi", out var property))
                {
                    maDiaChiKhoTiepNhan = property.GetInt32();
                }
                else if (result.TryGetProperty("MaDiaChi", out var propertyUpper))
                {
                    maDiaChiKhoTiepNhan = propertyUpper.GetInt32();
                }
            }

            int? maDich = infoDH.MaDiaChiNhanHang;

            _logger.LogInformation($"[Check] Đơn {data.MaDonHang}: MaKhoVao={maKhoId} -> MaDiaChiKho={maDiaChiKhoTiepNhan}, Dich={maDich}");

            if (maDich == 0 || maDiaChiKhoTiepNhan == 0)
                throw new Exception("Dữ liệu đầu vào không hợp lệ: Thiếu Địa chỉ kho hoặc Địa chỉ đích.");

            // 3. TẠO LỘ TRÌNH MỚI
            var loTrinhMoi = new LoTrinh
            {
                TrangThai = "Đang vận chuyển",
                ThoiGianBatDauKeHoach = DateTime.Now,
                ThoiGianBatDauThucTe = DateTime.Now,
                GhiChu = $"Lộ trình tự động cho đơn {data.MaDonHang}",
                MaKhoQuanLy = maKhoId, // Lưu mã kho vào quản lý
                MaTaiXeChinh = null,
                MaPhuongTien = null
            };

            _context.LoTrinhs.Add(loTrinhMoi);
            await _context.SaveChangesAsync();

            int thuTu = 1;

            // ĐIỂM 1: Kho tiếp nhận (Dùng mã địa chỉ vừa lấy được)
            _context.DiemDungs.Add(new DiemDung
            {
                MaLoTrinh = loTrinhMoi.MaLoTrinh,
                MaDiaChi = maDiaChiKhoTiepNhan,
                ThuTuDung = thuTu++,
                LoaiDung = "Kho tiếp nhận",
                EtaKeHoach = DateTime.Now
            });

            // ĐIỂM 2: Kho trung chuyển
            try
            {
                // 1. Tìm kho gần đích nhất (Trả về maKho = 13)
                var resKhoGanDich = await clientKho.GetFromJsonAsync<KhoGanNhatResponse>(
                    $"api/quanlykhobai/tim-kho-gan-nhat?maDiaChi={maDich}");

                if (resKhoGanDich != null && resKhoGanDich.MaKho > 0)
                {
                    int maDiaChiKhoTrungChuyen = 0;

                    // 2. Gọi API để đổi từ MaKho (13) sang MaDiaChi
                    var resMaDiaChi01 = await clientKho.GetAsync($"api/quanlykhobai/MaDiaChiKho/{resKhoGanDich.MaKho}");

                    if (resMaDiaChi01.IsSuccessStatusCode)
                    {
                        var result = await resMaDiaChi01.Content.ReadFromJsonAsync<JsonElement>();

                        if (result.TryGetProperty("maDiaChi", out var property))
                            maDiaChiKhoTrungChuyen = property.GetInt32();
                        else if (result.TryGetProperty("MaDiaChi", out var propertyUpper))
                            maDiaChiKhoTrungChuyen = propertyUpper.GetInt32();
                    }

                    // 3. Kiểm tra nếu mã địa chỉ này khác với kho tiếp nhận thì mới thêm vào lộ trình
                    if (maDiaChiKhoTrungChuyen > 0 && maDiaChiKhoTrungChuyen != maDiaChiKhoTiepNhan)
                    {
                        _context.DiemDungs.Add(new DiemDung
                        {
                            MaLoTrinh = loTrinhMoi.MaLoTrinh,
                            MaDiaChi = maDiaChiKhoTrungChuyen, // Bây giờ giá trị sẽ là mã địa chỉ thật (ví dụ 2811)
                            ThuTuDung = thuTu++,
                            LoaiDung = "Kho trung chuyển",
                            EtaKeHoach = DateTime.Now.AddHours(3)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Không tìm được kho trung chuyển: {ex.Message}");
            }

            // ĐIỂM 3: Địa chỉ khách hàng
            _context.DiemDungs.Add(new DiemDung
            {
                MaLoTrinh = loTrinhMoi.MaLoTrinh,
                MaDiaChi = maDich ?? 0,
                ThuTuDung = thuTu++,
                LoaiDung = "Địa chỉ khách hàng",
                EtaKeHoach = DateTime.Now.AddHours(6),
                MaVungH3 = infoDH.MaVungH3Giao
            });

            // 4. GHI LOG LỊCH SỬ
            _context.LichSuHanhTrinhDonHangs.Add(new LichSuHanhTrinhDonHang
            {
                MaDonHang = data.MaDonHang,
                MaLoTrinh = loTrinhMoi.MaLoTrinh,
                MaKho = maKhoId,
                TrangThai = "Đang vận chuyển",
                ThoiGianCapNhat = DateTime.Now,
                ViTriHienTai = $"Đã rời kho tiếp nhận (Mã kho: {maKhoId})",
                GhiChu = "Đã lập lộ trình vận chuyển tự động."
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogInformation($"[Success] Đã lập xong lộ trình cho đơn {data.MaDonHang}");
        }
        catch (Exception ex)
        {
            
            var errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            _logger.LogError($"[Routing Error] Đơn {data.MaDonHang}: {errorMsg}");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync();
        if (_connection != null) await _connection.CloseAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task VanVhuyenNguyenChuyen(RoutingOrderMessage data)
    {
        using var scope = _serviceProvider.CreateScope();
        var _context = scope.ServiceProvider.GetRequiredService<TmdtContext>();
        var clientDH = _httpClientFactory.CreateClient("DonHangApi");
        var clientKho = _httpClientFactory.CreateClient("KhoApi");
        var clientPT = _httpClientFactory.CreateClient("PhuongTienApi");
        var clientNS = _httpClientFactory.CreateClient("NhanSuApi");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Lấy chi tiết đơn hàng (để biết có phải giao thẳng/hỏa tốc không)
            var infoDH = await clientDH.GetFromJsonAsync<RoutingOrderMessage>($"api/quanlydonhang/thongtindonhang/{data.MaDonHang}");
            if (infoDH == null) throw new Exception($"Không tìm thấy đơn hàng {data.MaDonHang}");

            // 2. Lấy mã địa chỉ của kho tiếp nhận
            int maKhoId = data.MaKhoVao ?? 0;
            int maDiaChiKhoTiepNhan = 0;
            var resMaDiaChi = await clientKho.GetAsync($"api/quanlykhobai/MaDiaChiKho/{maKhoId}");
            if (resMaDiaChi.IsSuccessStatusCode)
            {
                var result = await resMaDiaChi.Content.ReadFromJsonAsync<JsonElement>();
                maDiaChiKhoTiepNhan = result.TryGetProperty("maDiaChi", out var p) ? p.GetInt32() : result.GetProperty("MaDiaChi").GetInt32();
            }

            // 3. TÌM TÀI NGUYÊN (XE & 2 TÀI XẾ) CHO GIAO THẲNG
            var xeSanSang = await clientPT.GetFromJsonAsync<List<VehicleFreeDto>>($"api/quanlyxe/xe-san-sang-dieu-phoi?maKho={maKhoId}");
            var txSanSang = await clientNS.GetFromJsonAsync<List<DriverAvailableDto>>($"api/quanlytaixe/lich-trinh-theo-chuyen?maKho={maKhoId}");

            if (xeSanSang == null || !xeSanSang.Any() || txSanSang == null || txSanSang.Count < 2)
            {
                _logger.LogWarning($"[Routing] Đơn {data.MaDonHang} chờ điều phối thủ công do thiếu xe/tài xế tại kho {maKhoId}");
                // Có thể đẩy vào một queue chờ hoặc đánh dấu đơn hàng cần điều phối tay
                return;
            }

            var xeChon = xeSanSang.First();
            var laiChinh = txSanSang.OrderByDescending(t => t.DiemUyTin).First();
            var laiPhu = txSanSang.Where(t => t.MaNguoiDung != laiChinh.MaNguoiDung).First();

            // 4. TẠO LỘ TRÌNH GIAO THẲNG (Gán trực tiếp tài nguyên)
            var loTrinhMoi = new LoTrinh
            {
                MaPhuongTien = xeChon.MaPhuongTien,
                MaTaiXeChinh = laiChinh.MaNguoiDung,
                MaTaiXePhu = laiPhu.MaNguoiDung, // Lưu lái phụ cho giao thẳng
                TrangThai = "Đang vận chuyển",
                ThoiGianBatDauKeHoach = DateTime.Now,
                GhiChu = $"Giao thẳng tự động từ RabbitMQ - Đơn {data.MaDonHang}",
                MaKhoQuanLy = maKhoId,
                LoTrinhTuyen = false // False = Giao thẳng
            };

            _context.LoTrinhs.Add(loTrinhMoi);
            await _context.SaveChangesAsync();

            // 5. THIẾT LẬP ĐIỂM DỪNG (KHO -> NƠI LẤY HÀNG -> ĐỊA CHỈ GIAO)
            int thuTu = 1;

            // Điểm 1: Kho xuất phát (Nơi xe và tài xế đang đỗ)
            _context.DiemDungs.Add(new DiemDung
            {
                MaLoTrinh = loTrinhMoi.MaLoTrinh,
                MaDiaChi = maDiaChiKhoTiepNhan,
                ThuTuDung = thuTu++,
                LoaiDung = "Kho xuất phát",
                EtaKeHoach = DateTime.Now
            });

            // Điểm 2: NƠI LẤY HÀNG (Địa chỉ của người gửi) - MỚI THÊM
            _context.DiemDungs.Add(new DiemDung
            {
                MaLoTrinh = loTrinhMoi.MaLoTrinh,
                MaDiaChi =(int) infoDH.MaDiaChiLayHang, // Lấy từ thông tin đơn hàng gửi qua
                ThuTuDung = thuTu++,
                LoaiDung = "Điểm lấy hàng",
                EtaKeHoach = DateTime.Now.AddMinutes(30) // Dự kiến 30p sau có mặt lấy hàng
            });

            // Điểm 3: Địa chỉ khách hàng (Điểm giao cuối cùng)
            _context.DiemDungs.Add(new DiemDung
            {
                MaLoTrinh = loTrinhMoi.MaLoTrinh,
                MaDiaChi = infoDH.MaDiaChiNhanHang ?? 0,
                ThuTuDung = thuTu++,
                LoaiDung = "Địa chỉ khách hàng",
                MaVungH3 = infoDH.MaVungH3Giao,
                EtaKeHoach = DateTime.Now.AddHours(2)
            });

            // 6. CẬP NHẬT TRẠNG THÁI LIÊN SERVICE (Đồng bộ hóa các Microservices)
            var updateTasks = new List<Task>
            {
                clientPT.PostAsJsonAsync($"api/quanlyxe/cap-nhat-trang-thai-xe/{xeChon.MaPhuongTien}", new { TrangThai = "Đang hoạt động" }),
                clientNS.PostAsJsonAsync("api/quanlytaixe/cap-nhat-trang-thai", new { MaNguoiDung = laiChinh.MaNguoiDung, TrangThaiMoi = "Đang hoạt động" }),
                clientNS.PostAsJsonAsync("api/quanlytaixe/cap-nhat-trang-thai", new { MaNguoiDung = laiPhu.MaNguoiDung, TrangThaiMoi = "Đang hoạt động" }),
                clientDH.PutAsJsonAsync("api/quanlydonhang/cap-nhat-trang-thai-nhieu", new {
                    DanhSachMaDonHang = new List<int> { data.MaDonHang },
                    TrangThaiMoi = "Đã điều phối - Giao thẳng"
                })
            };
            await Task.WhenAll(updateTasks);

            // 7. GHI LOG LỊCH SỬ
            _context.LichSuHanhTrinhDonHangs.Add(new LichSuHanhTrinhDonHang
            {
                MaDonHang = data.MaDonHang,
                MaLoTrinh = loTrinhMoi.MaLoTrinh,
                MaKho = maKhoId,
                TrangThai = "Đã khởi hành",
                ThoiGianCapNhat = DateTime.Now,
                ViTriHienTai = $"Xe {xeChon.BienSo} đã rời kho {maKhoId}",
                GhiChu = $"Lái chính: {laiChinh.HoTen}, Lái phụ: {laiPhu.HoTen}"
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogInformation($"[Success] Đã lập xong lộ trình GIAO THẲNG cho đơn {data.MaDonHang}");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError($"[Routing Error] Lỗi xử lý đơn {data.MaDonHang}: {ex.Message}");
            throw; // Throw để RabbitMQ thực hiện Nack/Requeue nếu cần
        }
    }
}