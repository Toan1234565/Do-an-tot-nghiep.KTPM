using QuanLyNhatKyHeThong;
using QuanLyNhatKyHeThong.Models;
using QuanLyNhatKyHeThong.Models12;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace QuanLyTaiKhoanNguoiDung.BackgroundServices
{
    public class LogConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LogConsumer> _logger;
        private readonly string _queueName = "system_logs";
        private readonly ConnectionFactory _factory;

        public LogConsumer(IServiceProvider serviceProvider, ILogger<LogConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _factory = new ConnectionFactory()
            {
                HostName = "localhost"
                // Không cần DispatchConsumersAsync ở bản v7.x
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[RabbitMQ] LogConsumer v7 đang khởi động...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // v7 sử dụng CreateConnectionAsync trực tiếp với CancellationToken
                    using var connection = await _factory.CreateConnectionAsync(stoppingToken);

                    // SỬA LỖI 1: CreateChannelAsync trong v7 nhận CreateChannelOptions
                    // Nếu muốn truyền CancellationToken, ta để null cho options hoặc dùng mặc định
                    using var channel = await connection.CreateChannelAsync(options: null, cancellationToken: stoppingToken);

                    await channel.QueueDeclareAsync(
                        queue: _queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: stoppingToken);

                    var consumer = new AsyncEventingBasicConsumer(channel);

                    var jsonOptions = new JsonSerializerOptions
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = false
                    };

                    consumer.ReceivedAsync += async (model, ea) =>
                    {
                        try
                        {
                            var body = ea.Body.ToArray();
                            var message = Encoding.UTF8.GetString(body);
                            var logData = JsonSerializer.Deserialize<LogMessage>(message);

                            if (logData != null)
                            {
                                using (var scope = _serviceProvider.CreateScope())
                                {
                                    var dbContext = scope.ServiceProvider.GetRequiredService<TmdtContext>();

                                    var newLog = new NhatKyHeThong
                                    {
                                        TenDichVu = logData.TenDichVu,
                                        LoaiThaoTac = logData.LoaiThaoTac,
                                        DuLieuCu = JsonSerializer.Serialize(logData.DuLieuCu, jsonOptions),
                                        DuLieuMoi = JsonSerializer.Serialize(logData.DuLieuMoi, jsonOptions),
                                        NguoiThucHien = logData.NguoiThucHien,
                                        ThoiGianThucHien = logData.ThoiGianThucHien ?? DateTime.Now,
                                        DiaChiIp = logData.DiaChiIp,
                                        TrangThaiThaoTac = logData.TrangThaiThaoTac
                                    };

                                    dbContext.NhatKyHeThongs.Add(newLog);
                                    await dbContext.SaveChangesAsync();
                                    _logger.LogInformation($"[RabbitMQ] Đã lưu nhật ký: {logData.LoaiThaoTac}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[RabbitMQ] Lỗi xử lý tin nhắn");
                        }
                    };

                    // v7: BasicConsumeAsync
                    await channel.BasicConsumeAsync(
                        queue: _queueName,
                        autoAck: true,
                        consumer: consumer,
                        cancellationToken: stoppingToken);

                    _logger.LogInformation("[RabbitMQ] Đã kết nối thành công.");

                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (BrokerUnreachableException)
                {
                    _logger.LogWarning("[RabbitMQ] Server chưa sẵn sàng. Thử lại sau 5s...");
                    await Task.Delay(5000, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Lỗi kết nối.");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }
    }
}