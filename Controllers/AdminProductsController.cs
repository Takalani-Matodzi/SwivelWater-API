using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class AdminProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/AdminProducts
    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _context.Products
            .OrderBy(p => p.ProductName)
            .Select(p => new
            {
                productId = p.ProductId,
                productName = p.ProductName,
                description = p.Description,
                price = p.Price,
                stockQuantity = p.StockQuantity,
                productType = p.ProductType,
                imageUrl = p.ImageUrl,
                isActive = p.IsActive,
                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt
            })
            .ToListAsync();

        return Ok(products);
    }
}