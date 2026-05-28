using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Common;

public static class UserContext
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    // Real client IP. Behind the Caddy reverse proxy the socket address is the
    // proxy, so prefer the first X-Forwarded-For hop when present.
    public static string ClientIp(this HttpContext ctx)
    {
        var fwd = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(fwd))
        {
            var first = fwd.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first)) return first;
        }
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    // V1: a user belongs to at most one building.
    public static Task<BuildingMembership?> GetMembershipAsync(
        this KotirauhaDbContext db, Guid userId, CancellationToken ct = default) =>
        db.Memberships
            .Include(m => m.Building)
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);
}
