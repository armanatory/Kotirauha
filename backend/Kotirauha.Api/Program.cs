using System.Text;
using System.Threading.RateLimiting;
using Kotirauha.Api.Common;
using Kotirauha.Api.Endpoints;
using Kotirauha.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
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

// Abuse protection. Keys are the real client IP (behind Caddy) or the signed-in
// user. Limits are generous so normal use is never throttled.
static string IpKey(HttpContext ctx) => ctx.ClientIp();
static string UserOrIpKey(HttpContext ctx) =>
    ctx.User.GetUserId()?.ToString() is { } uid ? $"u:{uid}" : $"ip:{ctx.ClientIp()}";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Backstop against floods, per IP, across the whole API.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(IpKey(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) }));

    // Login emails cost Mailjet credits: tight, per IP.
    options.AddPolicy("magic-link", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(IpKey(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(15) }));

    // AI calls cost OpenAI tokens: per user, still roomy for live typing.
    options.AddPolicy("ai", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(UserOrIpKey(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromMinutes(1) }));

    // Anonymous analytics pings, per IP.
    options.AddPolicy("track", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(IpKey(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));

    // Exports, per user, generous daily cap to stop spam.
    options.AddPolicy("export", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(UserOrIpKey(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromDays(1) }));
});

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
app.UseRateLimiter();
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
