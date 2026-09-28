namespace SwivelWater.API.DTOs;

public class DeliveryDto
{
    public Guid DeliveryId { get; set; }

    public Guid OrderId { get; set; }

    public Guid? EmployeeId { get; set; }

    public string DeliveryStatus { get; set; } = "PENDING";

    public DateTime? ScheduledDate { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
