using Kotirauha.Api.Common;
using Kotirauha.Core.Abstractions;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record RegisterRequest(string Email, string Password, string DisplayName, string? PreferredLanguage);
public record LoginRequest(string Email, string Password);
public record TokenResponse(string Token);

public record MembershipDto(Guid BuildingId, string BuildingName, string SharedLanguage, string Role, string? ApartmentNumber);
public record CurrentUserDto(Guid Id, string Email, string DisplayName, string PreferredLanguage, bool IsAdmin, MembershipDto? Membership);

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth");

        group.MapPost("/register", async (
            RegisterRequest req, KotirauhaDbContext db, IPasswordHasher hasher, IJwtTokenService jwt) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.Problem("Email and password are required.", statusCode: 400);
            if (req.Password.Length < 8)
                return Results.Problem("Password must be at least 8 characters.", statusCode: 400);

            var email = req.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.Email == email))
                return Results.Problem("An account with that email already exists.", statusCode: 409);

            var user = new User
            {
                Email = email,
                PasswordHash = hasher.Hash(req.Password),
                DisplayName = req.DisplayName.Trim(),
                PreferredLanguage = string.IsNullOrWhiteSpace(req.PreferredLanguage) ? "en" : req.PreferredLanguage!,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Ok(new TokenResponse(jwt.CreateToken(user)));
        });

        group.MapPost("/login", async (
            LoginRequest req, KotirauhaDbContext db, IPasswordHasher hasher, IJwtTokenService jwt) =>
        {
            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null || !user.IsActive || !hasher.Verify(req.Password, user.PasswordHash))
                return Results.Problem("Invalid email or password.", statusCode: 401);

            return Results.Ok(new TokenResponse(jwt.CreateToken(user)));
        });

        group.MapGet("/me", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return Results.Unauthorized();

            var membership = await db.GetMembershipAsync(user.Id);
            MembershipDto? dto = membership is null
                ? null
                : new MembershipDto(
                    membership.BuildingId,
                    membership.Building!.Name,
                    membership.Building!.SharedLanguage,
                    membership.Role.ToString().ToLowerInvariant(),
                    membership.ApartmentNumber);

            return Results.Ok(new CurrentUserDto(
                user.Id, user.Email, user.DisplayName, user.PreferredLanguage, user.IsPlatformAdmin, dto));
        }).RequireAuthorization();

        return api;
    }
}
