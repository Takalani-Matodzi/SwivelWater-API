using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;

namespace SwivelWater.API.Services;

public class AiCapabilityService : IAiCapabilityService
{
    private readonly ApplicationDbContext _context;

    public AiCapabilityService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GetSafeContextAsync(
        string message,
        string accessLevel,
        Guid? userId)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var question = message.ToLowerInvariant();

        return accessLevel switch
        {
            "PUBLIC" => await GetPublicContextAsync(question),

            "CUSTOMER" => userId.HasValue
                ? await GetCustomerContextAsync(
                    question,
                    userId.Value)
                : string.Empty,

            "EMPLOYEE" => userId.HasValue
                ? await GetEmployeeContextAsync(
                    question,
                    userId.Value)
                : string.Empty,

            "DRIVER" => userId.HasValue
                ? await GetDriverContextAsync(
                    question,
                    userId.Value)
                : string.Empty,

            "ADMIN" => await GetAdminContextAsync(question),

            _ => string.Empty
        };
    }

    // =========================================================
    // PUBLIC
    // =========================================================

    private async Task<string> GetPublicContextAsync(
        string question)
    {
        if (ContainsAny(
            question,
            "product",
            "products",
            "price",
            "prices",
            "water",
            "bottle"
        ))
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => new
                {
                    p.ProductName,
                    p.Description,
                    p.Price,
                    p.StockQuantity
                })
                .ToListAsync();

            var formattedProducts = products
                .Select(p => new
                {
                    p.ProductName,
                    p.Description,
                    Price = FormatRand(p.Price),
                    p.StockQuantity
                })
                .ToList();

            return FormatData(
                "PUBLIC PRODUCT INFORMATION",
                formattedProducts
            );
        }

        return string.Empty;
    }

    // =========================================================
    // CUSTOMER
    // =========================================================

    private async Task<string> GetCustomerContextAsync(
        string question,
        Guid userId)
    {
        var sections = new List<string>();

        // -----------------------------------------------------
        // CUSTOMER PROFILE
        // -----------------------------------------------------

        if (ContainsAny(
            question,
            "profile",
            "my details",
            "my information",
            "my address",
            "where do i live"
        ))
        {
            var customer = await _context.Customers
                .Where(c => c.UserId == userId)
                .Select(c => new
                {
                    c.FirstName,
                    c.LastName,
                    c.Phone,
                    Email = c.User.Email,
                    Addresses = c.Addresses.Select(a => new
                    {
                        a.AddressLine1,
                        a.AddressLine2,
                        a.City,
                        a.Province,
                        a.PostalCode,
                        a.Country
                    })
                })
                .FirstOrDefaultAsync();

            if (customer != null)
            {
                sections.Add(
                    FormatData(
                        "CUSTOMER'S OWN PROFILE",
                        customer
                    )
                );
            }
        }

        // -----------------------------------------------------
        // CUSTOMER ORDERS
        // -----------------------------------------------------

        if (ContainsAny(
            question,
            "order",
            "orders",
            "latest order",
            "recent order"
        ))
        {
            var orders = await _context.Orders
                .Where(o => o.Customer.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.OrderId,
                    o.OrderDate,
                    o.OrderType,
                    o.OrderStatus,
                    o.TotalAmount,
                    o.Notes
                })
                .Take(10)
                .ToListAsync();

            var formattedOrders = orders
                .Select(o => new
                {
                    o.OrderId,
                    o.OrderDate,
                    o.OrderType,
                    o.OrderStatus,
                    TotalAmount = FormatRand(o.TotalAmount),
                    o.Notes
                })
                .ToList();

            sections.Add(
                FormatData(
                    "CUSTOMER'S OWN ORDERS",
                    formattedOrders
                )
            );
        }

        // -----------------------------------------------------
        // CUSTOMER PAYMENTS
        // -----------------------------------------------------

        if (ContainsAny(
            question,
            "payment",
            "payments",
            "paid",
            "transaction"
        ))
        {
            var payments = await _context.Payments
                .Where(p =>
                    p.Order.Customer.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.PaymentId,
                    p.OrderId,
                    p.Amount,
                    p.PaymentMethod,
                    p.PaymentStatus,
                    p.TransactionReference,
                    p.PaymentDate
                })
                .Take(10)
                .ToListAsync();

            var formattedPayments = payments
                .Select(p => new
                {
                    p.PaymentId,
                    p.OrderId,
                    Amount = FormatRand(p.Amount),
                    p.PaymentMethod,
                    p.PaymentStatus,
                    p.TransactionReference,
                    p.PaymentDate
                })
                .ToList();

            sections.Add(
                FormatData(
                    "CUSTOMER'S OWN PAYMENTS",
                    formattedPayments
                )
            );
        }

        // -----------------------------------------------------
        // CUSTOMER DELIVERIES
        // -----------------------------------------------------

        if (ContainsAny(
            question,
            "delivery",
            "deliveries",
            "delivery status",
            "where is my order",
            "where is my delivery"
        ))
        {
            var deliveries = await _context.Deliveries
                .Where(d =>
                    d.Order.Customer.UserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new
                {
                    d.DeliveryId,
                    d.OrderId,
                    d.DeliveryStatus,
                    d.ScheduledDate,
                    d.DeliveredAt,
                    d.Notes
                })
                .Take(10)
                .ToListAsync();

            sections.Add(
                FormatData(
                    "CUSTOMER'S OWN DELIVERIES",
                    deliveries
                )
            );
        }

        // -----------------------------------------------------
        // CUSTOMER PRODUCT INFORMATION
        // -----------------------------------------------------

        if (ContainsAny(
            question,
            "product",
            "products",
            "price",
            "prices"
        ))
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => new
                {
                    p.ProductName,
                    p.Description,
                    p.Price,
                    p.StockQuantity
                })
                .ToListAsync();

            var formattedProducts = products
                .Select(p => new
                {
                    p.ProductName,
                    p.Description,
                    Price = FormatRand(p.Price),
                    p.StockQuantity
                })
                .ToList();

            sections.Add(
                FormatData(
                    "PUBLIC PRODUCT INFORMATION",
                    formattedProducts
                )
            );
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            sections
        );
    }

    // =========================================================
    // EMPLOYEE
    // =========================================================

    private async Task<string> GetEmployeeContextAsync(
        string question,
        Guid userId)
    {
        var sections = new List<string>();

        // -----------------------------------------------------
        // EMPLOYEE PROFILE
        // -----------------------------------------------------

        if (ContainsAny(
            question,
            "profile",
            "my details",
            "my employee number"
        ))
        {
            var employee = await _context.Employees
                .Where(e => e.UserId == userId)
                .Select(e => new
                {
                    e.FirstName,
                    e.LastName,
                    e.Phone,
                    e.EmployeeNumber,
                    e.Role,
                    e.IsActive
                })
                .FirstOrDefaultAsync();

            if (employee != null)
            {
                sections.Add(
                    FormatData(
                        "EMPLOYEE'S OWN PROFILE",
                        employee
                    )
                );
            }
        }

        // -----------------------------------------------------
        // EMPLOYEE DELIVERIES
        // -----------------------------------------------------

        if (ContainsAny(
            question,
            "delivery",
            "deliveries",
            "assigned"
        ))
        {
            var deliveries = await _context.Deliveries
                .Where(d =>
                    d.Employee != null &&
                    d.Employee.UserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new
                {
                    d.DeliveryId,
                    d.OrderId,
                    d.DeliveryStatus,
                    d.ScheduledDate,
                    d.DeliveredAt
                })
                .Take(20)
                .ToListAsync();

            sections.Add(
                FormatData(
                    "EMPLOYEE'S ASSIGNED DELIVERIES",
                    deliveries
                )
            );
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            sections
        );
    }

    // =========================================================
    // DRIVER
    // =========================================================

    private async Task<string> GetDriverContextAsync(
        string question,
        Guid userId)
    {
        var sections = new List<string>();

        // -----------------------------------------------------
        // DRIVER DELIVERIES
        // -----------------------------------------------------

        if (ContainsAny(
            question,
            "delivery",
            "deliveries",
            "assigned",
            "next delivery"
        ))
        {
            var deliveries = await _context.Deliveries
                .Where(d =>
                    d.Employee != null &&
                    d.Employee.UserId == userId)
                .OrderByDescending(d => d.ScheduledDate)
                .Select(d => new
                {
                    d.DeliveryId,
                    d.OrderId,
                    d.DeliveryStatus,
                    d.ScheduledDate,
                    d.DeliveredAt,
                    d.Notes
                })
                .Take(20)
                .ToListAsync();

            sections.Add(
                FormatData(
                    "DRIVER'S OWN ASSIGNED DELIVERIES",
                    deliveries
                )
            );
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            sections
        );
    }

    // =========================================================
    // ADMIN
    // =========================================================

    private async Task<string> GetAdminContextAsync(
        string question)
    {
        var sections = new List<string>();

        // -----------------------------------------------------
        // ADMIN ORDER SUMMARY
        // -----------------------------------------------------

        if (ContainsAny(
            question,
            "order",
            "orders",
            "sales",
            "revenue"
        ))
        {
            var orderSummary = await _context.Orders
                .GroupBy(o => 1)
                .Select(g => new
                {
                    TotalOrders = g.Count(),

                    PendingOrders = g.Count(
                        o => o.OrderStatus == "PENDING"),

                    ConfirmedOrders = g.Count(
                        o => o.OrderStatus == "CONFIRMED"),

                    ProcessingOrders = g.Count(
                        o => o.OrderStatus == "PROCESSING"),

                    CompletedOrders = g.Count(
                        o => o.OrderStatus == "COMPLETED"),

                    TotalValue = g.Sum(
                        o => o.TotalAmount)
                })
                .FirstOrDefaultAsync();

            if (orderSummary != null)
            {
                var formattedOrderSummary = new
                {
                    orderSummary.TotalOrders,
                    orderSummary.PendingOrders,
                    orderSummary.ConfirmedOrders,
                    orderSummary.ProcessingOrders,
                    orderSummary.CompletedOrders,
                    TotalValue = FormatRand(
                        orderSummary.TotalValue)
                };

                sections.Add(
                    FormatData(
                        "ADMIN ORDER SUMMARY",
                        formattedOrderSummary
                    )
                );
            }
        }

        // -----------------------------------------------------
        // ADMIN PRODUCTS / STOCK
        // -----------------------------------------------------

        if (ContainsAny(
            question,
            "product",
            "products",
            "stock",
            "inventory"
        ))
        {
            var products = await _context.Products
                .OrderBy(p => p.ProductName)
                .Select(p => new
                {
                    p.ProductName,
                    p.Price,
                    p.StockQuantity,
                    p.IsActive
                })
                .ToListAsync();

            var formattedProducts = products
                .Select(p => new
                {
                    p.ProductName,
                    Price = FormatRand(p.Price),
                    p.StockQuantity,
                    p.IsActive
                })
                .ToList();

            sections.Add(
                FormatData(
                    "ADMIN PRODUCT AND STOCK INFORMATION",
                    formattedProducts
                )
            );
        }

        // -----------------------------------------------------
        // ADMIN EMPLOYEES
        // -----------------------------------------------------

        if (ContainsAny(
            question,
            "employee",
            "employees",
            "staff"
        ))
        {
            var employees = await _context.Employees
                .OrderBy(e => e.LastName)
                .Select(e => new
                {
                    e.FirstName,
                    e.LastName,
                    e.EmployeeNumber,
                    e.Role,
                    e.IsActive
                })
                .ToListAsync();

            sections.Add(
                FormatData(
                    "ADMIN EMPLOYEE INFORMATION",
                    employees
                )
            );
        }

        // -----------------------------------------------------
        // ADMIN DELIVERY SUMMARY
        // -----------------------------------------------------

        if (ContainsAny(
            question,
            "delivery",
            "deliveries"
        ))
        {
            var deliveries = await _context.Deliveries
                .GroupBy(d => d.DeliveryStatus)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            sections.Add(
                FormatData(
                    "ADMIN DELIVERY SUMMARY",
                    deliveries
                )
            );
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            sections
        );
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static bool ContainsAny(
        string question,
        params string[] terms)
    {
        return terms.Any(question.Contains);
    }

    private static string FormatRand(decimal amount)
    {
        return $"R {amount:N2}";
    }

    private static string FormatData<T>(
        string title,
        T data)
    {
        return $"""
            {title}

            The following information was retrieved directly
            from the Swivel Water backend.

            {System.Text.Json.JsonSerializer.Serialize(
                data,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }
            )}

            Treat this information as trusted backend data.
            Do not invent values that are not present here.
            """;
    }
}