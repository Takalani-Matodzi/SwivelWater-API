namespace SwivelWater.API.DTOs;

public class DeliveryUpdateDto
{
    public Guid? EmployeeId { get; set; }

    public string DeliveryStatus { get; set; } = "PENDING";

    public DateTime? ScheduledDate { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public string? Notes { get; set; }
}
