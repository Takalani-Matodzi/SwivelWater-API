namespace SwivelWater.API.Models;

public class Payment
{
    public Guid PaymentId { get; set; }

    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = "PENDING";

    public string? TransactionReference { get; set; }

    public DateTime? PaymentDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Relationship
    public Order Order { get; set; } = null!;
}