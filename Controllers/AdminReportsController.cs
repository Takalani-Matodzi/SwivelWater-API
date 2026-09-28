using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class AdminReportsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminReportsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET REPORTS
    //
    // Supported periods:
    // TODAY
    // WEEK
    // MONTH
    // YEAR
    // ALL
    //
    // Example:
    // /api/AdminReports?period=MONTH
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetReports(
        [FromQuery] string period = "MONTH")
    {
        period = period.Trim().ToUpperInvariant();

        var now = DateTime.UtcNow;

        DateTime? startDate = period switch
        {
            "TODAY" => DateTime.SpecifyKind(now.Date, DateTimeKind.Utc),

            "WEEK" => DateTime.SpecifyKind(
                now.Date.AddDays(-(int)now.DayOfWeek),
                DateTimeKind.Utc),

            "MONTH" => new DateTime(
                now.Year,
                now.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc),

            "YEAR" => new DateTime(
                now.Year,
                1,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc),

            "ALL" => null,

            _ => null
        };

        if (period is not
            ("TODAY" or "WEEK" or "MONTH" or "YEAR" or "ALL"))
        {
            return BadRequest(
                "Invalid report period. Use TODAY, WEEK, MONTH, YEAR or ALL."
            );
        }

        // =====================================================
        // SALES REPORT
        // =====================================================

        var ordersQuery = _context.Orders.AsQueryable();

        if (startDate.HasValue)
        {
            ordersQuery = ordersQuery.Where(o =>
                o.OrderDate >= startDate.Value &&
                o.OrderDate <= now);
        }

        var totalOrders = await ordersQuery.CountAsync();

        var completedOrders = await ordersQuery.CountAsync(o =>
            o.OrderStatus == "COMPLETED");

        var cancelledOrders = await ordersQuery.CountAsync(o =>
            o.OrderStatus == "CANCELLED");

        var grossSales =
            await ordersQuery
                .Where(o => o.OrderStatus == "COMPLETED")
                .SumAsync(o => (decimal?)o.TotalAmount)
            ?? 0m;

        var deliverySales =
            await ordersQuery
                .Where(o =>
                    o.OrderStatus == "COMPLETED" &&
                    o.OrderType == "DELIVERY")
                .SumAsync(o => (decimal?)o.TotalAmount)
            ?? 0m;

        var collectionSales =
            await ordersQuery
                .Where(o =>
                    o.OrderStatus == "COMPLETED" &&
                    o.OrderType == "COLLECTION")
                .SumAsync(o => (decimal?)o.TotalAmount)
            ?? 0m;

        var salesByOrderType = new[]
        {
            new
            {
                orderType = "DELIVERY",
                sales = deliverySales
            },
            new
            {
                orderType = "COLLECTION",
                sales = collectionSales
            }
        };

        // =====================================================
        // TOP PRODUCTS
        // =====================================================

        var orderItemsQuery = _context.OrderItems
            .Include(oi => oi.Product)
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.OrderStatus == "COMPLETED");

        if (startDate.HasValue)
        {
            orderItemsQuery = orderItemsQuery.Where(oi =>
                oi.Order.OrderDate >= startDate.Value &&
                oi.Order.OrderDate <= now);
        }

        var topProducts = await orderItemsQuery
            .GroupBy(oi => new
            {
                oi.ProductId,
                oi.Product.ProductName
            })
            .Select(group => new
            {
                productId = group.Key.ProductId,
                productName = group.Key.ProductName,
                quantitySold = group.Sum(oi => oi.Quantity),
                sales = group.Sum(oi => oi.SubTotal)
            })
            .OrderByDescending(x => x.sales)
            .Take(5)
            .ToListAsync();

        // =====================================================
        // DELIVERY REPORT
        // =====================================================

        var deliveriesQuery = _context.Deliveries.AsQueryable();

        if (startDate.HasValue)
        {
            deliveriesQuery = deliveriesQuery.Where(d =>
                d.CreatedAt >= startDate.Value &&
                d.CreatedAt <= now);
        }

        var totalDeliveries = await deliveriesQuery.CountAsync();

        var deliveredDeliveries =
            await deliveriesQuery.CountAsync(d =>
                d.DeliveryStatus == "DELIVERED");

        var outForDelivery =
            await deliveriesQuery.CountAsync(d =>
                d.DeliveryStatus == "OUT_FOR_DELIVERY");

        var pendingDeliveries =
            await deliveriesQuery.CountAsync(d =>
                d.DeliveryStatus == "PENDING");

        var cancelledDeliveries =
            await deliveriesQuery.CountAsync(d =>
                d.DeliveryStatus == "CANCELLED");

        var deliveryCompletionRate =
            totalDeliveries == 0
                ? 0
                : Math.Round(
                    (decimal)deliveredDeliveries /
                    totalDeliveries *
                    100,
                    2);

        // =====================================================
        // INVENTORY REPORT
        // =====================================================

        var activeProducts =
            await _context.Products.CountAsync(p =>
                p.IsActive);

        var bottledUnits =
            await _context.Products
                .Where(p =>
                    p.IsActive &&
                    p.ProductType == "BOTTLED")
                .SumAsync(p => (int?)p.StockQuantity)
            ?? 0;

        var refillLitres =
            await _context.Products
                .Where(p =>
                    p.IsActive &&
                    p.ProductType == "REFILL")
                .SumAsync(p => (int?)p.StockQuantity)
            ?? 0;

        var lowStockProducts =
            await _context.Products
                .Where(p =>
                    p.IsActive &&
                    p.ProductType != "REFILL_CARD" &&
                    p.StockQuantity <= 10)
                .OrderBy(p => p.StockQuantity)
                .Select(p => new
                {
                    productId = p.ProductId,
                    productName = p.ProductName,
                    productType = p.ProductType,
                    stockQuantity = p.StockQuantity
                })
                .ToListAsync();

        // =====================================================
        // CUSTOMER COUNT
        // =====================================================

        var registeredCustomers =
            await _context.Customers.CountAsync();

        // =====================================================
        // RESPONSE
        // =====================================================

        return Ok(new
        {
            period,

            periodStart = startDate,
            periodEnd = now,

            sales = new
            {
                totalOrders,
                completedOrders,
                cancelledOrders,
                grossSales,
                averageCompletedOrderValue =
                    completedOrders == 0
                        ? 0
                        : Math.Round(
                            grossSales / completedOrders,
                            2),
                salesByOrderType
            },

            topProducts,

            deliveries = new
            {
                totalDeliveries,
                deliveredDeliveries,
                outForDelivery,
                pendingDeliveries,
                cancelledDeliveries,
                deliveryCompletionRate
            },

            inventory = new
            {
                activeProducts,
                bottledUnits,
                refillLitres,
                lowStockProducts
            },

            registeredCustomers
        });
    }
}
