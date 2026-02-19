# NorthwindMicro — Event-Driven Microservices

A learning project demonstrating **event-driven microservices** with ASP.NET Core 8 and Azure Service Bus.
Three services communicate via a message queue — no direct HTTP calls between services.

---

## Architecture

```
Client (Swagger / React)
    └── ApiGateway (port 5020)  — YARP reverse proxy
          ├── /api/orders/*    → OrderService (5021)
          └── /api/inventory/* → InventoryService (5022)

Azure Service Bus (Basic tier):
    OrderService ──publish──► [order-placed queue] ──subscribe──► InventoryService
```

### Event Flow

```
1. Client  →  POST /api/orders  →  ApiGateway
2. ApiGateway proxies to OrderService
3. OrderService saves order to MicroOrders DB
4. OrderService publishes OrderPlaced event to Azure Service Bus
5. InventoryService BackgroundService receives event from queue
6. InventoryService deducts UnitsInStock in MicroInventory DB
```

---

## Services

| Service | Port | Purpose |
|---------|------|---------|
| **ApiGateway** | 5020 | YARP reverse proxy — single entry point for all clients |
| **OrderService** | 5021 | Creates orders, publishes events |
| **InventoryService** | 5022 | Subscribes to events, manages stock |
| SQL Server 2022 | 1433 | Local sidecar — hosts MicroOrders + MicroInventory |

---

## Key Patterns

| Pattern | Implementation |
|---------|---------------|
| API Gateway | YARP (`Yarp.ReverseProxy`) in `ApiGateway/` |
| Event publishing | `Azure.Messaging.ServiceBus` — `ServiceBusSender` (singleton) |
| Event consuming | `BackgroundService` with `ServiceBusProcessor` |
| Database per service | `MicroOrders` (OrderService) + `MicroInventory` (InventoryService) |
| No shared library | `OrderPlacedMessage.cs` duplicated intentionally in both services |
| Scope per message | `IServiceScopeFactory` — BackgroundService (singleton) → DbContext (scoped) |

---

## Prerequisites

- Docker Desktop (WSL 2 backend)
- VS Code + [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
- Azure account with a Service Bus namespace (Basic tier)

---

## Azure Service Bus Setup (one-time)

1. Azure Portal → **Create Resource → Service Bus**
2. Namespace: `northwind-micro-mahi` | Region: East Asia | Pricing: **Basic**
3. After deploy → Entities → Queues → **+ Queue** → name: `order-placed`
4. **Namespace** Shared access policies → `RootManageSharedAccessKey` → copy **Primary Connection String**

---

## Running Locally

### Step 1 — Add connection string
```bash
cp .devcontainer/.env.example .devcontainer/.env
# Edit .env — paste AZURE_SERVICE_BUS_CONNECTION_STRING
```

### Step 2 — Open in Dev Container
- VS Code → File → Open Folder → select this folder
- Click **"Reopen in Container"** → wait for `post-create.sh` to complete

### Step 3 — Start services (one terminal each)
```bash
# Terminal 1
cd /workspace/OrderService && dotnet run

# Terminal 2
cd /workspace/InventoryService && dotnet run

# Terminal 3
cd /workspace/ApiGateway && dotnet run
```

### Step 4 — Test event flow
```bash
# 1. Check stock before
GET http://localhost:5022/api/inventory/1      # Chai — UnitsInStock: 39

# 2. Place order via gateway
POST http://localhost:5020/api/orders
{
  "customerId": "ALFKI",
  "items": [{ "productId": 1, "quantity": 5, "unitPrice": 18.00 }]
}

# 3. Wait ~5 seconds, watch InventoryService terminal for log output

# 4. Check stock after
GET http://localhost:5022/api/inventory/1      # Chai — UnitsInStock: 34 (39 - 5)

# 5. Verify order
GET http://localhost:5021/api/orders/1
```

---

## Swagger UIs

| URL | Service |
|-----|---------|
| `http://localhost:5021/swagger` | OrderService |
| `http://localhost:5022/swagger` | InventoryService |
| `http://localhost:5020` | ApiGateway (proxy only — use service swaggers) |

---

## Project Structure

```
NorthwindMicro/
  .devcontainer/
    devcontainer.json        ← VS Code config, port forwards
    docker-compose.yml       ← app + SQL Server, injects connection strings
    post-create.sh           ← creates databases on first start
    seed-orders-db.sql       ← MicroOrders schema
    seed-inventory-db.sql    ← MicroInventory schema + 10 products
    .env.example             ← template for secrets (copy to .env)
  ApiGateway/                ← YARP reverse proxy
  OrderService/              ← EF Core + Azure Service Bus publisher
  InventoryService/          ← EF Core + Azure Service Bus consumer
  NorthwindMicro.sln
```

---

## Related Projects

| Project | Repo | What it demonstrates |
|---------|------|----------------------|
| NorthwindApi | `northwind-devcontainer` | EF Core patterns (direct DbContext) |
| NorthwindCqrs | `northwind-devcontainer` | CQRS + Clean Architecture + MediatR |
| **NorthwindMicro** | `northwind-micro` | Event-driven microservices + API Gateway |
