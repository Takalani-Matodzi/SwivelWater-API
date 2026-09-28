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
public class OrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OrdersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET ALL ORDERS
    //
    // CUSTOMER -> own orders
    // ADMIN    -> all orders
    // =========================================================
    [HttpGet]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetOrders()
    {
        if (User.IsInRole("ADMIN"))
        {
            var allOrders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    CustomerId = o.CustomerId,
                    AddressId = o.AddressId,
                    OrderDate = o.OrderDate,
                    OrderType = o.OrderType,
                    OrderStatus = o.OrderStatus,
                    UsesLoyaltyFreeRefill = o.UsesLoyaltyFreeRefill,
                    TotalAmount = o.TotalAmount,
                    DeliveryFee = o.DeliveryFee,
                    DeliveryDistanceKm = o.DeliveryDistanceKm,
                    Notes = o.Notes,
                    CreatedAt = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt
                })
                .ToListAsync();

            return Ok(allOrders);
        }

        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var orders = await _context.Orders
            .Where(o => o.Customer.UserId == userId.Value)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new OrderDto
            {
                OrderId = o.OrderId,
                CustomerId = o.CustomerId,
                AddressId = o.AddressId,
                OrderDate = o.OrderDate,
                OrderType = o.OrderType,
                OrderStatus = o.OrderStatus,
                UsesLoyaltyFreeRefill = o.UsesLoyaltyFreeRefill,
                TotalAmount = o.TotalAmount,
                DeliveryFee = o.DeliveryFee,
                DeliveryDistanceKm = o.DeliveryDistanceKm,
                Notes = o.Notes,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt
            })
            .ToListAsync();

        return Ok(orders);
    }

    // =========================================================
    // GET ONE ORDER
    //
    // CUSTOMER -> own order
    // ADMIN    -> any order
    // =========================================================
    [HttpGet("{id}")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<OrderDto>> GetOrder(Guid id)
    {
        if (User.IsInRole("ADMIN"))
        {
            var adminOrder = await _context.Orders
                .Where(o => o.OrderId == id)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    CustomerId = o.CustomerId,
                    AddressId = o.AddressId,
                    OrderDate = o.OrderDate,
                    OrderType = o.OrderType,
                    OrderStatus = o.OrderStatus,
                    UsesLoyaltyFreeRefill = o.UsesLoyaltyFreeRefill,
                    TotalAmount = o.TotalAmount,
                    DeliveryFee = o.DeliveryFee,
                    DeliveryDistanceKm = o.DeliveryDistanceKm,
                    Notes = o.Notes,
                    CreatedAt = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (adminOrder == null)
            {
                return NotFound("Order not found.");
            }

            return Ok(adminOrder);
        }

        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var order = await _context.Orders
            .Where(o =>
                o.OrderId == id &&
                o.Customer.UserId == userId.Value)
            .Select(o => new OrderDto
            {
                OrderId = o.OrderId,
                CustomerId = o.CustomerId,
                AddressId = o.AddressId,
                OrderDate = o.OrderDate,
                OrderType = o.OrderType,
                OrderStatus = o.OrderStatus,
                UsesLoyaltyFreeRefill = o.UsesLoyaltyFreeRefill,
                TotalAmount = o.TotalAmount,
                DeliveryFee = o.DeliveryFee,
                DeliveryDistanceKm = o.DeliveryDistanceKm,
                Notes = o.Notes,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound("Order not found.");
        }

        return Ok(order);
    }

    // =========================================================
    // CREATE ORDER
    //
    // CUSTOMER ONLY
    //
    // Loyalty free refill rules:
    // - COLLECTION only
    // - Active loyalty card required
    // - Free refill must be available
    // - Prevent unlimited pending loyalty reservations
    // =========================================================
    [HttpPost]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<ActionResult<OrderDto>> CreateOrder(
        OrderCreateDto dto)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(dto.OrderType))
        {
            return BadRequest("Order type is required.");
        }

        var orderType = dto.OrderType
            .Trim()
            .ToUpperInvariant();

        var allowedOrderTypes = new[]
        {
            "DELIVERY",
            "COLLECTION"
        };

        if (!allowedOrderTypes.Contains(orderType))
        {
            return BadRequest(
                "Order type must be DELIVERY or COLLECTION."
            );
        }

        // -----------------------------------------------------
        // Find customer belonging to logged-in user.
        // -----------------------------------------------------
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c =>
                c.UserId == userId.Value);

        if (customer == null)
        {
            return BadRequest(
                "Customer profile does not exist for this user."
            );
        }

        // -----------------------------------------------------
        // Validate address.
        // -----------------------------------------------------
        var addressExists = await _context.Addresses
            .AnyAsync(a =>
                a.AddressId == dto.AddressId &&
                a.CustomerId == customer.CustomerId);

        if (!addressExists)
        {
            return BadRequest(
                "Address does not exist or does not belong to this customer."
            );
        }

        // =====================================================
        // LOYALTY FREE REFILL VALIDATION
        // =====================================================
        if (dto.UseLoyaltyFreeRefill)
        {
            if (orderType != "COLLECTION")
            {
                return BadRequest(
                    "A loyalty free refill must be a COLLECTION order."
                );
            }

            var loyaltyCard =
                await _context.RefillLoyaltyCards
                    .FirstOrDefaultAsync(c =>
                        c.CustomerId == customer.CustomerId &&
                        c.IsActive);

            if (loyaltyCard == null)
            {
                return BadRequest(
                    "You do not have an active refill loyalty card."
                );
            }

            if (loyaltyCard.FreeRefillsAvailable <= 0)
            {
                return BadRequest(
                    "You do not have a free 5L refill available."
                );
            }

            var activeLoyaltyReservations =
                await _context.Orders.CountAsync(o =>
                    o.CustomerId == customer.CustomerId &&
                    o.UsesLoyaltyFreeRefill &&
                    (
                        o.OrderStatus == "PENDING" ||
                        o.OrderStatus == "READY_FOR_COLLECTION"
                    ));

            if (activeLoyaltyReservations >=
                loyaltyCard.FreeRefillsAvailable)
            {
                return BadRequest(
                    "You already have an active loyalty refill order."
                );
            }
        }

        var now = DateTime.UtcNow;

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            CustomerId = customer.CustomerId,
            AddressId = dto.AddressId,
            OrderType = orderType,
            UsesLoyaltyFreeRefill = dto.UseLoyaltyFreeRefill,
            OrderStatus = "PENDING",
            TotalAmount = 0m,
            DeliveryFee = 0m,
            DeliveryDistanceKm = null,
            Notes = string.IsNullOrWhiteSpace(dto.Notes)
                ? null
                : dto.Notes.Trim(),
            OrderDate = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Orders.Add(order);

        await _context.SaveChangesAsync();

        var result = new OrderDto
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            AddressId = order.AddressId,
            OrderDate = order.OrderDate,
            OrderType = order.OrderType,
            OrderStatus = order.OrderStatus,
            TotalAmount = order.TotalAmount,
            DeliveryFee = order.DeliveryFee,
            DeliveryDistanceKm = order.DeliveryDistanceKm,
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = order.OrderId },
            result
        );
    }

    // =========================================================
    // UPDATE ORDER
    // =========================================================
    [HttpPut("{id}")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<IActionResult> UpdateOrder(
        Guid id,
        OrderUpdateDto dto)
    {
        var order = await _context.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null)
        {
            return NotFound("Order not found.");
        }

        var requestedStatus =
            string.IsNullOrWhiteSpace(dto.OrderStatus)
                ? "PENDING"
                : dto.OrderStatus
                    .Trim()
                    .ToUpperInvariant();

        // =====================================================
        // ADMIN
        // =====================================================
        if (User.IsInRole("ADMIN"))
        {
            return await UpdateOrderAsAdmin(
                order,
                dto,
                requestedStatus);
        }

        // =====================================================
        // CUSTOMER
        // =====================================================
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var ownsOrder = await _context.Orders
            .AnyAsync(o =>
                o.OrderId == order.OrderId &&
                o.Customer.UserId == userId.Value);

        if (!ownsOrder)
        {
            return Forbid();
        }

        // -----------------------------------------------------
        // CUSTOMER CANCELLATION
        // -----------------------------------------------------
        if (requestedStatus == "CANCELLED")
        {
            if (order.OrderStatus == "COMPLETED")
            {
                return BadRequest(
                    "A completed order cannot be cancelled."
                );
            }

            if (order.OrderStatus == "CANCELLED")
            {
                return BadRequest(
                    "Order is already cancelled."
                );
            }

            if (order.OrderStatus == "PROCESSING")
            {
                return BadRequest(
                    "A processing order cannot be cancelled by the customer."
                );
            }

            if (order.OrderStatus == "READY_FOR_COLLECTION")
            {
                return BadRequest(
                    "An order ready for collection cannot be cancelled by the customer."
                );
            }

            var hasPaidPayment = order.Payments
                .Any(p => p.PaymentStatus == "PAID");

            if (hasPaidPayment)
            {
                return BadRequest(
                    "A paid order cannot be cancelled directly. " +
                    "Please contact Swivel Water support."
                );
            }

            order.OrderStatus = "CANCELLED";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // -----------------------------------------------------
        // CUSTOMER NORMAL EDIT
        // -----------------------------------------------------
        if (order.OrderStatus != "PENDING")
        {
            return BadRequest(
                "Only pending orders can be edited."
            );
        }

        var paid = order.Payments
            .Any(p => p.PaymentStatus == "PAID");

        if (paid)
        {
            return BadRequest(
                "A paid order cannot be edited."
            );
        }

        if (requestedStatus != "PENDING")
        {
            return BadRequest(
                "Customers can only keep an order in PENDING status " +
                "or request cancellation."
            );
        }

        var validationResult = await ValidateOrderDetails(
            order.CustomerId,
            dto.AddressId,
            dto.OrderType);

        if (validationResult != null)
        {
            return BadRequest(validationResult);
        }

        var requestedOrderType = dto.OrderType
            .Trim()
            .ToUpperInvariant();

        if (order.UsesLoyaltyFreeRefill &&
            requestedOrderType != "COLLECTION")
        {
            return BadRequest(
                "A loyalty free refill order must remain COLLECTION."
            );
        }

        var hasRefillItem = await _context.OrderItems
            .AnyAsync(oi =>
                oi.OrderId == order.OrderId &&
                oi.Product.ProductType == "REFILL");

        if (hasRefillItem &&
            requestedOrderType == "DELIVERY")
        {
            return BadRequest(
                "Refill orders are COLLECTION only. " +
                "A refill order cannot be changed to DELIVERY."
            );
        }

        order.AddressId = dto.AddressId;
        order.OrderType = requestedOrderType;

        if (requestedOrderType == "COLLECTION")
        {
            order.DeliveryFee = 0m;
            order.DeliveryDistanceKm = null;
        }

        order.Notes = string.IsNullOrWhiteSpace(dto.Notes)
            ? null
            : dto.Notes.Trim();

        order.OrderStatus = "PENDING";
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // =========================================================
    // DELETE ORDER
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<IActionResult> DeleteOrder(Guid id)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var order = await _context.Orders
            .FirstOrDefaultAsync(o =>
                o.OrderId == id &&
                o.Customer.UserId == userId.Value);

        if (order == null)
        {
            return NotFound("Order not found.");
        }

        if (order.OrderStatus != "PENDING")
        {
            return BadRequest(
                "Only pending orders can be deleted."
            );
        }

        var hasItems = await _context.OrderItems
            .AnyAsync(oi => oi.OrderId == order.OrderId);

        if (hasItems)
        {
            return BadRequest(
                "An order containing items cannot be deleted. " +
                "Cancel the order instead."
            );
        }

        var hasPayments = await _context.Payments
            .AnyAsync(p => p.OrderId == order.OrderId);

        if (hasPayments)
        {
            return BadRequest(
                "An order with payment activity cannot be deleted."
            );
        }

        var hasDelivery = await _context.Deliveries
            .AnyAsync(d => d.OrderId == order.OrderId);

        if (hasDelivery)
        {
            return BadRequest(
                "An order with delivery activity cannot be deleted."
            );
        }

        _context.Orders.Remove(order);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // =========================================================
    // ADMIN ORDER MANAGEMENT
    // =========================================================
    private async Task<IActionResult> UpdateOrderAsAdmin(
        Order order,
        OrderUpdateDto dto,
        string requestedStatus)
    {
        if (order.OrderStatus == "COMPLETED")
        {
            return BadRequest(
                "A completed order cannot be modified."
            );
        }

        // -----------------------------------------------------
        // CANCEL ORDER
        // -----------------------------------------------------
        if (requestedStatus == "CANCELLED")
        {
            if (order.OrderStatus == "CANCELLED")
            {
                return BadRequest(
                    "Order is already cancelled."
                );
            }

            var hasPaidPayment = order.Payments
                .Any(p => p.PaymentStatus == "PAID");

            if (hasPaidPayment)
            {
                return BadRequest(
                    "A paid order cannot be cancelled directly. " +
                    "Handle the payment/refund first."
                );
            }

            order.OrderStatus = "CANCELLED";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // -----------------------------------------------------
        // PENDING ORDER EDIT
        // -----------------------------------------------------
        if (requestedStatus == "PENDING")
        {
            if (order.OrderStatus != "PENDING")
            {
                return BadRequest(
                    "Only pending orders can be edited back to PENDING."
                );
            }

            var hasPaidPayment = order.Payments
                .Any(p => p.PaymentStatus == "PAID");

            if (hasPaidPayment)
            {
                return BadRequest(
                    "A paid order cannot have its core details changed."
                );
            }

            var validationResult = await ValidateOrderDetails(
                order.CustomerId,
                dto.AddressId,
                dto.OrderType);

            if (validationResult != null)
            {
                return BadRequest(validationResult);
            }

            var requestedOrderType = dto.OrderType
                .Trim()
                .ToUpperInvariant();

            if (order.UsesLoyaltyFreeRefill &&
                requestedOrderType != "COLLECTION")
            {
                return BadRequest(
                    "A loyalty free refill order must remain COLLECTION."
                );
            }

            var hasRefillItem = await _context.OrderItems
                .AnyAsync(oi =>
                    oi.OrderId == order.OrderId &&
                    oi.Product.ProductType == "REFILL");

            if (hasRefillItem &&
                requestedOrderType == "DELIVERY")
            {
                return BadRequest(
                    "Refill orders are COLLECTION only. " +
                    "A refill order cannot be changed to DELIVERY."
                );
            }

            order.AddressId = dto.AddressId;
            order.OrderType = requestedOrderType;

            if (requestedOrderType == "COLLECTION")
            {
                order.DeliveryFee = 0m;
                order.DeliveryDistanceKm = null;
            }

            order.Notes = string.IsNullOrWhiteSpace(dto.Notes)
                ? null
                : dto.Notes.Trim();

            order.OrderStatus = "PENDING";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // -----------------------------------------------------
        // COLLECTION -> READY_FOR_COLLECTION
        // -----------------------------------------------------
        if (requestedStatus == "READY_FOR_COLLECTION")
        {
            if (order.OrderType != "COLLECTION")
            {
                return BadRequest(
                    "Only COLLECTION orders can be marked READY_FOR_COLLECTION."
                );
            }

            if (order.OrderStatus != "PENDING")
            {
                return BadRequest(
                    "Only pending collection orders can become ready for collection."
                );
            }

            if (order.UsesLoyaltyFreeRefill)
            {
                var loyaltyValidation =
                    await ValidateLoyaltyFreeRefillOrderAsync(order);

                if (loyaltyValidation != null)
                {
                    return BadRequest(loyaltyValidation);
                }
            }
            else if (!IsFullyPaid(order))
            {
                return BadRequest(
                    "The collection order must be fully paid before it is ready for collection."
                );
            }

            order.OrderStatus = "READY_FOR_COLLECTION";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // -----------------------------------------------------
        // COLLECTION -> COMPLETED
        // -----------------------------------------------------
        if (requestedStatus == "COMPLETED")
        {
            if (order.OrderType != "COLLECTION")
            {
                return BadRequest(
                    "Only COLLECTION orders can be completed through order management."
                );
            }

            if (order.OrderStatus != "READY_FOR_COLLECTION")
            {
                return BadRequest(
                    "A collection order must be READY_FOR_COLLECTION before it can be completed."
                );
            }

            if (order.UsesLoyaltyFreeRefill)
            {
                var loyaltyValidation =
                    await ValidateLoyaltyFreeRefillOrderAsync(order);

                if (loyaltyValidation != null)
                {
                    return BadRequest(loyaltyValidation);
                }
            }
            else if (!IsFullyPaid(order))
            {
                return BadRequest(
                    "The collection order must be fully paid before completion."
                );
            }

            // =================================================
            // PROCESS REFILL / LOYALTY CARD
            // =================================================
            var loyaltyResult =
                await ProcessRefillLoyaltyAsync(order);

            if (loyaltyResult != null)
            {
                return BadRequest(loyaltyResult);
            }

            order.OrderStatus = "COMPLETED";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // -----------------------------------------------------
        // PROCESSING
        // -----------------------------------------------------
        if (requestedStatus == "PROCESSING")
        {
            return BadRequest(
                "PROCESSING for DELIVERY orders is controlled by delivery management."
            );
        }

        return BadRequest(
            "Invalid order status for admin management."
        );
    }

    // =========================================================
    // VALIDATE ORDER DETAILS
    // =========================================================
    private async Task<string?> ValidateOrderDetails(
        Guid customerId,
        Guid addressId,
        string orderType)
    {
        if (string.IsNullOrWhiteSpace(orderType))
        {
            return "Order type is required.";
        }

        var normalizedOrderType =
            orderType.Trim().ToUpperInvariant();

        var allowedOrderTypes = new[]
        {
            "DELIVERY",
            "COLLECTION"
        };

        if (!allowedOrderTypes.Contains(normalizedOrderType))
        {
            return "Order type must be DELIVERY or COLLECTION.";
        }

        var addressExists = await _context.Addresses
            .AnyAsync(a =>
                a.AddressId == addressId &&
                a.CustomerId == customerId);

        if (!addressExists)
        {
            return "Address does not exist or does not belong to this customer.";
        }

        return null;
    }

    // =========================================================
    // VALIDATE LOYALTY FREE REFILL ORDER
    // =========================================================
    private async Task<string?> ValidateLoyaltyFreeRefillOrderAsync(
        Order order)
    {
        if (!order.UsesLoyaltyFreeRefill)
        {
            return null;
        }

        if (order.OrderType != "COLLECTION")
        {
            return "A loyalty free refill must be a COLLECTION order.";
        }

        var loyaltyCard = await _context.RefillLoyaltyCards
            .FirstOrDefaultAsync(c =>
                c.CustomerId == order.CustomerId &&
                c.IsActive);

        if (loyaltyCard == null)
        {
            return "The customer does not have an active refill loyalty card.";
        }

        if (loyaltyCard.FreeRefillsAvailable <= 0)
        {
            return "The customer does not have a free 5L refill available.";
        }

        var items = await _context.OrderItems
            .Include(oi => oi.Product)
            .Where(oi => oi.OrderId == order.OrderId)
            .ToListAsync();

        if (items.Count != 1)
        {
            return "A loyalty free refill order must contain exactly one refill item.";
        }

        var item = items[0];

        var productType =
            string.IsNullOrWhiteSpace(item.Product.ProductType)
                ? "BOTTLED"
                : item.Product.ProductType
                    .Trim()
                    .ToUpperInvariant();

        if (productType != "REFILL")
        {
            return "A loyalty free refill order must contain a REFILL product.";
        }

        if (item.Quantity != 5)
        {
            return "A loyalty free refill must be exactly 5 litres.";
        }

        if (item.UnitPrice != 0m)
        {
            return "A loyalty free refill must have a zero item price.";
        }

        if (item.SubTotal != 0m)
        {
            return "A loyalty free refill must have a zero subtotal.";
        }

        if (order.TotalAmount != 0m)
        {
            return "A loyalty free refill order must have a total amount of R0.";
        }

        return null;
    }

    // =========================================================
    // PROCESS REFILL / LOYALTY CARD WHEN ORDER IS COMPLETED
    //
    // REFILL CARD PURCHASE:
    // - Creates the customer's digital loyalty card.
    // - Starts at zero ticks.
    //
    // NORMAL PAID REFILL:
    // - No loyalty card required.
    // - If active card exists, every 5 paid litres = 1 tick.
    //
    // FREE LOYALTY REFILL:
    // - Exactly 5L.
    // - Consumes one free refill.
    // - Earns no new tick.
    // =========================================================
    private async Task<string?> ProcessRefillLoyaltyAsync(
        Order order)
    {
        // =====================================================
        // LOYALTY CARD PURCHASE
        // =====================================================
        var loyaltyCardItem = await _context.OrderItems
            .Include(oi => oi.Product)
            .FirstOrDefaultAsync(oi =>
                oi.OrderId == order.OrderId &&
                oi.Product.ProductType == "REFILL_CARD");

        if (loyaltyCardItem != null)
        {
            if (loyaltyCardItem.Quantity != 1)
            {
                return "A refill loyalty card purchase must have quantity 1.";
            }

            var existingCard =
                await _context.RefillLoyaltyCards
                    .FirstOrDefaultAsync(c =>
                        c.CustomerId == order.CustomerId &&
                        c.IsActive);

            if (existingCard != null)
            {
                return "The customer already has an active refill loyalty card.";
            }

            var newCard = new RefillLoyaltyCard
            {
                RefillLoyaltyCardId = Guid.NewGuid(),
                CustomerId = order.CustomerId,
                TickCount = 0,
                FreeRefillsAvailable = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.RefillLoyaltyCards.Add(newCard);

            return null;
        }

        // =====================================================
        // FREE LOYALTY REFILL
        // =====================================================
        if (order.UsesLoyaltyFreeRefill)
        {
            var loyaltyCard =
                await _context.RefillLoyaltyCards
                    .FirstOrDefaultAsync(c =>
                        c.CustomerId == order.CustomerId &&
                        c.IsActive);

            if (loyaltyCard == null)
            {
                return "The customer does not have an active refill loyalty card.";
            }

            if (loyaltyCard.FreeRefillsAvailable <= 0)
            {
                return "The customer does not have a free 5L refill available.";
            }

            var refillItem = await _context.OrderItems
                .Include(oi => oi.Product)
                .FirstOrDefaultAsync(oi =>
                    oi.OrderId == order.OrderId &&
                    oi.Product.ProductType == "REFILL");

            if (refillItem == null)
            {
                return "The loyalty order does not contain a refill item.";
            }

            if (refillItem.Quantity != 5)
            {
                return "A loyalty free refill must be exactly 5 litres.";
            }

            // Consume one earned free refill.
            loyaltyCard.FreeRefillsAvailable--;
            loyaltyCard.UpdatedAt = DateTime.UtcNow;

            _context.RefillLoyaltyTransactions.Add(
                new RefillLoyaltyTransaction
                {
                    RefillLoyaltyTransactionId = Guid.NewGuid(),
                    RefillLoyaltyCardId =
                        loyaltyCard.RefillLoyaltyCardId,
                    OrderId = order.OrderId,
                    OrderItemId = refillItem.OrderItemId,
                    TransactionType = "FREE_REFILL_REDEEMED",
                    Litres = 5,
                    TicksAdded = 0,
                    FreeRefillsAdded = 0,
                    FreeRefillsUsed = 1,
                    CreatedAt = DateTime.UtcNow
                });

            return null;
        }

        // =====================================================
        // NORMAL PAID REFILL
        // =====================================================
        var paidRefillItems = await _context.OrderItems
            .Include(oi => oi.Product)
            .Where(oi =>
                oi.OrderId == order.OrderId &&
                oi.Product.ProductType == "REFILL" &&
                oi.UnitPrice > 0)
            .ToListAsync();

        // Not a refill order.
        if (paidRefillItems.Count == 0)
        {
            return null;
        }

        // Loyalty card is optional.
        var activeCard =
            await _context.RefillLoyaltyCards
                .FirstOrDefaultAsync(c =>
                    c.CustomerId == order.CustomerId &&
                    c.IsActive);

        // Customer bought a normal refill without a loyalty card.
        if (activeCard == null)
        {
            return null;
        }

        var totalLitres =
            paidRefillItems.Sum(oi => oi.Quantity);

        // Every 5 paid litres = 1 tick.
        var ticksEarned =
            totalLitres / 5;

        if (ticksEarned <= 0)
        {
            return null;
        }

        var combinedTicks =
            activeCard.TickCount + ticksEarned;

        var freeRefillsEarned =
            combinedTicks / 10;

        var newTickCount =
            combinedTicks % 10;

        activeCard.TickCount =
            newTickCount;

        activeCard.FreeRefillsAvailable +=
            freeRefillsEarned;

        activeCard.UpdatedAt =
            DateTime.UtcNow;

        var firstRefillItem =
            paidRefillItems
                .OrderBy(oi => oi.OrderItemId)
                .First();

        _context.RefillLoyaltyTransactions.Add(
            new RefillLoyaltyTransaction
            {
                RefillLoyaltyTransactionId =
                    Guid.NewGuid(),

                RefillLoyaltyCardId =
                    activeCard.RefillLoyaltyCardId,

                OrderId =
                    order.OrderId,

                OrderItemId =
                    firstRefillItem.OrderItemId,

                TransactionType =
                    "TICK_EARNED",

                Litres =
                    totalLitres,

                TicksAdded =
                    ticksEarned,

                FreeRefillsAdded =
                    freeRefillsEarned,

                FreeRefillsUsed =
                    0,

                CreatedAt =
                    DateTime.UtcNow
            });

        return null;
    }

    // =========================================================
    // FULL PAYMENT CHECK
    // =========================================================
    private static bool IsFullyPaid(Order order)
    {
        if (order.TotalAmount <= 0)
        {
            return false;
        }

        var paidAmount = order.Payments
            .Where(p => p.PaymentStatus == "PAID")
            .Sum(p => p.Amount);

        return paidAmount >= order.TotalAmount;
    }

    // =========================================================
    // GET JWT USER ID
    // =========================================================
    private Guid? GetUserId()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (Guid.TryParse(
            userId,
            out var parsedUserId))
        {
            return parsedUserId;
        }

        return null;
    }
}