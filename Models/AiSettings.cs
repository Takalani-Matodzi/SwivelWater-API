namespace SwivelWater.API.Models;

public class AiSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-5.6-luna";
}