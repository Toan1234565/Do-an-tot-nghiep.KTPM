using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyLoTrinhTheoDoi.Models;
using QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace QuanLyLoTrinhTheoDoi
{
    public class OrderTaskConsumer : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OrderTaskConsumer> _logger;
        private readonly IServiceProvider _serviceProvider;

        // Cấu hình
        private const string QueueName = "order_queue";
        private readonly int _maxBatchSize = 20;
        private readonly int _maxWaitTimeMs = 30000;

        // Quản lý trạng thái
        private readonly List<(OrderMessageDto Order, ulong DeliveryTag)> _buffer = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private DateTime _lastFlushTime = DateTime.UtcNow;

        public OrderTaskConsumer(
            IHttpClientFactory httpClientFactory,
            ILogger<OrderTaskConsumer> logger,
            IServiceProvider serviceProvider)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            // Hoặc nếu bạn muốn tối ưu hơn cho ứng dụng hiện đại:
            

            try
            {
                using var connection = await factory.CreateConnectionAsync(stoppingToken);
                using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: (ushort)(_maxBatchSize * 2), global: false, stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var order = JsonSerializer.Deserialize<OrderMessageDto>(message,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (order == null)
                        {
                            await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                            return;
                        }

                        await _lock.WaitAsync(stoppingToken);
                        try
                        {
                            _buffer.Add((order, ea.DeliveryTag));

                            // Kiểm tra điều kiện Flush ngay trong callback
                            if (_buffer.Count >= _maxBatchSize)
                            {
                                await ProcessBatchInternalAsync(channel, stoppingToken);
                            }
                        }
                        finally
                        {
                            _lock.Release();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi khi nhận tin nhắn từ RabbitMQ");
                        await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, stoppingToken);

                // Background Timer để Flush theo thời gian (Tránh treo đơn lẻ)
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(5000, stoppingToken);

                    if (_buffer.Count > 0 && (DateTime.UtcNow - _lastFlushTime).TotalMilliseconds >= _maxWaitTimeMs)
                    {
                        await _lock.WaitAsync(stoppingToken);
                        try
                        {
                            if (_buffer.Count > 0) // Kiểm tra lại sau khi lấy lock
                            {
                                await ProcessBatchInternalAsync(channel, stoppingToken);
                            }
                        }
                        finally
                        {
                            _lock.Release();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Lỗi nghiêm trọng kết nối RabbitMQ");
            }
        }

        private async Task ProcessBatchInternalAsync(IChannel channel, CancellationToken ct)
        {
            var batchToProcess = _buffer.ToList();
            _buffer.Clear();
            _lastFlushTime = DateTime.UtcNow;

            _logger.LogInformation($"[BATCH] Đang xử lý {batchToProcess.Count} đơn hàng...");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TmdtContext>();

                var listOrders = batchToProcess.Select(x => x.Order).ToList();
                bool isSuccess = await DieuPhoiGomDonOptimization(listOrders, context);

                if (isSuccess)
                {
                    ulong highestTag = batchToProcess.Max(x => x.DeliveryTag);
                    await channel.BasicAckAsync(highestTag, multiple: true, ct);
                    _logger.LogInformation($"[SUCCESS] Đã Ack batch {batchToProcess.Count} đơn.");
                }
                else
                {
                    throw new Exception("Xử lý nghiệp vụ thất bại");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xử lý Batch. Đang thực hiện Requeue...");
                ulong highestTag = batchToProcess.Max(x => x.DeliveryTag);
                await channel.BasicNackAsync(highestTag, multiple: true, requeue: true, ct);
            }
        }

        private async Task<bool> DieuPhoiGomDonOptimization(List<OrderMessageDto> orders, TmdtContext context)
        {
            var clientPT = _httpClientFactory.CreateClient("PhuongTienApi");
            var clientNS = _httpClientFactory.CreateClient("NhanSuApi");

            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                var ordersByKho = orders.GroupBy(o => o.MaKho);

                foreach (var group in ordersByKho)
                {
                    int maKho = group.Key;
                    var danhSachDon = group.ToList();

                    // Gọi song song 2 API để tối ưu thời gian phản hồi
                    var taskXe = clientPT.GetFromJsonAsync<List<VehicleFreeDto>>($"api/quanlyxe/xe-san-sang-dieu-phoi?maKho={maKho}");
                    var taskTX = clientNS.GetFromJsonAsync<List<DriverAvailableDto>>($"api/quanlytaixe/lich-trinh-tai-xe?maKho={maKho}");

                    await Task.WhenAll(taskXe, taskTX);
                    var listXe = await taskXe ?? new();
                    var listTX = await taskTX ?? new();

                    if (!listXe.Any() || !listTX.Any())
                    {
                        _logger.LogWarning($"Kho {maKho} thiếu tài nguyên.");
                        return false;
                    }

                    // 1. Clustering
                    int k = Math.Max(1, (int)Math.Ceiling(danhSachDon.Count / 5.0));
                    var clusters = ApplyKMeansClustering(danhSachDon, k);

                    foreach (var cluster in clusters)
                    {
                        // 2. Bin Packing
                        var xePhuHop = ApplyBinPacking(cluster, listXe);
                        if (xePhuHop == null) continue;

                        var txChon = listTX.OrderByDescending(t => t.DiemUyTin).FirstOrDefault();
                        if (txChon == null) continue;

                       

                        // 3. Create Route
                        var loTrinhMoi = new LoTrinh
                        {
                            MaPhuongTien = xePhuHop.MaPhuongTien,
                            MaTaiXeChinh = txChon.MaNguoiDung,
                            TrangThai = "Chờ khởi hành",
                            ThoiGianBatDauKeHoach = DateTime.Now.AddHours(1)
                        };
                        context.LoTrinhs.Add(loTrinhMoi);
                        await context.SaveChangesAsync(); // Lưu để lấy MaLoTrinh tự tăng

                       
                        var chiTietKienHang = new ChiTietLoTrinhKienHang
                        {
                            MaLoTrinh = loTrinhMoi.MaLoTrinh,
                            MaDonHang = cluster.First().MaDonHang,
                            TrangThaiTrenXe = "Chờ lấy hàng"

                        };
                        context.ChiTietLoTrinhKienHangs.Add(chiTietKienHang);
                       
                        // 4. Optimize stops
                        var sortedRoute = OptimizeRoute(cluster);
                        for (int i = 0; i < sortedRoute.Count; i++)
                        {
                            var order = sortedRoute[i];
                            context.DiemDungs.Add(new DiemDung
                            {
                                MaLoTrinh = loTrinhMoi.MaLoTrinh,
                                MaDiaChi = order.MaDiaChiLayHang,
                                ThuTuDung = i + 1,
                                LoaiDung = "Pickup",
                                EtaKeHoach = DateTime.Now.AddMinutes(20 * (i + 1))
                            });
                        }

                        

                        // Cập nhật trạng thái API (nên dùng Batch Update nếu API hỗ trợ)
                        await clientPT.PutAsJsonAsync($"api/quanlyxe/capnhatxe/{xePhuHop.MaPhuongTien}", new { TrangThai = "Chờ khởi hành" });

                        listXe.Remove(xePhuHop);
                        listTX.Remove(txChon);
                    }
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi Database/API trong quá trình điều phối");
                await transaction.RollbackAsync();
                return false;
            }
        }

        // --- Các hàm Heuristic giữ nguyên logic nhưng tối ưu Performance ---

        private List<List<OrderMessageDto>> ApplyKMeansClustering(List<OrderMessageDto> orders, int k)
        {
            // Chia cụm dựa trên số lượng k
            return orders
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index % k)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }

        private VehicleFreeDto? ApplyBinPacking(List<OrderMessageDto> cluster, List<VehicleFreeDto> vehicles)
        {
            double totalWeight = cluster.Sum(o => o.KhoiLuong);
            return vehicles
                .Where(v => v.TaiTrongToiDaKg >= totalWeight)
                .OrderBy(v => v.TaiTrongToiDaKg) // Chọn xe nhỏ nhất vừa đủ tải
                .FirstOrDefault();
        }

        private List<OrderMessageDto> OptimizeRoute(List<OrderMessageDto> cluster)
        {
            return cluster.OrderBy(o => o.MaDiaChiLayHang).ToList();
        }
    }
}