using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Messaging;

var builder = WebApplication.CreateBuilder(args);

// ── EF Core — OrderService owns MicroOrders database ─────────────────────────
var connStr = builder.Configuration.GetConnectionString("MicroOrdersConnection");
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(connStr));

// ── Azure Service Bus publisher — singleton (one sender per app lifetime) ─────
// Connection string comes from AzureServiceBus:ConnectionString in config
// In dev container: injected by docker-compose from .devcontainer/.env
builder.Services.AddSingleton<OrderEventPublisher>();

// ── Controllers + Swagger ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "OrderService API", Version = "v1",
        Description = "Creates orders and publishes OrderPlaced events to Azure Service Bus queue 'order-placed'. InventoryService subscribes to deduct stock." });
});

var app = builder.Build();

// Auto-create tables on startup (code-first without migrations — suitable for learning)
// In production: use EF migrations for schema control
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
