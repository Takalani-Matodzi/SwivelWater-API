using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;
using SwivelWater.API.DTOs;
using System.Security.Claims;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    private const long MaxFileSize = 5 * 1024 * 1024;

    private static readonly string[] AllowedExtensions =
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private static readonly string[] AllowedContentTypes =
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public ProfileController(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // GET: api/Profile
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _context.Users
            .Include(u => u.Customer)
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);

        if (user == null)
        {
            return NotFound("User profile not found.");
        }

        string? firstName = null;
        string? lastName = null;
        string? phone = null;
        string? employeeNumber = null;
        string? employeeRole = null;

        if (user.Customer != null)
        {
            firstName = user.Customer.FirstName;
            lastName = user.Customer.LastName;
            phone = user.Customer.Phone;
        }
        else if (user.Employee != null)
        {
            firstName = user.Employee.FirstName;
            lastName = user.Employee.LastName;
            phone = user.Employee.Phone;
            employeeNumber = user.Employee.EmployeeNumber;
            employeeRole = user.Employee.Role;
        }

        return Ok(new
        {
            userId = user.UserId,
            email = user.Email,
            role = user.Role,
            firstName,
            lastName,
            phone,
            employeeNumber,
            employeeRole,
            profileImageUrl = user.ProfileImageUrl,
            isEmailVerified = user.IsEmailVerified
        });
    }

    // PUT: api/Profile
    // Employee updates their personal profile information.
    [HttpPut]
    [Authorize(Roles = "ADMIN,EMPLOYEE")]
    public async Task<IActionResult> UpdateEmployeeProfile(
        EmployeeProfileUpdateDto dto)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(dto.FirstName))
        {
            return BadRequest("First name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.LastName))
        {
            return BadRequest("Last name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Phone))
        {
            return BadRequest("Phone number is required.");
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == userId.Value);

        if (employee == null)
        {
            return NotFound("Employee profile not found.");
        }

        employee.FirstName = dto.FirstName.Trim();
        employee.LastName = dto.LastName.Trim();
        employee.Phone = dto.Phone.Trim();
        employee.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Employee profile updated successfully.",
            firstName = employee.FirstName,
            lastName = employee.LastName,
            phone = employee.Phone
        });
    }

    // POST: api/Profile/image
    [HttpPost("image")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> UploadProfileImage(IFormFile file)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest("Please select a profile picture.");
        }

        if (file.Length > MaxFileSize)
        {
            return BadRequest("Profile picture must be 5 MB or smaller.");
        }

        if (!AllowedContentTypes.Contains(
            file.ContentType.ToLowerInvariant()))
        {
            return BadRequest(
                "Only JPG, JPEG, PNG and WEBP images are allowed."
            );
        }

        var extension = Path.GetExtension(file.FileName)
            .ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(
                "Only JPG, JPEG, PNG and WEBP images are allowed."
            );
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);

        if (user == null)
        {
            return NotFound("User profile not found.");
        }

        var uploadsFolder = Path.Combine(
            _environment.WebRootPath ?? Path.Combine(
                _environment.ContentRootPath,
                "wwwroot"
            ),
            "profile-images"
        );

        Directory.CreateDirectory(uploadsFolder);

        // Generate our own filename.
        // Never trust the original filename.
        var fileName = $"{user.UserId}_{Guid.NewGuid()}{extension}";

        var filePath = Path.Combine(
            uploadsFolder,
            fileName
        );

        await using (var stream = new FileStream(
            filePath,
            FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Delete the previous profile picture if one exists.
        if (!string.IsNullOrWhiteSpace(user.ProfileImageUrl))
        {
            var oldFileName = Path.GetFileName(
                user.ProfileImageUrl
            );

            var oldFilePath = Path.Combine(
                uploadsFolder,
                oldFileName
            );

            if (System.IO.File.Exists(oldFilePath))
            {
                System.IO.File.Delete(oldFilePath);
            }
        }

        user.ProfileImageUrl =
            $"/profile-images/{fileName}";

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Profile picture uploaded successfully.",
            profileImageUrl = user.ProfileImageUrl
        });
    }

    // DELETE: api/Profile/image
    [HttpDelete("image")]
    public async Task<IActionResult> DeleteProfileImage()
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);

        if (user == null)
        {
            return NotFound("User profile not found.");
        }

        if (!string.IsNullOrWhiteSpace(user.ProfileImageUrl))
        {
            var uploadsFolder = Path.Combine(
                _environment.WebRootPath ?? Path.Combine(
                    _environment.ContentRootPath,
                    "wwwroot"
                ),
                "profile-images"
            );

            var fileName = Path.GetFileName(
                user.ProfileImageUrl
            );

            var filePath = Path.Combine(
                uploadsFolder,
                fileName
            );

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            user.ProfileImageUrl = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            message = "Profile picture removed successfully."
        });
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(
            ClaimTypes.NameIdentifier
        )?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }
}