using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Messaging;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _db;
    private readonly OrderEventPublisher _publisher;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(OrderDbContext db, OrderEventPublisher publisher, ILogger<OrdersController> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    // GET api/orders — list recent orders
    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.OrderId)
            .Take(20)
            .Select(o => new
            {
                o.OrderId,
                o.CustomerId,
                o.OrderDate,
                o.Status,
                ItemCount = o.Items.Count,
                Total = o.Items.Sum(i => i.Quantity * i.UnitPrice)
            })
            .ToListAsync();

        return Ok(orders);
    }

    // GET api/orders/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order is null)
            return NotFound(new { Error = $"Order {id} not found." });

        return Ok(new
        {
            order.OrderId,
            order.CustomerId,
            order.OrderDate,
            order.Status,
            Items = order.Items.Select(i => new
            {
                i.ProductId,
                i.Quantity,
                i.UnitPrice,
                LineTotal = i.Quantity * i.UnitPrice
            })
        });
    }

    // POST api/orders
    // Step 1: Save order to MicroOrders DB (OrderService owns this data)
    // Step 2: Publish OrderPlaced event to Azure Service Bus queue
    // Step 3: InventoryService picks up the event and deducts stock (async, decoupled)
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { Error = "Order must contain at least one item." });

        var order = new Order
        {
            CustomerId = request.CustomerId,
            OrderDate = DateTime.UtcNow,
            Status = "Placed",
            Items = request.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Order {OrderId} saved to MicroOrders database.", order.OrderId);

        // Publish event AFTER successful DB save — never publish before persisting
        var message = new OrderPlacedMessage
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            PlacedAt = order.OrderDate,
            Items = order.Items.Select(i => new OrderItemMessage
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        await _publisher.PublishOrderPlacedAsync(message);

        return CreatedAtAction(nameof(GetOrder), new { id = order.OrderId }, new
        {
            order.OrderId,
            order.CustomerId,
            order.Status,
            Message = "Order created. OrderPlaced event published to Azure Service Bus — InventoryService will deduct stock shortly."
        });
    }
}

// ── Request DTOs ─────────────────────────────────────────────────────────────
public class CreateOrderRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
