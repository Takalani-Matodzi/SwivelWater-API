namespace SwivelWater.API.DTOs;

public class OrderUpdateDto
{
    public Guid AddressId { get; set; }

    public string OrderType { get; set; } = string.Empty;

    public string OrderStatus { get; set; } = string.Empty;

    public string? Notes { get; set; }
}