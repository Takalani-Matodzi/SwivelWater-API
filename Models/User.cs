namespace SwivelWater.API.Models;

public class User
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "CUSTOMER";

    public bool IsActive { get; set; } = true;

    public bool IsEmailVerified { get; set; } = false;

    public string? EmailOtpHash { get; set; }

    public DateTime? EmailOtpExpiresAt { get; set; }

    public string? ProfileImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Relationships
    public Customer? Customer { get; set; }

    public Employee? Employee { get; set; }
}