namespace SwivelWater.API.DTOs;

public class OrderCreateDto
{
    public Guid CustomerId { get; set; }

    public Guid AddressId { get; set; }

    public string OrderType { get; set; } = string.Empty;

    public bool UseLoyaltyFreeRefill { get; set; } = false;

    public string? Notes { get; set; }
}