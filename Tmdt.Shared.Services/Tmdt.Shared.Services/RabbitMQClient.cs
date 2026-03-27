using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace Tmdt.Shared.Services
{
    public class RabbitMQClient
    {
        private readonly string _hostName = "localhost";
        private readonly string _queueName = "system_logs";

        public async Task SendLogAsync(LogMessage log)
        {
            try
            {
                var factory = new ConnectionFactory() { HostName = _hostName };

                // Sử dụng await cho kết nối và channel (RabbitMQ.Client v7+)
                using var connection = await factory.CreateConnectionAsync();
                using var channel = await connection.CreateChannelAsync();

                // Khai báo hàng đợi (Đảm bảo các tham số khớp với phía Receiver)
                await channel.QueueDeclareAsync(queue: _queueName,
                                                durable: true,
                                                exclusive: false,
                                                autoDelete: false,
                                                arguments: null);

                // Cấu hình JsonSerializer để hiển thị tiếng Việt có dấu thay vì mã \uXXXX
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    // Cho phép các ký tự Unicode (tiếng Việt) không bị mã hóa
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(log, options);
                var body = Encoding.UTF8.GetBytes(json);

                // Gửi tin nhắn vào queue
                await channel.BasicPublishAsync(exchange: "",
                                                routingKey: _queueName,
                                                body: body);
            }
            catch (Exception ex)
            {
                // Bạn có thể log lỗi ra console hoặc file nếu việc gửi tin nhắn thất bại
                Console.WriteLine($"[RabbitMQ Error] Không thể gửi log: {ex.Message}");
            }
        }
    }
}
