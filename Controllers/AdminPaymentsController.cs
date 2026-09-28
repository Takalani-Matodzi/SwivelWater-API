using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class AdminPaymentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminPaymentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/AdminPayments
    [HttpGet]
    public async Task<IActionResult> GetPayments()
    {
        var payments = await _context.Payments
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                paymentId = p.PaymentId,
                orderId = p.OrderId,
                customerName =
                    p.Order.Customer.FirstName + " " +
                    p.Order.Customer.LastName,
                amount = p.Amount,
                paymentMethod = p.PaymentMethod,
                paymentStatus = p.PaymentStatus,
                transactionReference = p.TransactionReference,
                paymentDate = p.PaymentDate,
                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt
            })
            .ToListAsync();

        return Ok(payments);
    }
}