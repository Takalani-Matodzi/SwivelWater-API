namespace SwivelWater.API.Models;

public class Employee
{
    public Guid EmployeeId { get; set; }

    public Guid UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    // EMPLOYEE, DRIVER, or ADMIN
    public string Role { get; set; } = "EMPLOYEE";

    public string EmployeeNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Relationship
    public User User { get; set; } = null!;

    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
}