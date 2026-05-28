using System.Security.Cryptography;
using System.Text;
using Kotirauha.Api.Common;
using Kotirauha.Core.Abstractions;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record MagicLinkRequest(string Email, string? DisplayName, string? PreferredLanguage);
public record VerifyRequest(string Token);
public record VerifyResponse(string Token, bool ProfileComplete);
public record UpdateProfileRequest(string? DisplayName, string? PreferredLanguage);

public record MembershipDto(Guid BuildingId, string BuildingName, string SharedLanguage, string Role, string? ApartmentNumber);
public record CurrentUserDto(Guid Id, string Email, string DisplayName, string PreferredLanguage, bool IsAdmin, MembershipDto? Membership);

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth");

        // Request a one-time login link by email (passwordless).
        group.MapPost("/magic-link", async (
            MagicLinkRequest req, HttpContext ctx, KotirauhaDbContext db,
            IEmailSender email, IWebHostEnvironment envInfo) =>
        {
            var address = req.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(address) || !address.Contains('@'))
                return Results.Problem("A valid email is required.", statusCode: 400);

            var lang = NormalizeLang(req.PreferredLanguage);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address);
            if (user is null)
            {
                user = new User { Email = address, DisplayName = req.DisplayName?.Trim() ?? "", PreferredLanguage = lang };
                db.Users.Add(user);
            }
            else if (string.IsNullOrWhiteSpace(user.DisplayName) && !string.IsNullOrWhiteSpace(req.DisplayName))
            {
                user.DisplayName = req.DisplayName!.Trim();
                user.PreferredLanguage = lang;
            }

            // Project owner / configured operators are platform admins.
            if (AdminConfig.IsAdminEmail(address) && !user.IsPlatformAdmin) user.IsPlatformAdmin = true;

            var rawToken = Base64Url(RandomNumberGenerator.GetBytes(32));
            db.MagicLinkTokens.Add(new MagicLinkToken
            {
                Email = address,
                TokenHash = Sha256(rawToken),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(20),
            });
            await db.SaveChangesAsync();

            var appBase = (Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5173").TrimEnd('/');
            var link = $"{appBase}/auth/verify?token={rawToken}";

            try
            {
                await email.SendAsync(
                    address,
                    "Your Kotirauha login link",
                    $"<p>Tap the button to sign in to Kotirauha. The link works once and expires in 20 minutes.</p>" +
                    $"<p><a href=\"{link}\">Sign in to Kotirauha</a></p>" +
                    $"<p>If you did not request this, you can ignore this email.</p>",
                    $"Sign in to Kotirauha (link works once, expires in 20 minutes):\n{link}");
            }
            catch
            {
                // Never reveal send failures to the caller; the link is still valid.
            }

            // In development we return the link so the flow is testable without an inbox.
            return envInfo.IsDevelopment()
                ? Results.Ok(new { sent = true, devLink = link })
                : Results.Ok(new { sent = true });
        });

        // Exchange a magic-link token for a JWT session.
        group.MapPost("/verify", async (VerifyRequest req, KotirauhaDbContext db, IJwtTokenService jwt) =>
        {
            if (string.IsNullOrWhiteSpace(req.Token)) return Results.Problem("Missing token.", statusCode: 400);

            var hash = Sha256(req.Token.Trim());
            var now = DateTimeOffset.UtcNow;
            var token = await db.MagicLinkTokens
                .Where(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now)
                .FirstOrDefaultAsync();
            if (token is null) return Results.Problem("This link is invalid or has expired.", statusCode: 400);

            token.UsedAt = now;
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == token.Email);
            if (user is null) return Results.Problem("Account not found.", statusCode: 400);
            if (!user.IsActive) return Results.Problem("This account is deactivated.", statusCode: 403);
            await db.SaveChangesAsync();

            return Results.Ok(new VerifyResponse(jwt.CreateToken(user), !string.IsNullOrWhiteSpace(user.DisplayName)));
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

        group.MapPatch("/me", async (UpdateProfileRequest req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return Results.Unauthorized();

            if (!string.IsNullOrWhiteSpace(req.DisplayName)) user.DisplayName = req.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(req.PreferredLanguage)) user.PreferredLanguage = NormalizeLang(req.PreferredLanguage);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = user.Id, displayName = user.DisplayName, preferredLanguage = user.PreferredLanguage });
        }).RequireAuthorization();

        return api;
    }

    // Bilingual product: only English and Finnish are supported.
    private static string NormalizeLang(string? lang)
    {
        var l = lang?.Trim().ToLowerInvariant();
        return l == "en" ? "en" : "fi";
    }

    private static string Sha256(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
