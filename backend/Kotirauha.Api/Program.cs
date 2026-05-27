using System.Text;
using Kotirauha.Api.Endpoints;
using Kotirauha.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKotirauhaInfrastructure();

var corsOrigins = (Environment.GetEnvironmentVariable("CORS_ORIGINS")
        ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
                ?? "dev-only-insecure-secret-change-me-32chars!!";
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "kotirauha";
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "kotirauha-users";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KotirauhaDbContext>();
    db.Database.Migrate();

    // Promote the configured operator to platform admin (idempotent).
    var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL")?.Trim().ToLowerInvariant();
    if (!string.IsNullOrWhiteSpace(adminEmail))
    {
        var admin = db.Users.FirstOrDefault(u => u.Email == adminEmail);
        if (admin is not null && !admin.IsPlatformAdmin)
        {
            admin.IsPlatformAdmin = true;
            db.SaveChanges();
        }
    }
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api/v1");
api.MapAuthEndpoints();
api.MapBuildingEndpoints();
api.MapEntryEndpoints();
api.MapExportEndpoints();
api.MapInsightsEndpoints();
api.MapAdminEndpoints();

app.Run();

public partial class Program;
