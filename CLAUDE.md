# CLAUDE.md — NorthwindMicro (Microservices + Azure Service Bus)

## What This Project Is

A learning project demonstrating **event-driven microservices** using ASP.NET Core 8 and Azure Service Bus.
Three services communicate via a message queue — no direct HTTP calls between services.

| Service | Port | Role |
|---------|------|------|
| ApiGateway | 5020 | YARP reverse proxy — single entry point for clients |
| OrderService | 5021 | Creates orders, publishes `OrderPlaced` event to Azure Service Bus |
| InventoryService | 5022 | Subscribes to `OrderPlaced` event, deducts stock |
| SQL Server 2022 | 1433 | Local sidecar — hosts both databases |

---

## Architecture

```
Client (Swagger / React)
    └── ApiGateway (port 5020)  — YARP
          ├── /api/orders/*    → OrderService (5021)
          └── /api/inventory/* → InventoryService (5022)

Azure Service Bus (Basic tier, real cloud):
    OrderService ──publish──► [order-placed queue] ──subscribe──► InventoryService
```

**Database per service pattern** (both in same SQL Server sidecar):
- `MicroOrders`    — owned by OrderService (Orders + OrderItems tables)
- `MicroInventory` — owned by InventoryService (Products table, 10 seeded products)

---

## Key Concepts Demonstrated

| Concept | Where |
|---------|-------|
| API Gateway / reverse proxy | `ApiGateway/` — YARP routes all traffic |
| Event-driven communication | `OrderService/Messaging/OrderEventPublisher.cs` |
| Message consumer (BackgroundService) | `InventoryService/Messaging/OrderPlacedConsumer.cs` |
| Database per service | `MicroOrders` + `MicroInventory` — separate schemas |
| No direct service-to-service HTTP calls | Services only communicate via queue |
| Duplicate message contracts (intentional) | `OrderPlacedMessage.cs` exists in both services — no shared library |
| IServiceScopeFactory pattern | BackgroundService (singleton) → DbContext (scoped) — must create scope per message |
| Manual message completion | `CompleteAsync` on success, `AbandonAsync` on error, `DeadLetterAsync` on bad JSON |

---

## Project Structure

```
NorthwindMicro/
  .devcontainer/
    devcontainer.json          ← ports 5020, 5021, 5022, 1433; no csdevkit (incompatible with .NET 8)
    docker-compose.yml         ← injects DB + ASB connection strings from .env
    post-create.sh             ← creates MicroOrders + MicroInventory on first start
    seed-orders-db.sql         ← Orders + OrderItems schema
    seed-inventory-db.sql      ← Products table + 10 Northwind products
    .env.example               ← template — copy to .env with real connection string
  ApiGateway/
    Program.cs                 ← AddReverseProxy().LoadFromConfig(...)
    appsettings.json           ← YARP routes and cluster addresses
  OrderService/
    Controllers/OrdersController.cs    ← POST /api/orders, GET /api/orders, GET /api/orders/{id}
    Data/OrderDbContext.cs             ← EF Core, MicroOrders DB
    Messaging/OrderEventPublisher.cs   ← singleton, ServiceBusSender
    Messaging/OrderPlacedMessage.cs    ← event DTO
    Program.cs                         ← AddDbContext + AddSingleton<OrderEventPublisher>
  InventoryService/
    Controllers/InventoryController.cs ← GET /api/inventory, GET /api/inventory/{id}
    Data/InventoryDbContext.cs         ← EF Core, MicroInventory DB
    Messaging/OrderPlacedConsumer.cs   ← BackgroundService, ServiceBusProcessor
    Messaging/OrderPlacedMessage.cs    ← event DTO (deliberate duplicate)
    Program.cs                         ← AddDbContext + AddHostedService<OrderPlacedConsumer>
  NorthwindMicro.sln
  .gitattributes                       ← enforces LF line endings (prevents bash script failures)
```

---

## How to Open and Run

### Prerequisites (one-time)
- Docker Desktop (WSL 2 backend)
- VS Code + Dev Containers extension (`ms-vscode-remote.remote-containers`)
- Azure Service Bus namespace + `order-placed` queue (see Azure Setup below)

