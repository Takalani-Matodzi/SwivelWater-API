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
public class PaymentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PaymentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET ALL PAYMENTS
    //
    // CUSTOMER -> own payments
    // ADMIN    -> all payments
    // =========================================================
    [HttpGet]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<IEnumerable<PaymentDto>>> GetPayments()
    {
        var userId = GetUserId();

        if (User.IsInRole("ADMIN"))
        {
            var allPayments = await _context.Payments
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => MapPayment(p))
                .ToListAsync();

            return Ok(allPayments);
        }

        if (userId == null)
        {
            return Unauthorized();
        }

        var payments = await _context.Payments
            .Where(p => p.Order.Customer.UserId == userId.Value)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => MapPayment(p))
            .ToListAsync();

        return Ok(payments);
    }

    // =========================================================
    // GET PAYMENT
    //
    // CUSTOMER -> own payment
    // ADMIN    -> any payment
    // =========================================================
    [HttpGet("{id}")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<PaymentDto>> GetPayment(Guid id)
    {
        var payment = await _context.Payments
            .Include(p => p.Order)
                .ThenInclude(o => o.Customer)
                    .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(p => p.PaymentId == id);

        if (payment == null)
        {
            return NotFound("Payment not found.");
        }

        if (User.IsInRole("ADMIN"))
        {
            return Ok(MapPayment(payment));
        }

        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        if (payment.Order.Customer.UserId != userId.Value)
        {
            return Forbid();
        }

        return Ok(MapPayment(payment));
    }

    // =========================================================
    // CREATE PAYMENT
    //
    // CUSTOMER ONLY
    //
    // Customer can initiate a payment.
    // Payment is ALWAYS created as PENDING.
    // =========================================================
    [HttpPost]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<ActionResult<PaymentDto>> CreatePayment(
        PaymentCreateDto dto)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        if (dto.Amount <= 0)
        {
            return BadRequest(
                "Payment amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(dto.PaymentMethod))
        {
            return BadRequest(
                "Payment method is required.");
        }

        // Make sure the order belongs to the logged-in customer.
        var order = await _context.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o =>
                o.OrderId == dto.OrderId &&
                o.Customer.UserId == userId.Value);

        if (order == null)
        {
            return Forbid();
        }

        if (order.OrderStatus == "CANCELLED")
        {
            return BadRequest(
                "Cannot make a payment for a cancelled order.");
        }

        if (order.OrderStatus == "COMPLETED")
        {
            return BadRequest(
                "This order has already been completed.");
        }

        if (order.OrderStatus == "READY_FOR_COLLECTION")
        {
            return BadRequest(
                "This collection order is already ready for collection.");
        }

        if (order.TotalAmount <= 0)
        {
            return BadRequest(
                "Cannot make a payment because the order total is zero.");
        }

        // Prevent multiple simultaneous pending payments
        // for the same order.
        var pendingPaymentExists = order.Payments
            .Any(p => p.PaymentStatus == "PENDING");

        if (pendingPaymentExists)
        {
            return BadRequest(
                "This order already has a pending payment.");
        }

        // Calculate what is still outstanding.
        var alreadyPaid = order.Payments
            .Where(p => p.PaymentStatus == "PAID")
            .Sum(p => p.Amount);

        var outstandingAmount = order.TotalAmount - alreadyPaid;

        if (outstandingAmount <= 0)
        {
            return BadRequest(
                "This order has already been fully paid.");
        }

        if (dto.Amount > outstandingAmount)
        {
            return BadRequest(
                $"Payment amount cannot exceed the outstanding amount of R {outstandingAmount:N2}.");
        }

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Amount = dto.Amount,
            PaymentMethod = dto.PaymentMethod.Trim().ToUpperInvariant(),

            // Customer payment attempts always start here.
            PaymentStatus = "PENDING",

            // Never trust a customer-supplied transaction reference.
            TransactionReference = null,

            PaymentDate = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);

        await _context.SaveChangesAsync();

        var result = MapPayment(payment);

        return CreatedAtAction(
            nameof(GetPayment),
            new { id = payment.PaymentId },
            result
        );
    }

    // =========================================================
    // UPDATE PAYMENT
    //
    // ADMIN ONLY
    //
    // This is the trusted side of payment processing.
    // =========================================================
    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> UpdatePayment(
        Guid id,
        PaymentUpdateDto dto)
    {
        var payment = await _context.Payments
            .Include(p => p.Order)
                .ThenInclude(o => o.Payments)
            .FirstOrDefaultAsync(p => p.PaymentId == id);

        if (payment == null)
        {
            return NotFound("Payment not found.");
        }

        if (string.IsNullOrWhiteSpace(dto.PaymentStatus))
        {
            return BadRequest("Payment status is required.");
        }

        var currentStatus = payment.PaymentStatus
            .Trim()
            .ToUpperInvariant();

        var newStatus = dto.PaymentStatus
            .Trim()
            .ToUpperInvariant();

        var allowedStatuses = new[]
        {
            "PENDING",
            "PAID",
            "FAILED",
            "REFUNDED"
        };

        if (!allowedStatuses.Contains(newStatus))
        {
            return BadRequest("Invalid payment status.");
        }

        // -----------------------------------------------------
        // PAYMENT STATUS TRANSITIONS
        // -----------------------------------------------------

        // A refunded payment is final.
        if (currentStatus == "REFUNDED" &&
            newStatus != "REFUNDED")
        {
            return BadRequest(
                "A refunded payment cannot be changed.");
        }

        // A paid payment can remain PAID or become REFUNDED.
        if (currentStatus == "PAID" &&
            newStatus != "PAID" &&
            newStatus != "REFUNDED")
        {
            return BadRequest(
                "A paid payment can only remain PAID or be REFUNDED.");
        }

        // A failed payment represents a failed attempt.
        // Create a new payment attempt instead.
        if (currentStatus == "FAILED" &&
            newStatus != "FAILED")
        {
            return BadRequest(
                "A failed payment cannot be reopened. Create a new payment attempt.");
        }

        // -----------------------------------------------------
        // ORDER PROTECTION
        // -----------------------------------------------------

        if (payment.Order.OrderStatus == "CANCELLED" &&
            newStatus == "PAID")
        {
            return BadRequest(
                "A cancelled order cannot be marked as paid.");
        }

        if (payment.Order.OrderStatus == "COMPLETED" &&
            newStatus == "PAID")
        {
            return BadRequest(
                "A completed order cannot receive a new successful payment.");
        }

        if (payment.Order.OrderStatus == "COMPLETED" &&
            newStatus == "REFUNDED")
        {
            return BadRequest(
                "A completed order cannot be refunded through this endpoint.");
        }

        // -----------------------------------------------------
        // UPDATE PAYMENT
        // -----------------------------------------------------

        payment.PaymentStatus = newStatus;

        if (!string.IsNullOrWhiteSpace(dto.TransactionReference))
        {
            payment.TransactionReference =
                dto.TransactionReference.Trim();
        }

        if (newStatus == "PAID")
        {
            payment.PaymentDate =
                dto.PaymentDate ?? DateTime.UtcNow;
        }
        else if (newStatus == "PENDING")
        {
            payment.PaymentDate = null;
        }

        payment.UpdatedAt = DateTime.UtcNow;

        // =====================================================
        // COLLECTION ORDER LIFECYCLE
        // =====================================================

        if (payment.Order.OrderType == "COLLECTION")
        {
            var paidAmount = payment.Order.Payments
                .Where(p => p.PaymentStatus == "PAID")
                .Sum(p => p.Amount);

            // Once the collection order is fully paid,
            // it becomes ready for the customer to collect.
            if (newStatus == "PAID" &&
                paidAmount >= payment.Order.TotalAmount &&
                payment.Order.OrderStatus == "PENDING")
            {
                payment.Order.OrderStatus =
                    "READY_FOR_COLLECTION";

                payment.Order.UpdatedAt =
                    DateTime.UtcNow;
            }

            // If a payment is refunded and the remaining
            // paid amount is no longer enough, move the order
            // back to PENDING.
            if (newStatus == "REFUNDED" &&
                payment.Order.OrderStatus == "READY_FOR_COLLECTION")
            {
                var remainingPaidAmount = payment.Order.Payments
                    .Where(p =>
                        p.PaymentId != payment.PaymentId &&
                        p.PaymentStatus == "PAID")
                    .Sum(p => p.Amount);

                if (remainingPaidAmount <
                    payment.Order.TotalAmount)
                {
                    payment.Order.OrderStatus = "PENDING";
                    payment.Order.UpdatedAt =
                        DateTime.UtcNow;
                }
            }
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // =========================================================
    // DELETE PAYMENT
    //
    // ADMIN ONLY
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> DeletePayment(Guid id)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.PaymentId == id);

        if (payment == null)
        {
            return NotFound("Payment not found.");
        }

        if (payment.PaymentStatus == "PAID")
        {
            return BadRequest(
                "Paid payments cannot be deleted.");
        }

        if (payment.PaymentStatus == "REFUNDED")
        {
            return BadRequest(
                "Refunded payments cannot be deleted.");
        }

        _context.Payments.Remove(payment);

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

    private static PaymentDto MapPayment(Payment payment)
    {
        return new PaymentDto
        {
            PaymentId = payment.PaymentId,
            OrderId = payment.OrderId,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            PaymentStatus = payment.PaymentStatus,
            TransactionReference = payment.TransactionReference,
            PaymentDate = payment.PaymentDate,
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt
        };
    }
}