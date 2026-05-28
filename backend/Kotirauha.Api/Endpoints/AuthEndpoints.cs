using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using Kotirauha.Api.Common;
using Kotirauha.Core.Abstractions;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Kotirauha.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record MagicLinkRequest(string Email, string? DisplayName, string? PreferredLanguage);
public record VerifyRequest(string Token);
public record VerifyCodeRequest(string Email, string Code);
public record VerifyResponse(string Token, bool ProfileComplete);
public record UpdateProfileRequest(string? DisplayName, string? PreferredLanguage);
public record GoogleSignInRequest(string Credential);

public record MembershipDto(Guid BuildingId, string BuildingName, string SharedLanguage, string Role, string? ApartmentNumber);
public record CurrentUserDto(Guid Id, string Email, string DisplayName, string PreferredLanguage, bool IsAdmin, MembershipDto? Membership);

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth");

        // Public client config (e.g. whether Google sign-in is available).
        group.MapGet("/config", () =>
        {
            var googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
            return Results.Ok(new { googleClientId = string.IsNullOrWhiteSpace(googleClientId) ? null : googleClientId });
        });

        // Sign in with a Google ID token. Google has already verified the email,
        // so no magic-link step is needed.
        group.MapPost("/google", async (GoogleSignInRequest req, KotirauhaDbContext db, IJwtTokenService jwt) =>
        {
            var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
            if (string.IsNullOrWhiteSpace(clientId))
                return Results.Problem("Google sign-in is not configured.", statusCode: 400);
            if (string.IsNullOrWhiteSpace(req.Credential))
                return Results.Problem("Missing Google credential.", statusCode: 400);

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(
                    req.Credential,
                    new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
            }
            catch
            {
                return Results.Problem("Google sign-in failed.", statusCode: 401);
            }

            if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
                return Results.Problem("Your Google email is not verified.", statusCode: 401);

            var address = payload.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address);
            if (user is null)
            {
                user = new User { Email = address, DisplayName = payload.Name?.Trim() ?? "", PreferredLanguage = "fi" };
                db.Users.Add(user);
            }
            else if (string.IsNullOrWhiteSpace(user.DisplayName) && !string.IsNullOrWhiteSpace(payload.Name))
            {
                user.DisplayName = payload.Name!.Trim();
            }

            if (AdminConfig.IsAdminEmail(address) && !user.IsPlatformAdmin) user.IsPlatformAdmin = true;
            if (!user.IsActive) return Results.Problem("This account is deactivated.", statusCode: 403);
            await db.SaveChangesAsync();

            return Results.Ok(new VerifyResponse(jwt.CreateToken(user), !string.IsNullOrWhiteSpace(user.DisplayName)));
        });

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

            // Invalidate earlier unused codes/links for this email so only the
            // newest email works (latest-wins, like a typical OTP).
            var now = DateTimeOffset.UtcNow;
            await db.MagicLinkTokens
                .Where(t => t.Email == address && t.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now));

            var rawToken = Base64Url(RandomNumberGenerator.GetBytes(32));
            var loginCode = GenerateNumericCode(6);
            db.MagicLinkTokens.Add(new MagicLinkToken
            {
                Email = address,
                TokenHash = Sha256(rawToken),
                CodeHash = Sha256(loginCode),
                ExpiresAt = now.AddMinutes(20),
            });
            await db.SaveChangesAsync();

            var appBase = (Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5173").TrimEnd('/');
            var link = $"{appBase}/auth/verify?token={rawToken}";

            var (subject, htmlBody, textBody) = EmailTemplates.RenderMagicLink(link, loginCode, user.PreferredLanguage);
            try
            {
                await email.SendAsync(address, subject, htmlBody, textBody);
            }
            catch
            {
                // Never reveal send failures to the caller; the link/code are still valid.
            }

            // In development we return the link + code so the flow is testable without an inbox.
            return envInfo.IsDevelopment()
                ? Results.Ok(new { sent = true, devLink = link, devCode = loginCode })
                : Results.Ok(new { sent = true });
        });

        // Exchange a 6-digit code (typed from the email) for a JWT session.
        group.MapPost("/verify-code", async (VerifyCodeRequest req, KotirauhaDbContext db, IJwtTokenService jwt) =>
        {
            var address = req.Email?.Trim().ToLowerInvariant();
            var raw = new string((req.Code ?? "").Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(address) || raw.Length < 4)
                return Results.Problem("Enter the code from your email.", statusCode: 400);

            var now = DateTimeOffset.UtcNow;
            var row = await db.MagicLinkTokens
                .Where(t => t.Email == address && t.UsedAt == null && t.ExpiresAt > now)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();
            if (row is null) return Results.Problem("This code is invalid or has expired.", statusCode: 400);

            if (row.Attempts >= 8)
            {
                row.UsedAt = now;
                await db.SaveChangesAsync();
                return Results.Problem("Too many attempts. Request a new code.", statusCode: 429);
            }
            if (Sha256(raw) != row.CodeHash)
            {
                row.Attempts++;
                await db.SaveChangesAsync();
                return Results.Problem("Incorrect code.", statusCode: 400);
            }

            row.UsedAt = now;
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address);
            if (user is null) return Results.Problem("Account not found.", statusCode: 400);
            if (!user.IsActive) return Results.Problem("This account is deactivated.", statusCode: 403);
            await db.SaveChangesAsync();

            return Results.Ok(new VerifyResponse(jwt.CreateToken(user), !string.IsNullOrWhiteSpace(user.DisplayName)));
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

    // Cryptographically-random N-digit code. Rejection sampling keeps the
    // digit distribution flat (no modulo bias).
    private static string GenerateNumericCode(int digits)
    {
        var sb = new StringBuilder(digits);
        Span<byte> buf = stackalloc byte[1];
        while (sb.Length < digits)
        {
            RandomNumberGenerator.Fill(buf);
            if (buf[0] >= 250) continue;
            sb.Append((char)('0' + (buf[0] % 10)));
        }
        return sb.ToString();
    }
}
