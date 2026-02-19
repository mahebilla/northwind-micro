namespace OrderService.Messaging;

// Event published to Azure Service Bus queue: order-placed
// InventoryService has its own copy of this message type — no shared library (intentional microservice isolation)
public class OrderPlacedMessage
{
    public int OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public DateTime PlacedAt { get; set; }
    public List<OrderItemMessage> Items { get; set; } = new();
}

public class OrderItemMessage
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
