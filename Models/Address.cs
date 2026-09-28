namespace SwivelWater.API.Models;

public class Address
{
    public Guid AddressId { get; set; }

    public Guid CustomerId { get; set; }

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string Province { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = "South Africa";

    // Relationship
    public Customer Customer { get; set; } = null!;
}