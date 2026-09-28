namespace SwivelWater.API.DTOs;

public class OrderItemCreateDto
{
    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }
}