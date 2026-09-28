using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;
using SwivelWater.API.DTOs;
using SwivelWater.API.Models;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET: api/Products
    //
    // Public catalogue - only active products are shown.
    // =========================================================
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts()
    {
        var products = await _context.Products
            .Where(p => p.IsActive)
            .Select(p => new ProductDto
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ProductType = p.ProductType,
                ImageUrl = p.ImageUrl,
                IsActive = p.IsActive
            })
            .ToListAsync();

        return Ok(products);
    }

    // =========================================================
    // GET: api/Products/{id}
    //
    // Public access for active products.
    // =========================================================
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductDto>> GetProduct(Guid id)
    {
        var product = await _context.Products
            .Where(p => p.ProductId == id && p.IsActive)
            .Select(p => new ProductDto
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ProductType = p.ProductType,
                ImageUrl = p.ImageUrl,
                IsActive = p.IsActive
            })
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    // =========================================================
    // POST: api/Products
    //
    // Only admins can create products.
    //
    // REFILL:
    // - Price is always R1.00 per litre
    //
    // BOTTLED:
    // - Price comes from the admin
    // =========================================================
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        ProductCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ProductName))
        {
            return BadRequest("Product name is required.");
        }

        var productType = string.IsNullOrWhiteSpace(dto.ProductType)
            ? "BOTTLED"
            : dto.ProductType.Trim().ToUpperInvariant();

        var allowedProductTypes = new[]
        {
            "BOTTLED",
            "REFILL"
        };

        if (!allowedProductTypes.Contains(productType))
        {
            return BadRequest(
                "Product type must be BOTTLED or REFILL."
            );
        }

        if (dto.StockQuantity < 0)
        {
            return BadRequest(
                "Stock quantity cannot be negative."
            );
        }

        decimal finalPrice;

        if (productType == "REFILL")
        {
            // Swivel Water refill price is fixed at R1 per litre.
            finalPrice = 1.00m;
        }
        else
        {
            if (dto.Price <= 0)
            {
                return BadRequest(
                    "Product price must be greater than zero."
                );
            }

            finalPrice = dto.Price;
        }

        var product = new Product
        {
            ProductName = dto.ProductName.Trim(),
            Description = dto.Description,
            Price = finalPrice,
            StockQuantity = dto.StockQuantity,
            ProductType = productType,
            ImageUrl = dto.ImageUrl,
            IsActive = dto.IsActive
        };

        _context.Products.Add(product);

        await _context.SaveChangesAsync();

        var result = new ProductDto
        {
            ProductId = product.ProductId,
            ProductName = product.ProductName,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            ProductType = product.ProductType,
            ImageUrl = product.ImageUrl,
            IsActive = product.IsActive
        };

        return CreatedAtAction(
            nameof(GetProduct),
            new { id = product.ProductId },
            result
        );
    }

    // =========================================================
    // PUT: api/Products/{id}
    //
    // Only admins can update products.
    // =========================================================
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        ProductUpdateDto dto)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(dto.ProductName))
        {
            return BadRequest("Product name is required.");
        }

        var productType = string.IsNullOrWhiteSpace(dto.ProductType)
            ? "BOTTLED"
            : dto.ProductType.Trim().ToUpperInvariant();

        var allowedProductTypes = new[]
        {
            "BOTTLED",
            "ACCESSORY",
            "REFILL",
            "REFILL_CARD"
        };

        if (!allowedProductTypes.Contains(productType))
        {
            return BadRequest(
                "Product type must be BOTTLED or REFILL."
            );
        }

        if (dto.StockQuantity < 0)
        {
            return BadRequest(
                "Stock quantity cannot be negative."
            );
        }

        decimal finalPrice;

        if (productType == "REFILL")
        {
            // Swivel Water refill price is fixed at R1 per litre.
            finalPrice = 1.00m;
        }
        else
        {
            if (dto.Price <= 0)
            {
                return BadRequest(
                    "Product price must be greater than zero."
                );
            }

            finalPrice = dto.Price;
        }

        product.ProductName = dto.ProductName.Trim();
        product.Description = dto.Description;
        product.Price = finalPrice;
        product.StockQuantity = dto.StockQuantity;
        product.ProductType = productType;
        product.ImageUrl = dto.ImageUrl;
        product.IsActive = dto.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // =========================================================
    // DELETE: api/Products/{id}
    //
    // Only admins can delete products.
    // =========================================================
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null)
        {
            return NotFound();
        }

        _context.Products.Remove(product);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}