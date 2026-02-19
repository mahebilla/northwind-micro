using Azure.Messaging.ServiceBus;
using InventoryService.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InventoryService.Messaging;

// BackgroundService that subscribes to Azure Service Bus 'order-placed' queue.
// Runs for the entire lifetime of the InventoryService process alongside the HTTP server.
//
// Key patterns demonstrated:
//   1. IServiceScopeFactory  — BackgroundService is singleton; DbContext is scoped.
//      Never inject scoped services directly into a singleton. Create a new scope per message.
//   2. AutoCompleteMessages=false — manual message completion gives full control:
//      CompleteMessageAsync   → success (removes from queue)
//      AbandonMessageAsync    → transient error (ASB redelivers, up to MaxDeliveryCount=10)
//      DeadLetterMessageAsync → permanent error (moves to dead-letter sub-queue for inspection)
public class OrderPlacedConsumer : BackgroundService
{
    private readonly ServiceBusProcessor _processor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderPlacedConsumer> _logger;

    public OrderPlacedConsumer(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderPlacedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var connectionString = config["AzureServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("AzureServiceBus:ConnectionString is not configured. Set it in .devcontainer/.env.");
        var queueName = config["AzureServiceBus:QueueName"] ?? "order-placed";

        var client = new ServiceBusClient(connectionString);
        _processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,        // sequential stock updates — no concurrent deductions
            AutoCompleteMessages = false   // we manually complete only on success
        });

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;
        _logger.LogInformation("OrderPlacedConsumer subscribed to queue '{Queue}'", queueName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _processor.StartProcessingAsync(stoppingToken);

        // Keep running until the host shuts down
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }

        _logger.LogInformation("OrderPlacedConsumer stopping...");
        await _processor.StopProcessingAsync();
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var body = args.Message.Body.ToString();
        _logger.LogInformation("Received message from queue: {Body}", body);

        OrderPlacedMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<OrderPlacedMessage>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Cannot deserialize OrderPlaced message — dead-lettering.");
            await args.DeadLetterMessageAsync(args.Message, "DeserializationFailed", ex.Message);
            return;
        }

        if (message is null || message.Items.Count == 0)
        {
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        // Create a new DI scope per message — DbContext is scoped, BackgroundService is singleton
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        try
        {
            foreach (var item in message.Items)
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.ProductId == item.ProductId);
                if (product is null)
                {
                    _logger.LogWarning("Product {ProductId} not found in inventory — skipping.", item.ProductId);
                    continue;
                }

                var previous = product.UnitsInStock;
                product.UnitsInStock = Math.Max(0, product.UnitsInStock - item.Quantity);
                _logger.LogInformation(
                    "Product {ProductId} ({Name}): stock {Prev} → {New} (deducted {Qty})",
                    product.ProductId, product.ProductName, previous, product.UnitsInStock, item.Quantity);
            }

            await db.SaveChangesAsync();

            // Only complete after successful DB save
            await args.CompleteMessageAsync(args.Message);
            _logger.LogInformation("OrderId={OrderId} processed. Stock updated.", message.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderId={OrderId} — abandoning for retry.", message.OrderId);
            // Abandon: ASB redelivers the message (up to MaxDeliveryCount=10, then auto dead-letters)
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "ServiceBus processor error. Source={Source}, EntityPath={EntityPath}",
            args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _processor.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
