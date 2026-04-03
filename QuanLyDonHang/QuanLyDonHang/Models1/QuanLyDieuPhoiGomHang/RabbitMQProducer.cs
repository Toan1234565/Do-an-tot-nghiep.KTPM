using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

public class RabbitMQProducer
{
    private readonly string _hostname = "localhost";

    public async Task SendOrderMessageAsync<T>(T message)
    {
        var factory = new ConnectionFactory() { HostName = _hostname };

        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(queue: "order_queue",
                                        durable: true,
                                        exclusive: false,
                                        autoDelete: false,
                                        arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        // FIX: Cách tạo Properties đúng trong bản v7
        var properties = new BasicProperties();
        properties.Persistent = true;

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: "order_queue",
            mandatory: false,
            basicProperties: properties, // Truyền trực tiếp object properties
            body: body);

        Console.WriteLine($" [x] Đã đẩy đơn hàng sang hàng đợi: {json}");
    }
}