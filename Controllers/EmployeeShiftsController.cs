using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "EMPLOYEE")]
public class EmployeeShiftsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmployeeShiftsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/EmployeeShifts/my
    [HttpGet("my")]
    public async Task<IActionResult> GetMyShifts()
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == userId);

        if (employee == null)
        {
            return NotFound("Employee profile not found.");
        }

        var today = DateTime.UtcNow.Date;
        var nextWeek = today.AddDays(7);

        var shifts = await _context.EmployeeShifts
            .Where(s =>
                s.EmployeeId == employee.EmployeeId &&
                s.ShiftEnd >= today &&
                s.ShiftStart < nextWeek)
            .OrderBy(s => s.ShiftStart)
            .Select(s => new
            {
                employeeShiftId = s.EmployeeShiftId,
                shiftStart = s.ShiftStart,
                shiftEnd = s.ShiftEnd,
                status = s.Status,
                notes = s.Notes
            })
            .ToListAsync();

        var currentShift = shifts.FirstOrDefault(s =>
    s.shiftStart <= DateTime.UtcNow &&
    s.shiftEnd >= DateTime.UtcNow);

var upcomingShifts = shifts
    .Where(s => s.shiftStart > DateTime.UtcNow)
    .ToList();

        return Ok(new
        {
            currentShift,
            upcomingShifts
        });
    }
}