using System.ComponentModel.DataAnnotations;

namespace SwivelWater.API.DTOs;

public class EmployeeProfileUpdateDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;
}