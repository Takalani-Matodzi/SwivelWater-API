using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class AdminEmployeesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminEmployeesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetEmployees()
    {
        var now = DateTime.UtcNow;

        var employees = await _context.Employees
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .Select(e => new
            {
                employeeId = e.EmployeeId,
                userId = e.UserId,
                employeeNumber = e.EmployeeNumber,
                firstName = e.FirstName,
                lastName = e.LastName,
                phone = e.Phone,
                role = e.Role,
                isActive = e.IsActive,
                isOnDuty = _context.EmployeeShifts.Any(s =>
                    s.EmployeeId == e.EmployeeId &&
                    s.ShiftStart <= now &&
                    s.ShiftEnd >= now &&
                    s.Status != "CANCELLED")
            })
            .ToListAsync();

        return Ok(employees);
    }
}