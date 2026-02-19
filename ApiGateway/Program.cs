var builder = WebApplication.CreateBuilder(args);

// YARP — loads route + cluster config from appsettings.json "ReverseProxy" section
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "NorthwindMicro API Gateway", Version = "v1",
        Description = "YARP reverse proxy. Routes /api/orders/* → OrderService (5021), /api/inventory/* → InventoryService (5022)." });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// YARP middleware — proxies matching requests to upstream services
app.MapReverseProxy();

app.Run();
