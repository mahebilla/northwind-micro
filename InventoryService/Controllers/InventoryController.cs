using InventoryService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public InventoryController(InventoryDbContext db) => _db = db;

    // GET api/inventory — list all products with current stock levels
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _db.Products
            .OrderBy(p => p.ProductId)
            .Select(p => new { p.ProductId, p.ProductName, p.UnitsInStock })
            .ToListAsync();

        return Ok(products);
    }

    // GET api/inventory/{productId}
    [HttpGet("{productId:int}")]
    public async Task<IActionResult> GetById(int productId)
    {
        var product = await _db.Products
            .Where(p => p.ProductId == productId)
            .Select(p => new { p.ProductId, p.ProductName, p.UnitsInStock })
            .FirstOrDefaultAsync();

        if (product is null)
            return NotFound(new { Error = $"Product {productId} not found in inventory." });

        return Ok(product);
    }
}
