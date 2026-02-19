-- seed-inventory-db.sql
-- Creates Products table and seeds 10 Northwind-themed products in MicroInventory.
-- Run by post-create.sh on first container start.
-- Note: EF Core EnsureCreated() in InventoryService/Program.cs creates the schema,
-- but NOT the seed data. The INSERT rows here must come from this script.

CREATE TABLE Products (
    ProductId    INT           NOT NULL PRIMARY KEY,
    ProductName  NVARCHAR(100) NOT NULL,
    UnitsInStock INT           NOT NULL DEFAULT 0
);

-- 10 Northwind products with realistic starting stock levels
INSERT INTO Products (ProductId, ProductName, UnitsInStock) VALUES
(1,  'Chai',                           39),
(2,  'Chang',                          17),
(3,  'Aniseed Syrup',                  13),
(4,  'Chef Anton''s Cajun Seasoning',  53),
(5,  'Chef Anton''s Gumbo Mix',         0),
(6,  'Grandma''s Boysenberry Spread', 120),
(7,  'Uncle Bob''s Organic Dried Pears', 15),
(8,  'Northwoods Cranberry Sauce',      6),
(9,  'Mishi Kobe Niku',               29),
(10, 'Ikura',                          31);

PRINT 'MicroInventory seeded with 10 products.';
