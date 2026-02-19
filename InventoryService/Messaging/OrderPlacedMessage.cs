namespace InventoryService.Messaging;

// Intentional duplicate of OrderService.Messaging.OrderPlacedMessage
// Each service owns its own copy — no shared library (realistic microservice isolation)
// Both services agree on the JSON schema; the class is not shared as code
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
