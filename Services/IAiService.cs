namespace SwivelWater.API.Services;

public interface IAiService
{Task<string> GetResponseAsync(
    string message,
    string accessLevel,
    string? userId = null,
    string? trustedContext = null);
}