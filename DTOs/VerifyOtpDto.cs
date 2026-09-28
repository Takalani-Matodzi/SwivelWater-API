namespace SwivelWater.API.DTOs;

public class VerifyOtpDto
{
    public string Email { get; set; } = string.Empty;

    public string Otp { get; set; } = string.Empty;
}