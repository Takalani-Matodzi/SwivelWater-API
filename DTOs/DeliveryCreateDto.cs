namespace SwivelWater.API.DTOs;

public class DeliveryCreateDto
{
    public Guid OrderId { get; set; }

    public Guid? EmployeeId { get; set; }

    public DateTime? ScheduledDate { get; set; }

    public string? Notes { get; set; }
}
