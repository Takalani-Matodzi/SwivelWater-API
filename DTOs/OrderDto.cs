namespace SwivelWater.API.DTOs;

public class OrderDto
{
    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid AddressId { get; set; }

    public DateTime OrderDate { get; set; }

    public string OrderType { get; set; } = string.Empty;

    public string OrderStatus { get; set; } = string.Empty;
    public bool UsesLoyaltyFreeRefill { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal? DeliveryDistanceKm { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
