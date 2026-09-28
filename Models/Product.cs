namespace SwivelWater.API.Models;

public class Product
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    // BOTTLED or REFILL
    public string ProductType { get; set; } = "BOTTLED";

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Relationship
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}