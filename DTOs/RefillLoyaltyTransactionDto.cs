namespace SwivelWater.API.DTOs;

public class RefillLoyaltyTransactionDto
{
    public Guid RefillLoyaltyTransactionId { get; set; }

    public Guid OrderId { get; set; }

    public Guid? OrderItemId { get; set; }

    public string TransactionType { get; set; } = string.Empty;

    public int Litres { get; set; }

    public int TicksAdded { get; set; }

    public int FreeRefillsAdded { get; set; }

    public int FreeRefillsUsed { get; set; }

    public DateTime CreatedAt { get; set; }
}