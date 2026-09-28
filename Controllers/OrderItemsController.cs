using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;
using SwivelWater.API.DTOs;
using SwivelWater.API.Models;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderItemsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OrderItemsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET: api/OrderItems
    // =========================================================
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderItemDto>>> GetOrderItems()
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var orderItems = await _context.OrderItems
            .Where(oi => oi.Order.Customer.UserId == userId.Value)
            .Select(oi => new OrderItemDto
            {
                OrderItemId = oi.OrderItemId,
                OrderId = oi.OrderId,
                ProductId = oi.ProductId,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                SubTotal = oi.SubTotal
            })
            .ToListAsync();

        return Ok(orderItems);
    }

    // =========================================================
    // GET: api/OrderItems/{id}
    // =========================================================
    [HttpGet("{id}")]
    public async Task<ActionResult<OrderItemDto>> GetOrderItem(Guid id)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var orderItem = await _context.OrderItems
            .Where(oi =>
                oi.OrderItemId == id &&
                oi.Order.Customer.UserId == userId.Value)
            .Select(oi => new OrderItemDto
            {
                OrderItemId = oi.OrderItemId,
                OrderId = oi.OrderId,
                ProductId = oi.ProductId,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                SubTotal = oi.SubTotal
            })
            .FirstOrDefaultAsync();

        if (orderItem == null)
        {
            return NotFound("Order item not found.");
        }

        return Ok(orderItem);
    }

    // =========================================================
    // POST: api/OrderItems
    //
    // SUPPORTED PRODUCT TYPES:
    // BOTTLED
    // REFILL
    // REFILL_CARD
    //
    // REFILL:
    // - COLLECTION only
    // - R1 per litre
    // - Loyalty card not required
    //
    // REFILL_CARD:
    // - COLLECTION only
    // - Quantity = 1
    // - Fixed price = R50
    // - Digital product, stock not required
    // - One active card per customer
    //
    // LOYALTY FREE REFILL:
    // - COLLECTION only
    // - Exactly 5L
    // - R0
    // =========================================================
    [HttpPost]
    public async Task<ActionResult<OrderItemDto>> CreateOrderItem(
        OrderItemCreateDto dto)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var order = await _context.Orders
            .FirstOrDefaultAsync(o =>
                o.OrderId == dto.OrderId &&
                o.Customer.UserId == userId.Value);

        if (order == null)
        {
            return Forbid();
        }

        if (order.OrderStatus == "COMPLETED" ||
            order.OrderStatus == "CANCELLED")
        {
            return BadRequest(
                "Items cannot be added to a completed or cancelled order."
            );
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p =>
                p.ProductId == dto.ProductId &&
                p.IsActive);

        if (product == null)
        {
            return BadRequest(
                "Product does not exist or is inactive."
            );
        }

        if (dto.Quantity <= 0)
        {
            return BadRequest(
                "Quantity must be greater than zero."
            );
        }

        var productType = string.IsNullOrWhiteSpace(product.ProductType)
            ? "BOTTLED"
            : product.ProductType.Trim().ToUpperInvariant();

        if (productType != "BOTTLED" &&
            productType != "REFILL" &&
            productType != "REFILL_CARD")
        {
            return BadRequest(
                "Invalid product type."
            );
        }

        // REFILL_CARD is digital, so stock does not apply.
        if (productType != "REFILL_CARD" &&
            dto.Quantity > product.StockQuantity)
        {
            return BadRequest(
                "Not enough stock available."
            );
        }

        // =====================================================
        // REFILL
        // =====================================================
        if (productType == "REFILL" &&
            order.OrderType != "COLLECTION")
        {
            return BadRequest(
                "Refill orders are COLLECTION only. " +
                "Please create a collection order for a refill."
            );
        }

        // =====================================================
        // LOYALTY FREE REFILL
        // =====================================================
        if (order.UsesLoyaltyFreeRefill)
        {
            if (order.OrderType != "COLLECTION")
            {
                return BadRequest(
                    "A loyalty free refill must be a COLLECTION order."
                );
            }

            if (productType != "REFILL")
            {
                return BadRequest(
                    "A loyalty free refill order can only contain a REFILL product."
                );
            }

            if (dto.Quantity != 5)
            {
                return BadRequest(
                    "A loyalty free refill must be exactly 5 litres."
                );
            }
        }

        // =====================================================
        // REFILL CARD
        // =====================================================
        if (productType == "REFILL_CARD")
        {
            if (order.OrderType != "COLLECTION")
            {
                return BadRequest(
                    "The refill loyalty card must be purchased as a COLLECTION order."
                );
            }

            if (dto.Quantity != 1)
            {
                return BadRequest(
                    "A refill loyalty card can only be purchased one at a time."
                );
            }

            if (order.UsesLoyaltyFreeRefill)
            {
                return BadRequest(
                    "A loyalty card cannot be added to a free refill order."
                );
            }

            var hasActiveCard =
                await _context.RefillLoyaltyCards
                    .AnyAsync(c =>
                        c.CustomerId == order.CustomerId &&
                        c.IsActive);

            if (hasActiveCard)
            {
                return BadRequest(
                    "You already have an active refill loyalty card."
                );
            }

            var hasPendingCardPurchase =
                await _context.OrderItems
                    .AnyAsync(oi =>
                        oi.Product.ProductType == "REFILL_CARD" &&
                        oi.Order.CustomerId == order.CustomerId &&
                        (
                            oi.Order.OrderStatus == "PENDING" ||
                            oi.Order.OrderStatus == "READY_FOR_COLLECTION" ||
                            oi.Order.OrderStatus == "PROCESSING"
                        ));

            if (hasPendingCardPurchase)
            {
                return BadRequest(
                    "You already have a refill loyalty card purchase in progress."
                );
            }
        }

        // =====================================================
        // EXISTING PRODUCT TYPES IN THIS ORDER
        // =====================================================
        var hasRefillItem = await _context.OrderItems
            .AnyAsync(oi =>
                oi.OrderId == order.OrderId &&
                oi.Product.ProductType == "REFILL");

        var hasBottledItem = await _context.OrderItems
            .AnyAsync(oi =>
                oi.OrderId == order.OrderId &&
                oi.Product.ProductType == "BOTTLED");

        var hasLoyaltyCardItem = await _context.OrderItems
            .AnyAsync(oi =>
                oi.OrderId == order.OrderId &&
                oi.Product.ProductType == "REFILL_CARD");

        // REFILL CARD must be alone.
        if (productType == "REFILL_CARD" &&
            (hasRefillItem ||
             hasBottledItem ||
             hasLoyaltyCardItem))
        {
            return BadRequest(
                "A refill loyalty card must be purchased in a separate order."
            );
        }

        if (hasLoyaltyCardItem)
        {
            return BadRequest(
                "This order already contains a refill loyalty card."
            );
        }

        // REFILL cannot mix with bottled water.
        if (productType == "REFILL" && hasBottledItem)
        {
            return BadRequest(
                "A refill cannot be added to an order that contains bottled water. " +
                "Please create a separate refill order."
            );
        }

        // BOTTLED cannot mix with refill.
        if (productType == "BOTTLED" && hasRefillItem)
        {
            return BadRequest(
                "Bottled water cannot be added to an order that contains a refill. " +
                "Please create a separate bottled-water order."
            );
        }

        // =====================================================
        // CALCULATE PRICE
        // =====================================================

        decimal unitPrice;

        if (order.UsesLoyaltyFreeRefill)
        {
            unitPrice = 0m;
        }
        else if (productType == "REFILL")
        {
            unitPrice = 1.00m;
        }
        else if (productType == "REFILL_CARD")
        {
            // Fixed business price.
            unitPrice = 50.00m;
        }
        else
        {
            unitPrice = product.Price;
        }

        var subTotal =
            unitPrice * dto.Quantity;

        var orderItem = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = order.OrderId,
            ProductId = product.ProductId,
            Quantity = dto.Quantity,
            UnitPrice = unitPrice,
            SubTotal = subTotal
        };

        _context.OrderItems.Add(orderItem);

        await _context.SaveChangesAsync();

        await RecalculateOrderTotalAsync(orderItem.OrderId);

        var result = new OrderItemDto
        {
            OrderItemId = orderItem.OrderItemId,
            OrderId = orderItem.OrderId,
            ProductId = orderItem.ProductId,
            Quantity = orderItem.Quantity,
            UnitPrice = orderItem.UnitPrice,
            SubTotal = orderItem.SubTotal
        };

        return CreatedAtAction(
            nameof(GetOrderItem),
            new { id = orderItem.OrderItemId },
            result
        );
    }

    // =========================================================
    // PUT: api/OrderItems/{id}
    // =========================================================
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOrderItem(
        Guid id,
        OrderItemUpdateDto dto)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var orderItem = await _context.OrderItems
            .Include(oi => oi.Order)
            .FirstOrDefaultAsync(oi =>
                oi.OrderItemId == id &&
                oi.Order.Customer.UserId == userId.Value);

        if (orderItem == null)
        {
            return NotFound("Order item not found.");
        }

        if (orderItem.Order.OrderStatus == "COMPLETED" ||
            orderItem.Order.OrderStatus == "CANCELLED")
        {
            return BadRequest(
                "Items cannot be updated on a completed or cancelled order."
            );
        }

        if (dto.Quantity <= 0)
        {
            return BadRequest(
                "Quantity must be greater than zero."
            );
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p =>
                p.ProductId == orderItem.ProductId &&
                p.IsActive);

        if (product == null)
        {
            return BadRequest(
                "Product does not exist or is inactive."
            );
        }

        var productType = string.IsNullOrWhiteSpace(product.ProductType)
            ? "BOTTLED"
            : product.ProductType.Trim().ToUpperInvariant();

        if (productType != "BOTTLED" &&
            productType != "REFILL" &&
            productType != "REFILL_CARD")
        {
            return BadRequest(
                "Invalid product type."
            );
        }

        if (productType != "REFILL_CARD" &&
            dto.Quantity > product.StockQuantity)
        {
            return BadRequest(
                "Not enough stock available."
            );
        }

        // =====================================================
        // REFILL
        // =====================================================
        if (productType == "REFILL" &&
            orderItem.Order.OrderType != "COLLECTION")
        {
            return BadRequest(
                "Refill orders are COLLECTION only."
            );
        }

        // =====================================================
        // REFILL CARD
        // =====================================================
        if (productType == "REFILL_CARD")
        {
            if (orderItem.Order.OrderType != "COLLECTION")
            {
                return BadRequest(
                    "The refill loyalty card must be purchased as a COLLECTION order."
                );
            }

            if (dto.Quantity != 1)
            {
                return BadRequest(
                    "A refill loyalty card can only have quantity 1."
                );
            }

            if (orderItem.Order.UsesLoyaltyFreeRefill)
            {
                return BadRequest(
                    "A loyalty card cannot be part of a free refill order."
                );
            }

            var hasActiveCard =
                await _context.RefillLoyaltyCards
                    .AnyAsync(c =>
                        c.CustomerId == orderItem.Order.CustomerId &&
                        c.IsActive);

            if (hasActiveCard)
            {
                return BadRequest(
                    "You already have an active refill loyalty card."
                );
            }

            // Fixed R50 price.
            orderItem.UnitPrice = 50.00m;
        }
        // =====================================================
        // LOYALTY FREE REFILL
        // =====================================================
        else if (orderItem.Order.UsesLoyaltyFreeRefill)
        {
            if (orderItem.Order.OrderType != "COLLECTION")
            {
                return BadRequest(
                    "A loyalty free refill must be a COLLECTION order."
                );
            }

            if (productType != "REFILL")
            {
                return BadRequest(
                    "A loyalty free refill order can only contain a REFILL product."
                );
            }

            if (dto.Quantity != 5)
            {
                return BadRequest(
                    "A loyalty free refill must remain exactly 5 litres."
                );
            }

            orderItem.UnitPrice = 0m;
        }
        // =====================================================
        // NORMAL REFILL
        // =====================================================
        else if (productType == "REFILL")
        {
            orderItem.UnitPrice = 1.00m;
        }
        // =====================================================
        // NORMAL BOTTLED PRODUCT
        // =====================================================
        else
        {
            orderItem.UnitPrice = product.Price;
        }

        orderItem.Quantity = dto.Quantity;

        orderItem.SubTotal =
            orderItem.UnitPrice *
            orderItem.Quantity;

        await _context.SaveChangesAsync();

        await RecalculateOrderTotalAsync(
            orderItem.OrderId
        );

        return NoContent();
    }

    // =========================================================
    // DELETE: api/OrderItems/{id}
    // =========================================================
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrderItem(Guid id)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var orderItem = await _context.OrderItems
            .Include(oi => oi.Order)
            .FirstOrDefaultAsync(oi =>
                oi.OrderItemId == id &&
                oi.Order.Customer.UserId == userId.Value);

        if (orderItem == null)
        {
            return NotFound("Order item not found.");
        }

        if (orderItem.Order.OrderStatus == "COMPLETED" ||
            orderItem.Order.OrderStatus == "CANCELLED")
        {
            return BadRequest(
                "Items cannot be deleted from a completed or cancelled order."
            );
        }

        var orderId = orderItem.OrderId;

        _context.OrderItems.Remove(orderItem);

        await _context.SaveChangesAsync();

        await RecalculateOrderTotalAsync(orderId);

        return NoContent();
    }

    // =========================================================
    // RECALCULATE ORDER TOTAL
    // =========================================================
    private async Task RecalculateOrderTotalAsync(
        Guid orderId)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o =>
                o.OrderId == orderId);

        if (order == null)
        {
            return;
        }

        var itemSubtotal =
            await _context.OrderItems
                .Where(oi =>
                    oi.OrderId == orderId)
                .SumAsync(oi =>
                    (decimal?)oi.SubTotal) ?? 0m;

        var hasRefillItem =
            await _context.OrderItems
                .AnyAsync(oi =>
                    oi.OrderId == orderId &&
                    oi.Product.ProductType == "REFILL");

        if (hasRefillItem)
        {
            order.DeliveryFee = 0m;
            order.DeliveryDistanceKm = null;
        }

        order.TotalAmount =
            itemSubtotal +
            order.DeliveryFee;

        order.UpdatedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    // =========================================================
    // GET JWT USER ID
    // =========================================================
    private Guid? GetUserId()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (Guid.TryParse(
            userId,
            out var parsedUserId))
        {
            return parsedUserId;
        }

        return null;
    }
}