using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;
using SwivelWater.API.DTOs;
using SwivelWater.API.Models;

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

    // =========================================================
    // GET: api/AdminProducts
    // =========================================================
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

    // =========================================================
    // POST: api/AdminProducts
    // ADD NEW PRODUCT
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> CreateProduct(ProductCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ProductName))
        {
            return BadRequest("Product name is required.");
        }

        if (dto.Price < 0)
        {
            return BadRequest("Product price cannot be negative.");
        }

        if (dto.StockQuantity < 0)
        {
            return BadRequest("Stock quantity cannot be negative.");
        }

        var productType = string.IsNullOrWhiteSpace(dto.ProductType)
            ? "BOTTLED"
            : dto.ProductType.Trim().ToUpperInvariant();

        // Only product types currently supported by Swivel Water.
        if (productType != "BOTTLED" &&
            productType != "REFILL" &&
            productType != "REFILL_CARD")
        {
            return BadRequest(
                "Invalid product type. Use BOTTLED, REFILL or REFILL_CARD."
            );
        }

        // Only one active REFILL product should exist.
        if (productType == "REFILL")
        {
            var refillExists = await _context.Products.AnyAsync(p =>
                p.ProductType == "REFILL" &&
                p.IsActive);

            if (refillExists)
            {
                return Conflict(
                    "An active Water Refill product already exists."
                );
            }
        }

        // Only one active REFILL_CARD product should exist.
        if (productType == "REFILL_CARD")
        {
            var loyaltyCardExists = await _context.Products.AnyAsync(p =>
                p.ProductType == "REFILL_CARD" &&
                p.IsActive);

            if (loyaltyCardExists)
            {
                return Conflict(
                    "An active Refill Loyalty Card product already exists."
                );
            }
        }

        var product = new Product
        {
            ProductName = dto.ProductName.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description)
                ? null
                : dto.Description.Trim(),
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            ProductType = productType,
            ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl)
                ? null
                : dto.ImageUrl.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Product created successfully.",
            product = new
            {
                productId = product.ProductId,
                productName = product.ProductName,
                description = product.Description,
                price = product.Price,
                stockQuantity = product.StockQuantity,
                productType = product.ProductType,
                imageUrl = product.ImageUrl,
                isActive = product.IsActive,
                createdAt = product.CreatedAt,
                updatedAt = product.UpdatedAt
            }
        });
    }

    // =========================================================
    // PUT: api/AdminProducts/{id}
    // UPDATE PRODUCT
    // =========================================================
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        ProductUpdateDto dto)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null)
        {
            return NotFound("Product not found.");
        }

        if (string.IsNullOrWhiteSpace(dto.ProductName))
        {
            return BadRequest("Product name is required.");
        }

        if (dto.Price < 0)
        {
            return BadRequest("Product price cannot be negative.");
        }

        if (dto.StockQuantity < 0)
        {
            return BadRequest("Stock quantity cannot be negative.");
        }

        var productType = string.IsNullOrWhiteSpace(dto.ProductType)
            ? "BOTTLED"
            : dto.ProductType.Trim().ToUpperInvariant();

        if (productType != "BOTTLED" &&
            productType != "REFILL" &&
            productType != "REFILL_CARD")
        {
            return BadRequest(
                "Invalid product type. Use BOTTLED, REFILL or REFILL_CARD."
            );
        }

        // Prevent creating a second active REFILL product.
        if (productType == "REFILL" && dto.IsActive)
        {
            var anotherRefillExists = await _context.Products.AnyAsync(p =>
                p.ProductId != id &&
                p.ProductType == "REFILL" &&
                p.IsActive);

            if (anotherRefillExists)
            {
                return Conflict(
                    "Another active Water Refill product already exists."
                );
            }
        }

        // Prevent creating a second active REFILL_CARD product.
        if (productType == "REFILL_CARD" && dto.IsActive)
        {
            var anotherLoyaltyCardExists = await _context.Products.AnyAsync(p =>
                p.ProductId != id &&
                p.ProductType == "REFILL_CARD" &&
                p.IsActive);

            if (anotherLoyaltyCardExists)
            {
                return Conflict(
                    "Another active Refill Loyalty Card product already exists."
                );
            }
        }

        product.ProductName = dto.ProductName.Trim();
        product.Description = string.IsNullOrWhiteSpace(dto.Description)
            ? null
            : dto.Description.Trim();
        product.Price = dto.Price;
        product.StockQuantity = dto.StockQuantity;
        product.ProductType = productType;
        product.ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl)
            ? null
            : dto.ImageUrl.Trim();
        product.IsActive = dto.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Product updated successfully."
        });
    }

    // =========================================================
    // DELETE: api/AdminProducts/{id}
    // REMOVE PRODUCT
    // =========================================================
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null)
        {
            return NotFound("Product not found.");
        }

        // Do not permanently delete products that are already
        // referenced by historical order items.
        var hasOrderHistory = await _context.OrderItems
            .AnyAsync(oi => oi.ProductId == id);

        if (hasOrderHistory)
        {
            product.IsActive = false;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "This product has order history, so it was deactivated instead of permanently deleted."
            });
        }

        _context.Products.Remove(product);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Product removed successfully."
        });
    }
}