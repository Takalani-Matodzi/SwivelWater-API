namespace SwivelWater.API.Models;

public class OrderItem
{
    public Guid OrderItemId { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal SubTotal { get; set; }

    // Relationships
    public Order Order { get; set; } = null!;

    public Product Product { get; set; } = null!;
}