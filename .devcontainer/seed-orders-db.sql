-- seed-orders-db.sql
-- Creates Orders + OrderItems tables in MicroOrders database.
-- Run by post-create.sh on first container start.
-- Note: EF Core EnsureCreated() in OrderService/Program.cs also creates these tables.
-- Having both ensures schema exists before dotnet run is first executed.

CREATE TABLE Orders (
    OrderId    INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId NVARCHAR(10)  NOT NULL,
    OrderDate  DATETIME      NOT NULL DEFAULT GETDATE(),
    Status     NVARCHAR(20)  NOT NULL DEFAULT 'Placed'
);

CREATE TABLE OrderItems (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    OrderId   INT            NOT NULL REFERENCES Orders(OrderId) ON DELETE CASCADE,
    ProductId INT            NOT NULL,
    Quantity  INT            NOT NULL,
    UnitPrice DECIMAL(10,2)  NOT NULL
);

PRINT 'MicroOrders schema created successfully.';
