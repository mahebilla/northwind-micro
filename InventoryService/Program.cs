using InventoryService.Data;
using InventoryService.Messaging;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── EF Core — InventoryService owns MicroInventory database ──────────────────
var connStr = builder.Configuration.GetConnectionString("MicroInventoryConnection");
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(connStr));

// ── BackgroundService — subscribes to Azure Service Bus queue ─────────────────
// Starts automatically with the app. Processes OrderPlaced events and deducts stock.
// Uses IServiceScopeFactory internally (singleton → scoped DbContext pattern).
builder.Services.AddHostedService<OrderPlacedConsumer>();

// ── Controllers + Swagger ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "InventoryService API", Version = "v1",
        Description = "Manages product stock levels. Subscribes to 'order-placed' queue on Azure Service Bus and deducts UnitsInStock when orders are placed." });
});

var app = builder.Build();

// Auto-create Products table on startup
// Note: seed data (INSERT) comes from seed-inventory-db.sql run by post-create.sh
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
