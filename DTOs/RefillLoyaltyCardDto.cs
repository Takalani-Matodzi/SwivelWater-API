namespace SwivelWater.API.DTOs;

public class RefillLoyaltyCardDto
{
    public Guid RefillLoyaltyCardId { get; set; }

    public Guid CustomerId { get; set; }

    public int TickCount { get; set; }

    public int FreeRefillsAvailable { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}