using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class AdminCustomersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminCustomersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/AdminCustomers
    [HttpGet]
    public async Task<IActionResult> GetCustomers()
    {
        var customers = await _context.Customers
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                customerId = c.CustomerId,
                firstName = c.FirstName,
                lastName = c.LastName,
                email = c.User.Email,
                phone = c.Phone,
                isActive = c.User.IsActive,
                createdAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(customers);
    }
}