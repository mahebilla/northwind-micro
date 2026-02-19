namespace OrderService.Models;

public class Order
{
    public int OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Placed";

    public List<OrderItem> Items { get; set; } = new();
}
