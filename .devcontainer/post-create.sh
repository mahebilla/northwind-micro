#!/bin/bash
# post-create.sh — runs once after the NorthwindMicro dev container is created
# Installs tools and creates MicroOrders + MicroInventory databases
set -e

echo ""
echo "┌──────────────────────────────────────────────────────────────────┐"
echo "│   NorthwindMicro Dev Container — First-Time Setup                │"
echo "└──────────────────────────────────────────────────────────────────┘"
echo ""

# ── Step 1: dotnet-ef CLI tool ──────────────────────────────────────────────
echo "▶ [1/3] Installing dotnet-ef tool..."
dotnet tool install --global dotnet-ef --version 8.* 2>/dev/null || echo "  (already installed)"
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.bashrc
export PATH="$PATH:$HOME/.dotnet/tools"
echo "  ✓ dotnet-ef ready"

# ── Step 2: Install sqlcmd ──────────────────────────────────────────────────
echo ""
echo "▶ [2/3] Installing sqlcmd..."
curl -sSL https://packages.microsoft.com/keys/microsoft.asc \
  | gpg --dearmor \
  | sudo tee /usr/share/keyrings/microsoft-prod.gpg > /dev/null

curl -sSL https://packages.microsoft.com/config/debian/12/prod.list \
  | sudo tee /etc/apt/sources.list.d/mssql-release.list > /dev/null

sudo apt-get update -qq 2>/dev/null || true
ACCEPT_EULA=Y sudo apt-get install -y -q mssql-tools18 unixodbc-dev
echo 'export PATH="$PATH:/opt/mssql-tools18/bin"' >> ~/.bashrc
export PATH="$PATH:/opt/mssql-tools18/bin"
echo "  ✓ sqlcmd ready"

# ── Step 3: Create MicroOrders and MicroInventory databases ─────────────────
echo ""
echo "▶ [3/3] Setting up databases..."

SA_PASS="${SA_PASSWORD:-YourStrong!Passw0rd}"
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"

# Wait for SQL Server to be ready (up to 150 seconds)
echo "  Waiting for SQL Server to start..."
for i in $(seq 1 30); do
  if $SQLCMD -S db,1433 -U sa -P "$SA_PASS" -Q "SELECT 1" -C -l 5 > /dev/null 2>&1; then
    echo "  SQL Server is ready."
    break
  fi
  if [ "$i" -eq 30 ]; then
    echo "  ERROR: SQL Server did not respond after 150 seconds. Check Docker logs."
    exit 1
  fi
  echo "  attempt $i/30 — retrying in 5s..."
  sleep 5
done

# ── MicroOrders (OrderService) ───────────────────────────────────────────────
ORDERS_DB_EXISTS=$($SQLCMD -S db,1433 -U sa -P "$SA_PASS" \
  -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name='MicroOrders'" \
  -C -h -1 2>/dev/null | tr -d ' \r\n')

if [ "$ORDERS_DB_EXISTS" = "1" ]; then
  echo "  MicroOrders database already exists — skipping."
else
  echo "  Creating MicroOrders database and tables..."
  $SQLCMD -S db,1433 -U sa -P "$SA_PASS" -Q "CREATE DATABASE MicroOrders" -C
  $SQLCMD -S db,1433 -U sa -P "$SA_PASS" -d MicroOrders \
    -i /workspace/.devcontainer/seed-orders-db.sql -C
  echo "  ✓ MicroOrders ready."
fi

# ── MicroInventory (InventoryService) ────────────────────────────────────────
INVENTORY_DB_EXISTS=$($SQLCMD -S db,1433 -U sa -P "$SA_PASS" \
  -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name='MicroInventory'" \
  -C -h -1 2>/dev/null | tr -d ' \r\n')

if [ "$INVENTORY_DB_EXISTS" = "1" ]; then
  echo "  MicroInventory database already exists — skipping."
else
  echo "  Creating MicroInventory database and seeding products..."
  $SQLCMD -S db,1433 -U sa -P "$SA_PASS" -Q "CREATE DATABASE MicroInventory" -C
  $SQLCMD -S db,1433 -U sa -P "$SA_PASS" -d MicroInventory \
    -i /workspace/.devcontainer/seed-inventory-db.sql -C
  echo "  ✓ MicroInventory ready with 10 seeded products."
fi

# ── Done ────────────────────────────────────────────────────────────────────
echo ""
echo "┌──────────────────────────────────────────────────────────────────────────┐"
echo "│  Setup complete!                                                           │"
echo "├──────────────────────────────────────────────────────────────────────────┤"
echo "│  BEFORE STARTING — add your Azure Service Bus connection string:           │"
echo "│    cp /workspace/.devcontainer/.env.example /workspace/.devcontainer/.env  │"
echo "│    # Edit .env and set AZURE_SERVICE_BUS_CONNECTION_STRING                 │"
echo "│    # Then rebuild the container (F1 → Dev Containers: Rebuild Container)   │"
echo "│                                                                             │"
echo "│  Terminal 1 — OrderService:    cd /workspace/OrderService && dotnet run    │"
echo "│  Terminal 2 — InventoryService: cd /workspace/InventoryService && dotnet run│"
echo "│  Terminal 3 — ApiGateway:      cd /workspace/ApiGateway && dotnet run      │"
echo "│                                                                             │"
echo "│  Swagger:  http://localhost:5021/swagger  (OrderService)                   │"
echo "│            http://localhost:5022/swagger  (InventoryService)               │"
echo "│  Gateway:  http://localhost:5020 (proxies to 5021 + 5022)                  │"
echo "└──────────────────────────────────────────────────────────────────────────┘"
echo ""
