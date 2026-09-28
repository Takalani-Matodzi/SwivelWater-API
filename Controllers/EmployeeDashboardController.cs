using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "EMPLOYEE")]
public class EmployeeDashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmployeeDashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/EmployeeDashboard
    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == userId);

        if (employee == null)
        {
            return NotFound("Employee profile not found.");
        }

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // =====================================================
        // SUMMARY
        // =====================================================

        var ordersToday = await _context.Orders
            .Where(o =>
                o.OrderDate >= today &&
                o.OrderDate < tomorrow)
            .CountAsync();

        var ordersRequiringAttention = await _context.Orders
            .CountAsync(o =>
                o.OrderStatus == "PENDING" ||
                o.OrderStatus == "PROCESSING" ||
                o.OrderStatus == "READY_FOR_COLLECTION");

        var activeCustomers = await _context.Customers
            .CountAsync(c => c.User.IsActive);

        var bottledStock = await _context.Products
            .Where(p =>
                p.IsActive &&
                p.ProductType == "BOTTLED")
            .SumAsync(p => p.StockQuantity);

        var refillLitres = await _context.Products
            .Where(p =>
                p.IsActive &&
                p.ProductType == "REFILL")
            .SumAsync(p => p.StockQuantity);

        // =====================================================
        // RECENT ORDERS
        // =====================================================

        var recentOrders = await _context.Orders
            .OrderByDescending(o => o.OrderDate)
            .Take(10)
            .Select(o => new
            {
                orderId = o.OrderId,
                customer =
                    o.Customer.FirstName + " " +
                    o.Customer.LastName,
                type = o.OrderType,
                amount = o.TotalAmount,
                status = o.OrderStatus,
                orderDate = o.OrderDate
            })
            .ToListAsync();

        // =====================================================
        // INVENTORY
        // =====================================================

        var inventory = await _context.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.ProductName)
            .Select(p => new
            {
                productId = p.ProductId,
                productName = p.ProductName,
                productType = p.ProductType,
                stockQuantity = p.StockQuantity,
                price = p.Price
            })
            .ToListAsync();

        // =====================================================
        // CUSTOMERS
        // =====================================================

        var customers = await _context.Customers
            .Where(c => c.User.IsActive)
            .OrderBy(c => c.FirstName)
            .ThenBy(c => c.LastName)
            .Select(c => new
            {
                customerId = c.CustomerId,
                firstName = c.FirstName,
                lastName = c.LastName,
                phone = c.Phone,
                email = c.User.Email
            })
            .ToListAsync();

        // =====================================================
        // RESPONSE
        // =====================================================

        return Ok(new
        {
            employee = new
            {
                employeeId = employee.EmployeeId,
                employeeNumber = employee.EmployeeNumber,
                employeeRole = employee.Role,
                isActive = employee.IsActive
            },

            summary = new
            {
                ordersToday,
                ordersRequiringAttention,
                activeCustomers,
                bottledStock,
                refillLitres
            },

            recentOrders,
            inventory,
            customers
        });
    }
}