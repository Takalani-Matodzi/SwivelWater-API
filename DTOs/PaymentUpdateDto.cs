namespace SwivelWater.API.DTOs;

public class PaymentUpdateDto
{
    public string PaymentStatus { get; set; } = "PENDING";

    public string? TransactionReference { get; set; }

    public DateTime? PaymentDate { get; set; }
}
