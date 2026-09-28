using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;
using SwivelWater.API.DTOs;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "CUSTOMER")]
public class RefillLoyaltyController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public RefillLoyaltyController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET: api/RefillLoyalty
    //
    // Returns the logged-in customer's active loyalty card.
    // =========================================================
    [HttpGet]
    public async Task<ActionResult<RefillLoyaltyCardDto>> GetMyLoyaltyCard()
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == userId.Value);

        if (customer == null)
        {
            return NotFound(
                "Customer profile does not exist."
            );
        }

        var card = await _context.RefillLoyaltyCards
            .FirstOrDefaultAsync(c =>
                c.CustomerId == customer.CustomerId &&
                c.IsActive);

        if (card == null)
        {
            return NotFound(
                "You do not have an active refill loyalty card."
            );
        }

        var result = new RefillLoyaltyCardDto
        {
            RefillLoyaltyCardId =
                card.RefillLoyaltyCardId,

            CustomerId =
                card.CustomerId,

            TickCount =
                card.TickCount,

            FreeRefillsAvailable =
                card.FreeRefillsAvailable,

            IsActive =
                card.IsActive,

            CreatedAt =
                card.CreatedAt,

            UpdatedAt =
                card.UpdatedAt
        };

        return Ok(result);
    }

    // =========================================================
    // GET: api/RefillLoyalty/status
    //
    // Returns a friendly summary for the dashboard.
    // =========================================================
    [HttpGet("status")]
    public async Task<IActionResult> GetLoyaltyStatus()
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == userId.Value);

        if (customer == null)
        {
            return NotFound(
                "Customer profile does not exist."
            );
        }

        var card = await _context.RefillLoyaltyCards
            .FirstOrDefaultAsync(c =>
                c.CustomerId == customer.CustomerId &&
                c.IsActive);

        if (card == null)
        {
            return Ok(new
            {
                HasLoyaltyCard = false,
                TickCount = 0,
                TicksRequired = 10,
                FreeRefillsAvailable = 0,
                RemainingTicks =
                    10
            });
        }

        return Ok(new
        {
            HasLoyaltyCard = true,
            TickCount = card.TickCount,
            TicksRequired = 10,
            FreeRefillsAvailable =
                card.FreeRefillsAvailable,
            RemainingTicks =
                10 - card.TickCount
        });
    }
    // =========================================================
// GET: api/RefillLoyalty/history
//
// Returns the logged-in customer's loyalty transactions.
// =========================================================
[HttpGet("history")]
public async Task<ActionResult<IEnumerable<RefillLoyaltyTransactionDto>>>
    GetLoyaltyHistory()
{
    var userId = GetUserId();

    if (userId == null)
    {
        return Unauthorized();
    }

    var customer = await _context.Customers
        .FirstOrDefaultAsync(c => c.UserId == userId.Value);

    if (customer == null)
    {
        return NotFound(
            "Customer profile does not exist."
        );
    }

    var card = await _context.RefillLoyaltyCards
        .FirstOrDefaultAsync(c =>
            c.CustomerId == customer.CustomerId &&
            c.IsActive);

    if (card == null)
    {
        return Ok(new List<RefillLoyaltyTransactionDto>());
    }

    var history = await _context.RefillLoyaltyTransactions
        .Where(t =>
            t.RefillLoyaltyCardId == card.RefillLoyaltyCardId)
        .OrderByDescending(t => t.CreatedAt)
        .Select(t => new RefillLoyaltyTransactionDto
        {
            RefillLoyaltyTransactionId =
                t.RefillLoyaltyTransactionId,

            OrderId =
                t.OrderId,

            OrderItemId =
                t.OrderItemId,

            TransactionType =
                t.TransactionType,

            Litres =
                t.Litres,

            TicksAdded =
                t.TicksAdded,

            FreeRefillsAdded =
                t.FreeRefillsAdded,

            FreeRefillsUsed =
                t.FreeRefillsUsed,

            CreatedAt =
                t.CreatedAt
        })
        .ToListAsync();

    return Ok(history);
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