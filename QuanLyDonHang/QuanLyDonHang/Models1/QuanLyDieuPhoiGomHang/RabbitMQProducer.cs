using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

public class RabbitMQProducer
{
    public async Task SendOrderMessageAsync<T>(T message)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        // Sử dụng CreateConnectionAsync thay vì CreateConnection
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync(); // Version 7+ dùng CreateChannelAsync

        await channel.QueueDeclareAsync(queue: "order_queue",
                                        durable: true,
                                        exclusive: false,
                                        autoDelete: false,
                                        arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        await channel.BasicPublishAsync(exchange: "",
                                        routingKey: "order_queue",
                                        body: body);
    }
}