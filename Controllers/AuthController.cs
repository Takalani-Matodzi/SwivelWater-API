using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Data;
using SwivelWater.API.DTOs;
using SwivelWater.API.Models;
using SwivelWater.API.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SwivelWater.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;

    public AuthController(
        ApplicationDbContext context,
        IConfiguration configuration,
        IEmailService emailService)
    {
        _context = context;
        _configuration = configuration;
        _emailService = emailService;
    }

    // =========================================================
    // CUSTOMER REGISTRATION
    // =========================================================

    // POST: api/Auth/register
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("Email is required.");

        if (string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Password is required.");

        if (string.IsNullOrWhiteSpace(dto.ConfirmPassword))
            return BadRequest("Confirm password is required.");

        if (dto.Password != dto.ConfirmPassword)
            return BadRequest("Passwords do not match.");

        if (string.IsNullOrWhiteSpace(dto.FirstName))
            return BadRequest("First name is required.");

        if (string.IsNullOrWhiteSpace(dto.LastName))
            return BadRequest("Last name is required.");

        if (string.IsNullOrWhiteSpace(dto.Phone))
            return BadRequest("Phone number is required.");

        if (string.IsNullOrWhiteSpace(dto.AddressLine1))
            return BadRequest("Address line 1 is required.");

        if (string.IsNullOrWhiteSpace(dto.City))
            return BadRequest("City is required.");

        if (string.IsNullOrWhiteSpace(dto.Province))
            return BadRequest("Province is required.");

        if (string.IsNullOrWhiteSpace(dto.PostalCode))
            return BadRequest("Postal code is required.");

        dto.Email = dto.Email.Trim().ToLower();

        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (existingUser != null)
        {
            return Conflict("Email is already registered.");
        }

        var otp = GenerateOtp();

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "CUSTOMER",
            IsActive = true,
            IsEmailVerified = false,
            EmailOtpHash = BCrypt.Net.BCrypt.HashPassword(otp),
            EmailOtpExpiresAt = DateTime.UtcNow.AddMinutes(10),
            ProfileImageUrl = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var customer = new Customer
        {
            CustomerId = Guid.NewGuid(),
            UserId = user.UserId,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Phone = dto.Phone.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var address = new Address
        {
            AddressId = Guid.NewGuid(),
            CustomerId = customer.CustomerId,
            AddressLine1 = dto.AddressLine1.Trim(),
            AddressLine2 = string.IsNullOrWhiteSpace(dto.AddressLine2)
                ? null
                : dto.AddressLine2.Trim(),
            City = dto.City.Trim(),
            Province = dto.Province.Trim(),
            PostalCode = dto.PostalCode.Trim(),
            Country = string.IsNullOrWhiteSpace(dto.Country)
                ? "South Africa"
                : dto.Country.Trim()
        };

        _context.Users.Add(user);
        _context.Customers.Add(customer);
        _context.Addresses.Add(address);

        await _context.SaveChangesAsync();

        await SendVerificationOtpAsync(
            user.Email,
            customer.FirstName,
            otp
        );

        return Ok(new
        {
            message = "Registration successful. A verification OTP has been sent to your email.",
            userId = user.UserId,
            customerId = customer.CustomerId,
            email = user.Email,
            firstName = customer.FirstName,
            lastName = customer.LastName,
            role = user.Role,
            emailVerified = user.IsEmailVerified
        });
    }

    // =========================================================
    // VERIFY CUSTOMER EMAIL OTP
    // =========================================================

    // POST: api/Auth/verify-otp
    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp(VerifyOtpDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("Email is required.");

        if (string.IsNullOrWhiteSpace(dto.Otp))
            return BadRequest("OTP is required.");

        var email = dto.Email.Trim().ToLower();

        var user = await _context.Users
            .Include(u => u.Customer)
            .FirstOrDefaultAsync(u =>
                u.Email == email &&
                u.Role == "CUSTOMER");

        if (user == null)
        {
            return NotFound("Customer account not found.");
        }

        if (user.IsEmailVerified)
        {
            return BadRequest("Email has already been verified.");
        }

        if (string.IsNullOrWhiteSpace(user.EmailOtpHash))
        {
            return BadRequest("No verification OTP is available.");
        }

        if (!user.EmailOtpExpiresAt.HasValue ||
            user.EmailOtpExpiresAt.Value < DateTime.UtcNow)
        {
            return BadRequest("OTP has expired.");
        }

        var otpValid = BCrypt.Net.BCrypt.Verify(
            dto.Otp.Trim(),
            user.EmailOtpHash
        );

        if (!otpValid)
        {
            return BadRequest("Invalid OTP.");
        }

        user.IsEmailVerified = true;
        user.EmailOtpHash = null;
        user.EmailOtpExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Email verified successfully.",
            email = user.Email
        });
    }

    // =========================================================
    // CUSTOMER LOGIN
    // =========================================================

    // POST: api/Auth/customer-login
    [HttpPost("customer-login")]
    [AllowAnonymous]
    public async Task<IActionResult> CustomerLogin(CustomerLoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("Email is required.");

        if (string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Password is required.");

        var email = dto.Email.Trim().ToLower();

        var user = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.Email == email &&
                u.Role == "CUSTOMER");

        if (user == null)
        {
            return Unauthorized("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            return Unauthorized("User account is inactive.");
        }

        if (!user.IsEmailVerified)
        {
            return Unauthorized("Please verify your email before logging in.");
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(
            dto.Password,
            user.PasswordHash
        );

        if (!passwordValid)
        {
            return Unauthorized("Invalid email or password.");
        }

        var token = GenerateJwtToken(user);

        return Ok(new
        {
            message = "Customer login successful.",
            token,
            userId = user.UserId,
            email = user.Email,
            role = user.Role
        });
    }

    // =========================================================
    // EMPLOYEE REGISTRATION
    // ADMIN ONLY
    // =========================================================

    // POST: api/Auth/register-employee
    [HttpPost("register-employee")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> RegisterEmployee(
        EmployeeRegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("Email is required.");

        if (string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Password is required.");

        if (dto.Password != dto.ConfirmPassword)
            return BadRequest("Passwords do not match.");

        if (string.IsNullOrWhiteSpace(dto.FirstName))
            return BadRequest("First name is required.");

        if (string.IsNullOrWhiteSpace(dto.LastName))
            return BadRequest("Last name is required.");

        if (string.IsNullOrWhiteSpace(dto.Phone))
            return BadRequest("Phone number is required.");

        var employeeRole = dto.EmployeeRole.Trim().ToUpper();

        if (employeeRole != "EMPLOYEE" &&
            employeeRole != "DRIVER")
        {
            return BadRequest("Employee role must be EMPLOYEE or DRIVER.");
        }

        var email = dto.Email.Trim().ToLower();

        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (existingUser != null)
        {
            return Conflict("Email is already registered.");
        }

        var employeeNumber = await GenerateEmployeeNumberAsync(
            employeeRole
        );

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "EMPLOYEE",
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var employee = new Employee
        {
            EmployeeId = Guid.NewGuid(),
            UserId = user.UserId,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Phone = dto.Phone.Trim(),
            Role = employeeRole,
            EmployeeNumber = employeeNumber,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.Employees.Add(employee);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Employee registered successfully.",
            employeeId = employee.EmployeeId,
            userId = user.UserId,
            employeeNumber = employee.EmployeeNumber,
            employeeRole = employee.Role,
            email = user.Email,
            firstName = employee.FirstName,
            lastName = employee.LastName
        });
    }

    // =========================================================
    // EMPLOYEE / DRIVER LOGIN
    // =========================================================

    // POST: api/Auth/employee-login
    [HttpPost("employee-login")]
    [AllowAnonymous]
    public async Task<IActionResult> EmployeeLogin(
        EmployeeLoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EmployeeNumber))
            return BadRequest("Employee number is required.");

        if (string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Password is required.");

        var employeeNumber = dto.EmployeeNumber.Trim().ToUpper();

        var employee = await _context.Employees
            .Include(e => e.User)
            .FirstOrDefaultAsync(e =>
                e.EmployeeNumber == employeeNumber);

        if (employee == null)
        {
            return Unauthorized("Invalid employee number or password.");
        }

        if (!employee.IsActive || !employee.User.IsActive)
        {
            return Unauthorized("Employee account is inactive.");
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(
            dto.Password,
            employee.User.PasswordHash
        );

        if (!passwordValid)
        {
            return Unauthorized("Invalid employee number or password.");
        }

        var token = GenerateJwtToken(
            employee.User,
            employee.EmployeeNumber,
            employee.Role
        );

        return Ok(new
        {
            message = "Employee login successful.",
            token,
            userId = employee.UserId,
            employeeId = employee.EmployeeId,
            employeeNumber = employee.EmployeeNumber,
            employeeRole = employee.Role,
            email = employee.User.Email,
            role = employee.User.Role
        });
    }

    // =========================================================
    // ADMIN / MANAGER LOGIN
    // =========================================================

    // POST: api/Auth/admin-login
    [HttpPost("admin-login")]
    [AllowAnonymous]
    public async Task<IActionResult> AdminLogin(AdminLoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("Email is required.");

        if (string.IsNullOrWhiteSpace(dto.StaffNumber))
            return BadRequest("Staff number is required.");

        if (string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Password is required.");

        var email = dto.Email.Trim().ToLower();
        var staffNumber = dto.StaffNumber.Trim().ToUpper();

        var user = await _context.Users
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u =>
                u.Email == email &&
                u.Role == "ADMIN");

        if (user == null)
        {
            return Unauthorized("Invalid admin credentials.");
        }

        if (!user.IsActive)
        {
            return Unauthorized("Admin account is inactive.");
        }

        if (user.Employee == null ||
            user.Employee.EmployeeNumber != staffNumber)
        {
            return Unauthorized("Invalid admin credentials.");
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(
            dto.Password,
            user.PasswordHash
        );

        if (!passwordValid)
        {
            return Unauthorized("Invalid admin credentials.");
        }

        var token = GenerateJwtToken(
            user,
            user.Employee.EmployeeNumber,
            "ADMIN"
        );

        return Ok(new
        {
            message = "Admin login successful.",
            token,
            userId = user.UserId,
            employeeId = user.Employee.EmployeeId,
            staffNumber = user.Employee.EmployeeNumber,
            email = user.Email,
            role = user.Role
        });
    }

    // =========================================================
    // JWT
    // =========================================================

    private string GenerateJwtToken(
        User user,
        string? employeeNumber = null,
        string? employeeRole = null)
    {
        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.UserId.ToString()
            ),

            new Claim(
                JwtRegisteredClaimNames.Email,
                user.Email
            ),

            new Claim(
                ClaimTypes.Role,
                user.Role
            )
        };

        if (!string.IsNullOrWhiteSpace(employeeNumber))
        {
            claims.Add(
                new Claim("EmployeeNumber", employeeNumber)
            );
        }

        if (!string.IsNullOrWhiteSpace(employeeRole))
        {
            claims.Add(
                new Claim("EmployeeRole", employeeRole)
            );
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"]!
            )
        );

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    // =========================================================
    // OTP
    // =========================================================

    private static string GenerateOtp()
    {
        return Random.Shared
            .Next(100000, 1000000)
            .ToString();
    }

    private async Task SendVerificationOtpAsync(
        string email,
        string firstName,
        string otp)
    {
        var htmlBody = $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: auto;">
                <h2>Welcome to Swivel Water, {firstName}!</h2>

                <p>Thank you for registering your Swivel Water account.</p>

                <p>Your email verification code is:</p>

                <div style="
                    font-size: 32px;
                    font-weight: bold;
                    letter-spacing: 8px;
                    padding: 20px;
                    text-align: center;
                    background-color: #f2f2f2;
                ">
                    {otp}
                </div>

                <p>This code will expire in <strong>10 minutes</strong>.</p>

                <p>If you did not create this account, please ignore this email.</p>

                <p>
                    Regards,<br/>
                    <strong>Swivel Water</strong>
                </p>
            </div>
            """;

        await _emailService.SendEmailAsync(
            email,
            "Swivel Water - Email Verification Code",
            htmlBody
        );
    }

    // =========================================================
    // EMPLOYEE NUMBER GENERATOR
    // =========================================================

    private async Task<string> GenerateEmployeeNumberAsync(
        string employeeRole)
    {
        var prefix = employeeRole == "DRIVER"
            ? "DRV"
            : "EMP";

        var number = 1;

        while (true)
        {
            var employeeNumber =
                $"{prefix}-{number:D6}";

            var exists = await _context.Employees
                .AnyAsync(e =>
                    e.EmployeeNumber == employeeNumber);

            if (!exists)
            {
                return employeeNumber;
            }

            number++;
        }
    }
}