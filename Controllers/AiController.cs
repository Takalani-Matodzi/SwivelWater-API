using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwivelWater.API.DTOs;
using SwivelWater.API.Services;
using System.Security.Claims;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly IAiCapabilityService _aiCapabilityService;

    public AiController(
        IAiService aiService,
        IAiCapabilityService aiCapabilityService)
    {
        _aiService = aiService;
        _aiCapabilityService = aiCapabilityService;
    }

    // POST: api/AI/chat
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(AiChatRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
        {
            return BadRequest("Message is required.");
        }

        if (dto.Message.Length > 2000)
        {
            return BadRequest(
                "Message cannot be longer than 2000 characters."
            );
        }

        var accessLevel = ResolveAccessLevel();

        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        Guid? parsedUserId = null;

        if (Guid.TryParse(userId, out var userGuid))
        {
            parsedUserId = userGuid;
        }

        // Get only the data this user is allowed to provide to the AI.
        var trustedContext =
            await _aiCapabilityService.GetSafeContextAsync(
                dto.Message.Trim(),
                accessLevel,
                parsedUserId
            );

        // Send the question together with the approved backend context.
        var response = await _aiService.GetResponseAsync(
            dto.Message.Trim(),
            accessLevel,
            userId,
            trustedContext
        );

        return Ok(new
        {
            message = response,
            accessLevel
        });
    }

    private string ResolveAccessLevel()
    {
        // No valid JWT = public visitor.
        if (User.Identity?.IsAuthenticated != true)
        {
            return "PUBLIC";
        }

        var role = User.FindFirstValue(
            ClaimTypes.Role
        );

        if (role == "ADMIN")
        {
            return "ADMIN";
        }

        if (role == "CUSTOMER")
        {
            return "CUSTOMER";
        }

        if (role == "EMPLOYEE")
        {
            var employeeRole = User.FindFirstValue(
                "EmployeeRole"
            );

            if (employeeRole == "DRIVER")
            {
                return "DRIVER";
            }

            return "EMPLOYEE";
        }

        return "PUBLIC";
    }
}