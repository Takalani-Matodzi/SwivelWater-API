using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SwivelWater.API.Data;
using System.Text;
using SwivelWater.API.Models;
using SwivelWater.API.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// Add Email Service
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email")
);

builder.Services.AddScoped<IEmailService, EmailService>();

// Add Admin Settings
builder.Services.Configure<AdminSettings>(
    builder.Configuration.GetSection("Admin")
);

// Add AI Service
builder.Services.Configure<AiSettings>(
    builder.Configuration.GetSection("OpenAI")
);

builder.Services.AddSingleton<IAiService, OpenAiService>();
builder.Services.AddScoped<IAiCapabilityService, AiCapabilityService>();

// Add Controllers
builder.Services.AddControllers();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174",
                "http://localhost:5175",
                "http://localhost:5176",
                "http://localhost:5177"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"]!
                )
            )
        };
    });

// Add Authorization
builder.Services.AddAuthorization();

// Add OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure OpenAPI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

var profileImagesPath = Path.Combine(
    app.Environment.ContentRootPath,
    "wwwroot",
    "profile-images"
);

Directory.CreateDirectory(profileImagesPath);

app.UseStaticFiles(
    new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(
            Path.Combine(
                app.Environment.ContentRootPath,
                "wwwroot"
            )
        ),
        RequestPath = ""
    }
);

app.UseCors("ReactFrontend");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// Apply pending database migrations
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    await context.Database.MigrateAsync();
}

// Seed the initial admin/manager account
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    await DbSeeder.SeedAdminAsync(
        context,
        app.Configuration
    );
}

app.Run();