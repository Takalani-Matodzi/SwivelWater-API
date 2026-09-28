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
public class CustomersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CustomersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/Customers
    // Customer can only retrieve their own profile.
    [HttpGet]
    public async Task<ActionResult<CustomerDto>> GetCustomers()
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var customer = await _context.Customers
            .Where(c => c.UserId == userId.Value)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                UserId = c.UserId,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Phone = c.Phone
            })
            .FirstOrDefaultAsync();

        if (customer == null)
        {
            return NotFound("Customer profile not found.");
        }

        return Ok(customer);
    }

    // GET: api/Customers/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var customer = await _context.Customers
            .Where(c =>
                c.CustomerId == id &&
                c.UserId == userId.Value)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                UserId = c.UserId,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Phone = c.Phone
            })
            .FirstOrDefaultAsync();

        if (customer == null)
        {
            return NotFound("Customer profile not found.");
        }

        return Ok(customer);
    }

    // POST: api/Customers
    // Customer profiles are created during registration.
    [HttpPost]
    public IActionResult CreateCustomer(CustomerCreateDto dto)
    {
        return Forbid();
    }

    // PUT: api/Customers/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCustomer(
        Guid id,
        CustomerUpdateDto dto)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c =>
                c.CustomerId == id &&
                c.UserId == userId.Value);

        if (customer == null)
        {
            return NotFound("Customer profile not found.");
        }

        customer.FirstName = dto.FirstName;
        customer.LastName = dto.LastName;
        customer.Phone = dto.Phone;
        customer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/Customers/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer(Guid id)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c =>
                c.CustomerId == id &&
                c.UserId == userId.Value);

        if (customer == null)
        {
            return NotFound("Customer profile not found.");
        }

        _context.Customers.Remove(customer);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Get the logged-in user's UserId from the JWT.
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
}