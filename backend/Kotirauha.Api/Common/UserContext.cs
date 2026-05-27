using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
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

    // V1: a user belongs to at most one building.
    public static Task<BuildingMembership?> GetMembershipAsync(
        this KotirauhaDbContext db, Guid userId, CancellationToken ct = default) =>
        db.Memberships
            .Include(m => m.Building)
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);
}