### Step 1 — Add Azure Service Bus connection string
```bash
# Inside the container terminal, or edit the file in VS Code
cp /workspace/.devcontainer/.env.example /workspace/.devcontainer/.env
# Edit .env — paste AZURE_SERVICE_BUS_CONNECTION_STRING value
```
Then **F1 → "Dev Containers: Rebuild Container"** to inject the env var.

### Step 2 — Open in Dev Container
1. VS Code → File → Open Folder → select `NorthwindMicro/`
2. Click **"Reopen in Container"** when prompted
3. `post-create.sh` runs automatically — creates both databases

### Step 3 — Start all 3 services (one terminal each)
```bash
# Terminal 1 — OrderService
cd /workspace/OrderService && dotnet run
# → http://localhost:5021/swagger

# Terminal 2 — InventoryService
cd /workspace/InventoryService && dotnet run
# → http://localhost:5022/swagger

# Terminal 3 — ApiGateway
cd /workspace/ApiGateway && dotnet run
# → http://localhost:5020 (proxy — no own swagger)
```

### Step 4 — Test the event flow
```
1. GET  http://localhost:5022/api/inventory/1       ← note UnitsInStock (39)
2. POST http://localhost:5020/api/orders            ← place order via gateway
   { "customerId": "ALFKI", "items": [{ "productId": 1, "quantity": 5, "unitPrice": 18.00 }] }
3. Watch InventoryService terminal — stock deduction log appears within ~5s
4. GET  http://localhost:5022/api/inventory/1       ← UnitsInStock now 34 (39 - 5)
5. GET  http://localhost:5021/api/orders/1          ← verify order saved
```

---

## Azure Service Bus Setup (one-time)

1. Azure Portal → Create Resource → **Service Bus**
2. Namespace: `northwind-micro-mahi` | Region: East Asia | Tier: **Basic**
3. After deploy → Entities → Queues → **+ Queue** → name: `order-placed`
4. Shared access policies (namespace level) → RootManageSharedAccessKey → copy **Primary Connection String**
5. Paste into `.devcontainer/.env` as `AZURE_SERVICE_BUS_CONNECTION_STRING`
6. Rebuild container

**Azure resources used:**
| Resource | Name |
|----------|------|
| Service Bus Namespace | `northwind-micro-mahi` |
| Queue | `order-placed` |
| Pricing | Basic (~$0.05/million messages) |

---

## Connection Strings

### MicroOrders (OrderService)
```
Server=db,1433;Database=MicroOrders;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;
```

### MicroInventory (InventoryService)
```
Server=db,1433;Database=MicroInventory;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;
```

**Connection string priority:**
- `appsettings.Development.json` — `Server=db,1433` (committed, works in container)
- `appsettings.json` — Windows SQLEXPRESS fallback
- docker-compose env vars — override both (injected from `.env` after rebuild)

---

## Git Identity (Shared Machine)

Identity auto-applied via `includeIf` rule — no manual setup needed.
- Name: `mahebilla`
- Email: `mahendran.apec@gmail.com`

---

## Notes for Claude

- **Do NOT modify existing source code** unless explicitly asked
- `ms-dotnettools.csdevkit` is intentionally excluded — crashes in .NET 8 containers (requires .NET 10 runtime assemblies). Use `ms-dotnettools.csharp` only.
- `OrderPlacedMessage.cs` is duplicated in both services intentionally — no shared library is the correct microservice pattern
- Azure Service Bus connection string is NEVER committed — lives in `.devcontainer/.env` (gitignored), injected by docker-compose
- Container must be rebuilt after editing `.env` for env vars to take effect
- `post-create.sh` only runs on first container creation — to re-run manually: `bash /workspace/.devcontainer/post-create.sh`
- To reset databases: note there is no named volume cleanup command here — delete and recreate the container
- `.gitattributes` enforces LF line endings — critical for bash scripts to run correctly in Linux container
- Solution file is `.sln` (classic format) — `.slnx` was replaced because .NET 8 SDK doesn't support it
