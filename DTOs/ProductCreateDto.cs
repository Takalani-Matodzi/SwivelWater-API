namespace SwivelWater.API.DTOs;

public class ProductCreateDto
{
    public string ProductName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public string ProductType { get; set; } = "BOTTLED";

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;
}