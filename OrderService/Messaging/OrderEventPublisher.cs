using Azure.Messaging.ServiceBus;
using System.Text.Json;

namespace OrderService.Messaging;

// Wraps ServiceBusSender. Registered as singleton — one sender per application lifetime.
// Azure Service Bus Basic tier supports queues (not topics). One publisher, one consumer.
public class OrderEventPublisher : IAsyncDisposable
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger<OrderEventPublisher> _logger;

    public OrderEventPublisher(IConfiguration config, ILogger<OrderEventPublisher> logger)
    {
        _logger = logger;
        var connectionString = config["AzureServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("AzureServiceBus:ConnectionString is not configured. Set it in .devcontainer/.env.");
        var queueName = config["AzureServiceBus:QueueName"] ?? "order-placed";

        var client = new ServiceBusClient(connectionString);
        _sender = client.CreateSender(queueName);
        _logger.LogInformation("OrderEventPublisher connected to queue '{Queue}'", queueName);
    }

    public async Task PublishOrderPlacedAsync(OrderPlacedMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        var sbMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            Subject = "OrderPlaced",
            MessageId = Guid.NewGuid().ToString()
        };

        await _sender.SendMessageAsync(sbMessage);
        _logger.LogInformation("Published OrderPlaced event for OrderId={OrderId}", message.OrderId);
    }

    public async ValueTask DisposeAsync() => await _sender.DisposeAsync();
}
