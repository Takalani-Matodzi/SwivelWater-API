namespace SwivelWater.API.Services;

public interface IAiCapabilityService
{
    Task<string> GetSafeContextAsync(
        string message,
        string accessLevel,
        Guid? userId);
}