namespace SwivelWater.API.DTOs;

public class EmployeeRegisterDto
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    // EMPLOYEE or DRIVER
    public string EmployeeRole { get; set; } = "EMPLOYEE";
}