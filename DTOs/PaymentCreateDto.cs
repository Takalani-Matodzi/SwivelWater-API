namespace SwivelWater.API.DTOs;

public class PaymentCreateDto
{
    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string? TransactionReference { get; set; }
}
