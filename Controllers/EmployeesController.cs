using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;
using SwivelWater.API.DTOs;
using SwivelWater.API.Models;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmployeesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET ALL EMPLOYEES
    //
    // EMPLOYEE / ADMIN can view employee records.
    // =========================================================
    [HttpGet]
    [Authorize(Roles = "EMPLOYEE,ADMIN")]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetEmployees()
    {
        var employees = await _context.Employees
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .Select(e => new EmployeeDto
            {
                EmployeeId = e.EmployeeId,
                UserId = e.UserId,
                FirstName = e.FirstName,
                LastName = e.LastName,
                Phone = e.Phone,
                Role = e.Role,
                EmployeeNumber = e.EmployeeNumber,
                IsActive = e.IsActive
            })
            .ToListAsync();

        return Ok(employees);
    }

    // =========================================================
    // GET ONE EMPLOYEE
    // =========================================================
    [HttpGet("{id}")]
    [Authorize(Roles = "EMPLOYEE,ADMIN")]
    public async Task<ActionResult<EmployeeDto>> GetEmployee(Guid id)
    {
        var employee = await _context.Employees
            .Where(e => e.EmployeeId == id)
            .Select(e => new EmployeeDto
            {
                EmployeeId = e.EmployeeId,
                UserId = e.UserId,
                FirstName = e.FirstName,
                LastName = e.LastName,
                Phone = e.Phone,
                Role = e.Role,
                EmployeeNumber = e.EmployeeNumber,
                IsActive = e.IsActive
            })
            .FirstOrDefaultAsync();

        if (employee == null)
        {
            return NotFound("Employee not found.");
        }

        return Ok(employee);
    }

    // =========================================================
    // CREATE EMPLOYEE
    //
    // ADMIN ONLY
    //
    // NOTE:
    // AuthController/register-employee is currently the main
    // employee/driver registration flow because it generates
    // employee numbers automatically.
    //
    // This endpoint therefore creates a normal EMPLOYEE record.
    // Driver creation is handled by AuthController.
    // =========================================================
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<EmployeeDto>> CreateEmployee(
        EmployeeCreateDto dto)
    {
        if (dto.UserId == Guid.Empty)
        {
            return BadRequest("UserId is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName) ||
            string.IsNullOrWhiteSpace(dto.Phone) ||
            string.IsNullOrWhiteSpace(dto.EmployeeNumber))
        {
            return BadRequest(
                "First name, last name, phone and employee number are required.");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == dto.UserId);

        if (user == null)
        {
            return BadRequest(
                "The specified user does not exist.");
        }

        if (user.Role == "ADMIN")
        {
            return BadRequest(
                "An administrator cannot be registered as an employee.");
        }

        var existingEmployee = await _context.Employees
            .AnyAsync(e => e.UserId == dto.UserId);

        if (existingEmployee)
        {
            return Conflict(
                "This user is already linked to an employee.");
        }

        var employeeNumber = dto.EmployeeNumber.Trim();

        var duplicateEmployeeNumber = await _context.Employees
            .AnyAsync(e => e.EmployeeNumber == employeeNumber);

        if (duplicateEmployeeNumber)
        {
            return Conflict(
                "Employee number already exists.");
        }

        var employee = new Employee
        {
            UserId = dto.UserId,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Phone = dto.Phone.Trim(),
            Role = "EMPLOYEE",
            EmployeeNumber = employeeNumber,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Employees.Add(employee);

        // Normal employee account.
        user.Role = "EMPLOYEE";
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var result = new EmployeeDto
        {
            EmployeeId = employee.EmployeeId,
            UserId = employee.UserId,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Phone = employee.Phone,
            Role = employee.Role,
            EmployeeNumber = employee.EmployeeNumber,
            IsActive = employee.IsActive
        };

        return CreatedAtAction(
            nameof(GetEmployee),
            new { id = employee.EmployeeId },
            result
        );
    }

    // =========================================================
    // UPDATE EMPLOYEE
    //
    // ADMIN ONLY
    //
    // IMPORTANT:
    // Do NOT force the employee role back to EMPLOYEE.
    //
    // A DRIVER must remain a DRIVER.
    // An ADMIN must remain an ADMIN.
    // =========================================================
    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> UpdateEmployee(
        Guid id,
        EmployeeUpdateDto dto)
    {
        var employee = await _context.Employees
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.EmployeeId == id);

        if (employee == null)
        {
            return NotFound("Employee not found.");
        }

        if (string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName) ||
            string.IsNullOrWhiteSpace(dto.Phone) ||
            string.IsNullOrWhiteSpace(dto.EmployeeNumber))
        {
            return BadRequest(
                "First name, last name, phone and employee number are required.");
        }

        var employeeNumber = dto.EmployeeNumber.Trim();

        var duplicateEmployeeNumber = await _context.Employees
            .AnyAsync(e =>
                e.EmployeeNumber == employeeNumber &&
                e.EmployeeId != id);

        if (duplicateEmployeeNumber)
        {
            return Conflict(
                "Employee number already exists.");
        }

        employee.FirstName = dto.FirstName.Trim();
        employee.LastName = dto.LastName.Trim();
        employee.Phone = dto.Phone.Trim();

        // IMPORTANT:
        // Preserve the existing role.
        employee.EmployeeNumber = employeeNumber;
        employee.IsActive = dto.IsActive;
        employee.UpdatedAt = DateTime.UtcNow;

        // Keep the User role consistent with the employee type.
        //
        // ADMIN:
        // User.Role = ADMIN
        //
        // DRIVER:
        // User.Role = EMPLOYEE
        //
        // Normal EMPLOYEE:
        // User.Role = EMPLOYEE
        if (employee.Role == "ADMIN")
        {
            employee.User.Role = "ADMIN";
        }
        else
        {
            employee.User.Role = "EMPLOYEE";
        }

        employee.User.IsActive = employee.IsActive;
        employee.User.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // =========================================================
    // DELETE EMPLOYEE
    //
    // ADMIN ONLY
    //
    // We disable the associated user account rather than
    // converting it into a CUSTOMER account.
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> DeleteEmployee(Guid id)
    {
        var employee = await _context.Employees
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.EmployeeId == id);

        if (employee == null)
        {
            return NotFound("Employee not found.");
        }

        // Never delete the administrator through the employee
        // management endpoint.
        if (employee.Role == "ADMIN")
        {
            return BadRequest(
                "The administrator account cannot be deleted here.");
        }

        // Disable the login account rather than changing the
        // account into a CUSTOMER.
        employee.User.IsActive = false;
        employee.User.UpdatedAt = DateTime.UtcNow;

        _context.Employees.Remove(employee);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}