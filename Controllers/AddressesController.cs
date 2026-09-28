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
public class AddressesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AddressesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/Addresses
    // Returns addresses belonging to the logged-in customer.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AddressDto>>> GetAddresses()
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var addresses = await _context.Addresses
            .Where(a => a.Customer.UserId == userId.Value)
            .Select(a => new AddressDto
            {
                AddressId = a.AddressId,
                CustomerId = a.CustomerId,
                AddressLine1 = a.AddressLine1,
                AddressLine2 = a.AddressLine2,
                City = a.City,
                Province = a.Province,
                PostalCode = a.PostalCode,
                Country = a.Country
            })
            .ToListAsync();

        return Ok(addresses);
    }

    // GET: api/Addresses/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<AddressDto>> GetAddress(Guid id)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var address = await _context.Addresses
            .Where(a =>
                a.AddressId == id &&
                a.Customer.UserId == userId.Value)
            .Select(a => new AddressDto
            {
                AddressId = a.AddressId,
                CustomerId = a.CustomerId,
                AddressLine1 = a.AddressLine1,
                AddressLine2 = a.AddressLine2,
                City = a.City,
                Province = a.Province,
                PostalCode = a.PostalCode,
                Country = a.Country
            })
            .FirstOrDefaultAsync();

        if (address == null)
        {
            return NotFound("Address not found.");
        }

        return Ok(address);
    }

    // POST: api/Addresses
    [HttpPost]
    public async Task<ActionResult<AddressDto>> CreateAddress(
        AddressCreateDto dto)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c =>
                c.CustomerId == dto.CustomerId &&
                c.UserId == userId.Value);

        if (customer == null)
        {
            return Forbid();
        }

        var address = new Address
        {
            AddressId = Guid.NewGuid(),
            CustomerId = customer.CustomerId,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            City = dto.City,
            Province = dto.Province,
            PostalCode = dto.PostalCode,
            Country = dto.Country
        };

        _context.Addresses.Add(address);

        await _context.SaveChangesAsync();

        var result = new AddressDto
        {
            AddressId = address.AddressId,
            CustomerId = address.CustomerId,
            AddressLine1 = address.AddressLine1,
            AddressLine2 = address.AddressLine2,
            City = address.City,
            Province = address.Province,
            PostalCode = address.PostalCode,
            Country = address.Country
        };

        return CreatedAtAction(
            nameof(GetAddress),
            new { id = address.AddressId },
            result
        );
    }

    // PUT: api/Addresses/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAddress(
        Guid id,
        AddressUpdateDto dto)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var address = await _context.Addresses
            .FirstOrDefaultAsync(a =>
                a.AddressId == id &&
                a.Customer.UserId == userId.Value);

        if (address == null)
        {
            return NotFound("Address not found.");
        }

        address.AddressLine1 = dto.AddressLine1;
        address.AddressLine2 = dto.AddressLine2;
        address.City = dto.City;
        address.Province = dto.Province;
        address.PostalCode = dto.PostalCode;
        address.Country = dto.Country;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/Addresses/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAddress(Guid id)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var address = await _context.Addresses
            .FirstOrDefaultAsync(a =>
                a.AddressId == id &&
                a.Customer.UserId == userId.Value);

        if (address == null)
        {
            return NotFound("Address not found.");
        }

        _context.Addresses.Remove(address);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Get UserId from JWT
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