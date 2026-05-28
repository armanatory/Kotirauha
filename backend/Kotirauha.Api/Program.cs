using System.Text;
using Kotirauha.Api.Endpoints;
using Kotirauha.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// Load secrets from the repo-root .env (walks up the directory tree). Does not
// override variables already set in the process environment.
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKotirauhaInfrastructure();
builder.Services.AddMemoryCache();

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

    // Promote configured operators to platform admin (idempotent).
    var toPromote = db.Users.Where(u => !u.IsPlatformAdmin).ToList()
        .Where(u => Kotirauha.Api.Common.AdminConfig.IsAdminEmail(u.Email))
        .ToList();
    if (toPromote.Count > 0)
    {
        foreach (var u in toPromote) u.IsPlatformAdmin = true;
        db.SaveChanges();
    }
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api/v1");
api.MapAuthEndpoints();
api.MapBuildingEndpoints();
api.MapInviteEndpoints();
api.MapEntryEndpoints();
api.MapExportEndpoints();
api.MapInsightsEndpoints();
api.MapAdminEndpoints();
api.MapResourceEndpoints();
api.MapTrackEndpoints();

app.Run();

public partial class Program;
