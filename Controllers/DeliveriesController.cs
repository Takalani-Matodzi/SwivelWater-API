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
public class DeliveriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DeliveriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET ALL DELIVERIES
    //
    // CUSTOMER -> own deliveries
    // ADMIN    -> all deliveries
    // DRIVER   -> only assigned deliveries
    // =========================================================
    [HttpGet]
    [Authorize(Roles = "CUSTOMER,ADMIN,EMPLOYEE")]
    public async Task<ActionResult<IEnumerable<DeliveryDto>>> GetDeliveries()
    {
        if (User.IsInRole("ADMIN"))
        {
            var allDeliveries = await _context.Deliveries
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => MapDelivery(d))
                .ToListAsync();

            return Ok(allDeliveries);
        }

        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        if (User.IsInRole("CUSTOMER"))
        {
            var customerDeliveries = await _context.Deliveries
                .Where(d => d.Order.Customer.UserId == userId.Value)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => MapDelivery(d))
                .ToListAsync();

            return Ok(customerDeliveries);
        }

        // Employee / Driver
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == userId.Value);

        if (employee == null)
        {
            return Forbid();
        }

        // Only DRIVER employees are allowed to work deliveries.
        if (employee.Role != "DRIVER")
        {
            return Ok(new List<DeliveryDto>());
        }

        var driverDeliveries = await _context.Deliveries
            .Where(d => d.EmployeeId == employee.EmployeeId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => MapDelivery(d))
            .ToListAsync();

        return Ok(driverDeliveries);
    }

    // =========================================================
    // DRIVER DASHBOARD
    //
    // DRIVER -> only their assigned deliveries
    //
    // Includes:
    // - Driver information
    // - Today's delivery summary
    // - Customer information
    // - Delivery address
    // - Order information
    // - Ordered products
    // =========================================================
    [HttpGet("driver-dashboard")]
    [Authorize(Roles = "EMPLOYEE")]
    public async Task<IActionResult> GetDriverDashboard()
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == userId.Value);

        if (employee == null || employee.Role != "DRIVER")
        {
            return Forbid();
        }

        var deliveries = await _context.Deliveries
            .Include(d => d.Order)
                .ThenInclude(o => o.Customer)
            .Include(d => d.Order)
                .ThenInclude(o => o.Address)
            .Include(d => d.Order)
                .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
            .Where(d => d.EmployeeId == employee.EmployeeId)
            .OrderByDescending(d => d.ScheduledDate ?? d.CreatedAt)
            .ToListAsync();

        var result = deliveries.Select(d => new
        {
            deliveryId = d.DeliveryId,
            orderId = d.OrderId,
            deliveryStatus = d.DeliveryStatus,
            scheduledDate = d.ScheduledDate,
            deliveredAt = d.DeliveredAt,
            notes = d.Notes,

            customer = new
            {
                customerId = d.Order.Customer.CustomerId,
                firstName = d.Order.Customer.FirstName,
                lastName = d.Order.Customer.LastName,
                phone = d.Order.Customer.Phone
            },

            address = new
            {
                addressId = d.Order.Address.AddressId,
                addressLine1 = d.Order.Address.AddressLine1,
                addressLine2 = d.Order.Address.AddressLine2,
                city = d.Order.Address.City,
                province = d.Order.Address.Province,
                postalCode = d.Order.Address.PostalCode,
                country = d.Order.Address.Country
            },

            order = new
            {
                orderType = d.Order.OrderType,
                orderStatus = d.Order.OrderStatus,
                totalAmount = d.Order.TotalAmount,

                items = d.Order.OrderItems
                    .Select(item => new
                    {
                        productId = item.ProductId,
                        productName = item.Product.ProductName,
                        quantity = item.Quantity,
                        unitPrice = item.UnitPrice,
                        subTotal = item.SubTotal
                    })
                    .ToList()
            }
        }).ToList();

        // =====================================================
        // TODAY'S SUMMARY
        // =====================================================

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var todayDeliveries = deliveries
            .Where(d =>
                d.ScheduledDate.HasValue &&
                d.ScheduledDate.Value >= today &&
                d.ScheduledDate.Value < tomorrow)
            .ToList();

        var completedToday = todayDeliveries
            .Count(d => d.DeliveryStatus == "DELIVERED");

        var remainingToday = todayDeliveries
            .Count(d =>
                d.DeliveryStatus != "DELIVERED" &&
                d.DeliveryStatus != "CANCELLED");

        return Ok(new
        {
            driver = new
            {
                employeeId = employee.EmployeeId,
                employeeNumber = employee.EmployeeNumber,
                firstName = employee.FirstName,
                lastName = employee.LastName,
                role = employee.Role,
                isActive = employee.IsActive
            },

            summary = new
            {
                todayDeliveries = todayDeliveries.Count,
                completedToday,
                remainingToday
            },

            deliveries = result
        });
    }

    // =========================================================
    // GET ONE DELIVERY
    //
    // CUSTOMER -> own delivery
    // ADMIN    -> any delivery
    // DRIVER   -> assigned delivery only
    // =========================================================
    [HttpGet("{id}")]
    [Authorize(Roles = "CUSTOMER,ADMIN,EMPLOYEE")]
    public async Task<ActionResult<DeliveryDto>> GetDelivery(Guid id)
    {
        var delivery = await _context.Deliveries
            .FirstOrDefaultAsync(d => d.DeliveryId == id);

        if (delivery == null)
        {
            return NotFound("Delivery not found.");
        }

        // Admin can view anything.
        if (User.IsInRole("ADMIN"))
        {
            return Ok(MapDelivery(delivery));
        }

        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        // Customer can only see their own order delivery.
        if (User.IsInRole("CUSTOMER"))
        {
            var belongsToCustomer = await _context.Deliveries
                .AnyAsync(d =>
                    d.DeliveryId == id &&
                    d.Order.Customer.UserId == userId.Value);

            if (!belongsToCustomer)
            {
                return Forbid();
            }

            return Ok(MapDelivery(delivery));
        }

        // Employee / Driver
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == userId.Value);

        if (employee == null || employee.Role != "DRIVER")
        {
            return Forbid();
        }

        if (delivery.EmployeeId != employee.EmployeeId)
        {
            return Forbid();
        }

        return Ok(MapDelivery(delivery));
    }

    // =========================================================
    // CREATE DELIVERY
    //
    // ADMIN ONLY
    //
    // Requirements:
    // - Order must exist
    // - Must be DELIVERY order
    // - Must not be cancelled
    // - Must not already be completed
    // - Must be fully paid
    // - Order cannot already have a delivery
    // - Assigned employee must be an active DRIVER
    // =========================================================
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<DeliveryDto>> CreateDelivery(
        DeliveryCreateDto dto)
    {
        var order = await _context.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId);

        if (order == null)
        {
            return NotFound("Order not found.");
        }

        if (order.OrderType != "DELIVERY")
        {
            return BadRequest(
                "A delivery can only be created for a DELIVERY order.");
        }

        if (order.OrderStatus == "CANCELLED")
        {
            return BadRequest(
                "Cannot create a delivery for a cancelled order.");
        }

        if (order.OrderStatus == "COMPLETED")
        {
            return BadRequest(
                "Cannot create a delivery for an already completed order.");
        }

        if (order.TotalAmount <= 0)
        {
            return BadRequest(
                "Cannot create a delivery for an order with a zero total.");
        }

        // Calculate confirmed paid amount.
        var paidAmount = order.Payments
            .Where(p => p.PaymentStatus == "PAID")
            .Sum(p => p.Amount);

        if (paidAmount < order.TotalAmount)
        {
            return BadRequest(
                $"The order must be fully paid before delivery is created. " +
                $"Paid: R {paidAmount:N2}, Required: R {order.TotalAmount:N2}.");
        }

        var existingDelivery = await _context.Deliveries
            .AnyAsync(d => d.OrderId == dto.OrderId);

        if (existingDelivery)
        {
            return BadRequest(
                "This order already has a delivery.");
        }

        // Validate assigned driver.
        if (dto.EmployeeId.HasValue)
        {
            var driver = await _context.Employees
                .FirstOrDefaultAsync(e =>
                    e.EmployeeId == dto.EmployeeId.Value);

            if (driver == null)
            {
                return BadRequest("Employee does not exist.");
            }

            if (driver.Role != "DRIVER")
            {
                return BadRequest(
                    "Only employees with the DRIVER role can be assigned to deliveries.");
            }

            if (!driver.IsActive)
            {
                return BadRequest(
                    "The selected driver is inactive.");
            }
        }

        var delivery = new Delivery
        {
            DeliveryId = Guid.NewGuid(),
            OrderId = order.OrderId,
            EmployeeId = dto.EmployeeId,
            DeliveryStatus = dto.ScheduledDate.HasValue
                ? "SCHEDULED"
                : "PENDING",
            ScheduledDate = dto.ScheduledDate,
            DeliveredAt = null,
            Notes = string.IsNullOrWhiteSpace(dto.Notes)
                ? null
                : dto.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Deliveries.Add(delivery);

        // Creating a delivery means the order has entered
        // the fulfillment/processing stage.
        order.OrderStatus = "PROCESSING";
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetDelivery),
            new { id = delivery.DeliveryId },
            MapDelivery(delivery)
        );
    }

    // =========================================================
    // UPDATE DELIVERY
    //
    // ADMIN:
    // - Assign/change driver
    // - Schedule
    // - Status
    // - Notes
    //
    // DRIVER:
    // - Only their own assigned delivery
    // - Can move to OUT_FOR_DELIVERY or DELIVERED
    // - Cannot change driver
    // - Cannot reschedule
    // =========================================================
    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN,EMPLOYEE")]
    public async Task<IActionResult> UpdateDelivery(
        Guid id,
        DeliveryUpdateDto dto)
    {
        var delivery = await _context.Deliveries
            .FirstOrDefaultAsync(d => d.DeliveryId == id);

        if (delivery == null)
        {
            return NotFound("Delivery not found.");
        }

        // Load the order so the order lifecycle can stay
        // synchronized with the delivery lifecycle.
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == delivery.OrderId);

        if (order == null)
        {
            return BadRequest("Associated order not found.");
        }

        var newStatus = dto.DeliveryStatus
            .Trim()
            .ToUpperInvariant();

        var allowedStatuses = new[]
        {
            "PENDING",
            "SCHEDULED",
            "OUT_FOR_DELIVERY",
            "DELIVERED",
            "CANCELLED"
        };

        if (!allowedStatuses.Contains(newStatus))
        {
            return BadRequest("Invalid delivery status.");
        }

        // =====================================================
        // ADMIN
        // =====================================================
        if (User.IsInRole("ADMIN"))
        {
            if (newStatus == "DELIVERED")
            {
                delivery.DeliveredAt =
                    dto.DeliveredAt ?? DateTime.UtcNow;
            }
            else
            {
                delivery.DeliveredAt = dto.DeliveredAt;
            }

            if (dto.EmployeeId.HasValue)
            {
                var driver = await _context.Employees
                    .FirstOrDefaultAsync(e =>
                        e.EmployeeId == dto.EmployeeId.Value);

                if (driver == null)
                {
                    return BadRequest(
                        "Employee does not exist.");
                }

                if (driver.Role != "DRIVER")
                {
                    return BadRequest(
                        "Only employees with the DRIVER role can be assigned.");
                }

                if (!driver.IsActive)
                {
                    return BadRequest(
                        "The selected driver is inactive.");
                }
            }

            delivery.EmployeeId = dto.EmployeeId;
            delivery.DeliveryStatus = newStatus;
            delivery.ScheduledDate = dto.ScheduledDate;
            delivery.Notes = string.IsNullOrWhiteSpace(dto.Notes)
                ? null
                : dto.Notes.Trim();
            delivery.UpdatedAt = DateTime.UtcNow;

            // Keep OrderStatus synchronized with DeliveryStatus.
            if (newStatus == "OUT_FOR_DELIVERY")
            {
                order.OrderStatus = "PROCESSING";
            }
            else if (newStatus == "DELIVERED")
            {
                order.OrderStatus = "COMPLETED";
            }

            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =====================================================
        // DRIVER
        // =====================================================
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == userId.Value);

        if (employee == null || employee.Role != "DRIVER")
        {
            return Forbid();
        }

        if (delivery.EmployeeId != employee.EmployeeId)
        {
            return Forbid();
        }

        // Driver cannot modify a cancelled or completed delivery.
        if (delivery.DeliveryStatus == "CANCELLED")
        {
            return BadRequest(
                "A cancelled delivery cannot be updated.");
        }

        if (delivery.DeliveryStatus == "DELIVERED")
        {
            return BadRequest(
                "A delivered order cannot be updated.");
        }

        // Driver can only perform operational status changes.
        if (newStatus != "OUT_FOR_DELIVERY" &&
            newStatus != "DELIVERED")
        {
            return BadRequest(
                "Drivers can only mark deliveries as OUT_FOR_DELIVERY or DELIVERED.");
        }

        // Driver cannot reassign themselves or change the schedule.
        if (dto.EmployeeId.HasValue &&
            dto.EmployeeId.Value != employee.EmployeeId)
        {
            return BadRequest(
                "Drivers cannot reassign deliveries.");
        }

        if (newStatus == "DELIVERED")
        {
            delivery.DeliveredAt =
                dto.DeliveredAt ?? DateTime.UtcNow;
        }

        delivery.DeliveryStatus = newStatus;

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            delivery.Notes = dto.Notes.Trim();
        }

        delivery.UpdatedAt = DateTime.UtcNow;

        // Keep OrderStatus synchronized with DeliveryStatus.
        if (newStatus == "OUT_FOR_DELIVERY")
        {
            order.OrderStatus = "PROCESSING";
        }
        else if (newStatus == "DELIVERED")
        {
            order.OrderStatus = "COMPLETED";
        }

        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // =========================================================
    // DELETE DELIVERY
    //
    // ADMIN ONLY
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> DeleteDelivery(Guid id)
    {
        var delivery = await _context.Deliveries
            .FirstOrDefaultAsync(d => d.DeliveryId == id);

        if (delivery == null)
        {
            return NotFound("Delivery not found.");
        }

        if (delivery.DeliveryStatus == "OUT_FOR_DELIVERY")
        {
            return BadRequest(
                "A delivery that is out for delivery cannot be deleted.");
        }

        if (delivery.DeliveryStatus == "DELIVERED")
        {
            return BadRequest(
                "A delivered delivery cannot be deleted.");
        }

        _context.Deliveries.Remove(delivery);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private Guid? GetUserId()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (Guid.TryParse(userId, out var parsedUserId))
        {
            return parsedUserId;
        }

        return null;
    }

    private static DeliveryDto MapDelivery(Delivery delivery)
    {
        return new DeliveryDto
        {
            DeliveryId = delivery.DeliveryId,
            OrderId = delivery.OrderId,
            EmployeeId = delivery.EmployeeId,
            DeliveryStatus = delivery.DeliveryStatus,
            ScheduledDate = delivery.ScheduledDate,
            DeliveredAt = delivery.DeliveredAt,
            Notes = delivery.Notes,
            CreatedAt = delivery.CreatedAt,
            UpdatedAt = delivery.UpdatedAt
        };
    }
}