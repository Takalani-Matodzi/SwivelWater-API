using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;
using SwivelWater.API.Models;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class AdminEmployeeShiftsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminEmployeeShiftsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET ALL EMPLOYEE SHIFTS
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAllShifts()
    {
        var shifts = await _context.EmployeeShifts
            .Include(s => s.Employee)
            .OrderBy(s => s.ShiftStart)
            .Select(s => new
            {
                employeeShiftId = s.EmployeeShiftId,
                employeeId = s.EmployeeId,
                employeeNumber = s.Employee.EmployeeNumber,
                employeeName = s.Employee.FirstName + " " + s.Employee.LastName,
                employeeRole = s.Employee.Role,
                shiftStart = s.ShiftStart,
                shiftEnd = s.ShiftEnd,
                status = s.Status,
                notes = s.Notes,
                createdAt = s.CreatedAt,
                updatedAt = s.UpdatedAt
            })
            .ToListAsync();

        return Ok(shifts);
    }

    // =========================================================
    // GET ONE SHIFT
    // =========================================================
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetShift(Guid id)
    {
        var shift = await _context.EmployeeShifts
            .Include(s => s.Employee)
            .Where(s => s.EmployeeShiftId == id)
            .Select(s => new
            {
                employeeShiftId = s.EmployeeShiftId,
                employeeId = s.EmployeeId,
                employeeNumber = s.Employee.EmployeeNumber,
                employeeName = s.Employee.FirstName + " " + s.Employee.LastName,
                employeeRole = s.Employee.Role,
                shiftStart = s.ShiftStart,
                shiftEnd = s.ShiftEnd,
                status = s.Status,
                notes = s.Notes,
                createdAt = s.CreatedAt,
                updatedAt = s.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (shift == null)
        {
            return NotFound("Employee shift not found.");
        }

        return Ok(shift);
    }

    // =========================================================
    // CREATE SHIFT
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> CreateShift(CreateShiftDto dto)
    {
        if (dto.EmployeeId == Guid.Empty)
        {
            return BadRequest("Employee is required.");
        }

        if (dto.ShiftEnd <= dto.ShiftStart)
        {
            return BadRequest("Shift end time must be after shift start time.");
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.EmployeeId == dto.EmployeeId);

        if (employee == null)
        {
            return NotFound("Employee not found.");
        }

        if (!employee.IsActive)
        {
            return BadRequest("Cannot schedule a shift for an inactive employee.");
        }

        var status = string.IsNullOrWhiteSpace(dto.Status)
            ? "SCHEDULED"
            : dto.Status.Trim().ToUpper();

        var allowedStatuses = new[]
        {
            "SCHEDULED",
            "CANCELLED",
            "COMPLETED"
        };

        if (!allowedStatuses.Contains(status))
        {
            return BadRequest(
                "Invalid shift status. Use SCHEDULED, CANCELLED or COMPLETED."
            );
        }

        var shift = new EmployeeShift
        {
            EmployeeShiftId = Guid.NewGuid(),
            EmployeeId = dto.EmployeeId,
            ShiftStart = dto.ShiftStart,
            ShiftEnd = dto.ShiftEnd,
            Status = status,
            Notes = string.IsNullOrWhiteSpace(dto.Notes)
                ? null
                : dto.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.EmployeeShifts.Add(shift);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Employee shift created successfully.",
            employeeShiftId = shift.EmployeeShiftId
        });
    }

    // =========================================================
    // UPDATE SHIFT
    // =========================================================
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateShift(
        Guid id,
        UpdateShiftDto dto)
    {
        if (dto.EmployeeId == Guid.Empty)
        {
            return BadRequest("Employee is required.");
        }

        if (dto.ShiftEnd <= dto.ShiftStart)
        {
            return BadRequest("Shift end time must be after shift start time.");
        }

        var shift = await _context.EmployeeShifts
            .FirstOrDefaultAsync(s => s.EmployeeShiftId == id);

        if (shift == null)
        {
            return NotFound("Employee shift not found.");
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.EmployeeId == dto.EmployeeId);

        if (employee == null)
        {
            return NotFound("Employee not found.");
        }

        if (!employee.IsActive)
        {
            return BadRequest("Cannot assign a shift to an inactive employee.");
        }

        var status = string.IsNullOrWhiteSpace(dto.Status)
            ? shift.Status
            : dto.Status.Trim().ToUpper();

        var allowedStatuses = new[]
        {
            "SCHEDULED",
            "CANCELLED",
            "COMPLETED"
        };

        if (!allowedStatuses.Contains(status))
        {
            return BadRequest(
                "Invalid shift status. Use SCHEDULED, CANCELLED or COMPLETED."
            );
        }

        shift.EmployeeId = dto.EmployeeId;
        shift.ShiftStart = dto.ShiftStart;
        shift.ShiftEnd = dto.ShiftEnd;
        shift.Status = status;
        shift.Notes = string.IsNullOrWhiteSpace(dto.Notes)
            ? null
            : dto.Notes.Trim();
        shift.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Employee shift updated successfully."
        });
    }

    // =========================================================
    // CANCEL SHIFT
    // =========================================================
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> CancelShift(Guid id)
    {
        var shift = await _context.EmployeeShifts
            .FirstOrDefaultAsync(s => s.EmployeeShiftId == id);

        if (shift == null)
        {
            return NotFound("Employee shift not found.");
        }

        shift.Status = "CANCELLED";
        shift.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Employee shift cancelled successfully."
        });
    }

    // =========================================================
    // DTOs
    // =========================================================

    public class CreateShiftDto
    {
        public Guid EmployeeId { get; set; }

        public DateTime ShiftStart { get; set; }

        public DateTime ShiftEnd { get; set; }

        public string? Status { get; set; }

        public string? Notes { get; set; }
    }

    public class UpdateShiftDto
    {
        public Guid EmployeeId { get; set; }

        public DateTime ShiftStart { get; set; }

        public DateTime ShiftEnd { get; set; }

        public string? Status { get; set; }

        public string? Notes { get; set; }
    }
}