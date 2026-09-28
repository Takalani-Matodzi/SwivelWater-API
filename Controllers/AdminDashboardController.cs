using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class AdminDashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminDashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET ADMIN DASHBOARD
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var now = DateTime.UtcNow;

        var today = now.Date;
        var tomorrow = today.AddDays(1);

        // =====================================================
        // SUMMARY
        // =====================================================

        var todaysOrders = await _context.Orders
            .CountAsync(o =>
                o.OrderDate >= today &&
                o.OrderDate < tomorrow);

        var todaysRevenue =
            await _context.Payments
                .Where(p =>
                    p.PaymentStatus == "PAID" &&
                    p.PaymentDate != null &&
                    p.PaymentDate >= today &&
                    p.PaymentDate < tomorrow)
                .SumAsync(p => (decimal?)p.Amount)
            ?? 0m;

        var registeredCustomers =
            await _context.Customers.CountAsync();

        var pendingDeliveries =
            await _context.Deliveries
                .CountAsync(d =>
                    d.DeliveryStatus != "DELIVERED" &&
                    d.DeliveryStatus != "CANCELLED");

        // =====================================================
        // RECENT ORDERS
        // =====================================================

        var recentOrders = await _context.Orders
            .Include(o => o.Customer)
            .OrderByDescending(o => o.OrderDate)
            .Take(5)
            .Select(o => new
            {
                orderId = o.OrderId,
                customerName =
                    o.Customer.FirstName + " " + o.Customer.LastName,
                orderType = o.OrderType,
                amount = o.TotalAmount,
                status = o.OrderStatus,
                orderDate = o.OrderDate
            })
            .ToListAsync();

        // =====================================================
        // TEAM STATUS
        // =====================================================

        var teamMembers = await _context.Employees
            .Where(e => e.IsActive)
            .OrderBy(e => e.FirstName)
            .Take(10)
            .Select(e => new
            {
                employeeId = e.EmployeeId,
                name = e.FirstName + " " + e.LastName,
                role = e.Role,

                isOnDuty = _context.EmployeeShifts.Any(s =>
                    s.EmployeeId == e.EmployeeId &&
                    s.ShiftStart <= now &&
                    s.ShiftEnd >= now &&
                    s.Status != "CANCELLED")
            })
            .ToListAsync();

        // =====================================================
        // RESPONSE
        // =====================================================

        return Ok(new
        {
            summary = new
            {
                todaysOrders,
                todaysRevenue,
                registeredCustomers,
                pendingDeliveries
            },

            recentOrders,

            teamMembers = teamMembers.Select(member => new
            {
                member.employeeId,
                member.name,
                role = member.role == "DRIVER"
                    ? "Driver"
                    : "Employee",
                status = member.isOnDuty
                    ? "Online"
                    : "Offline"
            })
        });
    }
}