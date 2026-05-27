using Kotirauha.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Database=kotirauha;Username=kotirauha;Password=kotirauha";

builder.Services.AddDbContext<KotirauhaDbContext>(options =>
    options.UseNpgsql(connectionString));

var corsOrigins = (Environment.GetEnvironmentVariable("CORS_ORIGINS")
        ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KotirauhaDbContext>();
    db.Database.Migrate();
}

app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
