using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwivelWater.API.Models;

public class EmployeeShift
{
    [Key]
    public Guid EmployeeShiftId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public DateTime ShiftStart { get; set; }

    [Required]
    public DateTime ShiftEnd { get; set; }

    [Required]
    public string Status { get; set; } = "SCHEDULED";

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;
}