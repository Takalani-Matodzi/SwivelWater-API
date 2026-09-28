namespace SwivelWater.API.Models;

public class Order
{
    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid AddressId { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public string OrderType { get; set; } = string.Empty;
    public bool UsesLoyaltyFreeRefill { get; set; } = false;
    public string OrderStatus { get; set; } = "PENDING";

    public decimal TotalAmount { get; set; }
    public decimal DeliveryFee { get; set; }

    public decimal? DeliveryDistanceKm { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Relationships
    public Customer Customer { get; set; } = null!;

    public Address Address { get; set; } = null!;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public Delivery? Delivery { get; set; }
}