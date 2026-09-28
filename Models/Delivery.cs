namespace SwivelWater.API.Models;

public class Delivery
{
    public Guid DeliveryId { get; set; }

    public Guid OrderId { get; set; }

    public Guid? EmployeeId { get; set; }

    public string DeliveryStatus { get; set; } = "PENDING";

    public DateTime? ScheduledDate { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Relationships
    public Order Order { get; set; } = null!;

    public Employee? Employee { get; set; }
}