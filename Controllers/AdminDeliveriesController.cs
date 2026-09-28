using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class AdminDeliveriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminDeliveriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/AdminDeliveries
    [HttpGet]
    public async Task<IActionResult> GetDeliveries()
    {
        var deliveries = await _context.Deliveries
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                deliveryId = d.DeliveryId,
                orderId = d.OrderId,

                customerName =
                    d.Order.Customer.FirstName + " " +
                    d.Order.Customer.LastName,

                phone = d.Order.Customer.Phone,

                address = new
                {
                    addressLine1 = d.Order.Address.AddressLine1,
                    addressLine2 = d.Order.Address.AddressLine2,
                    city = d.Order.Address.City,
                    province = d.Order.Address.Province,
                    postalCode = d.Order.Address.PostalCode,
                    country = d.Order.Address.Country
                },

                driverName = d.Employee != null
                    ? d.Employee.FirstName + " " + d.Employee.LastName
                    : null,

                deliveryStatus = d.DeliveryStatus,
                scheduledDate = d.ScheduledDate,
                deliveredAt = d.DeliveredAt,
                notes = d.Notes,

                orderType = d.Order.OrderType,
                orderStatus = d.Order.OrderStatus,
                totalAmount = d.Order.TotalAmount,

                createdAt = d.CreatedAt,
                updatedAt = d.UpdatedAt
            })
            .ToListAsync();

        return Ok(deliveries);
    }
}