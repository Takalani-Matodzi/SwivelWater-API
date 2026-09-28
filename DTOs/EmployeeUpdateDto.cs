namespace SwivelWater.API.DTOs;

public class EmployeeUpdateDto
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string EmployeeNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}