using Microsoft.EntityFrameworkCore;
using SwivelWater.API.Models;

namespace SwivelWater.API.Data;

public static class DbSeeder
{
    public static async Task SeedAdminAsync(
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        var email = configuration["Admin:Email"];
        var staffNumber = configuration["Admin:StaffNumber"];
        var password = configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(staffNumber) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Admin credentials are not configured. " +
                "Set Admin:Email, Admin:StaffNumber and Admin:Password using User Secrets."
            );
        }

        email = email.Trim().ToLower();
        staffNumber = staffNumber.Trim().ToUpper();

        var existingStaffNumber = await context.Employees
            .FirstOrDefaultAsync(e => e.EmployeeNumber == staffNumber);

        var user = await context.Users
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Email == email);

        // ---------------------------------------------------------
        // Create the admin if it does not exist
        // ---------------------------------------------------------

        if (user == null)
        {
            if (existingStaffNumber != null)
            {
                throw new InvalidOperationException(
                    $"Staff number {staffNumber} is already assigned to another employee."
                );
            }

            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "ADMIN",
                IsActive = true,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var adminEmployee = new Employee
            {
                EmployeeId = Guid.NewGuid(),
                UserId = user.UserId,
                FirstName = "Swivel",
                LastName = "Manager",
                Phone = "",
                Role = "ADMIN",
                EmployeeNumber = staffNumber,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            context.Employees.Add(adminEmployee);

            await context.SaveChangesAsync();

            return;
        }

        // ---------------------------------------------------------
        // Existing user must already be an ADMIN
        // ---------------------------------------------------------

        if (user.Role != "ADMIN")
        {
            throw new InvalidOperationException(
                $"The configured admin email '{email}' already belongs to a non-admin account."
            );
        }

        // ---------------------------------------------------------
        // Ensure the admin has an employee record
        // ---------------------------------------------------------

        if (user.Employee == null)
        {
            if (existingStaffNumber != null)
            {
                throw new InvalidOperationException(
                    $"Staff number {staffNumber} is already assigned to another employee."
                );
            }

            var adminEmployee = new Employee
            {
                EmployeeId = Guid.NewGuid(),
                UserId = user.UserId,
                FirstName = "Swivel",
                LastName = "Manager",
                Phone = "",
                Role = "ADMIN",
                EmployeeNumber = staffNumber,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Employees.Add(adminEmployee);

            await context.SaveChangesAsync();

            return;
        }

        // ---------------------------------------------------------
        // Validate existing admin employee record
        // ---------------------------------------------------------

        if (user.Employee.EmployeeNumber != staffNumber)
        {
            throw new InvalidOperationException(
                $"The configured staff number '{staffNumber}' does not match the admin account."
            );
        }

        if (user.Employee.Role != "ADMIN")
        {
            user.Employee.Role = "ADMIN";
            user.Employee.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
        }
    }
}