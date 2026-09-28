namespace SwivelWater.API.DTOs;

public class PaymentDto
{
    public Guid PaymentId { get; set; }

    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = "PENDING";

    public string? TransactionReference { get; set; }

    public DateTime? PaymentDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
