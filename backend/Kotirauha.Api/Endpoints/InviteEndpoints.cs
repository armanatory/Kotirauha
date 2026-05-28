using Kotirauha.Api.Common;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record InvitePreviewDto(bool Valid, string BuildingName, string? Title);
public record RedeemInviteRequest(string? ApartmentNumber);

public static class InviteEndpoints
{
    public static RouteGroupBuilder MapInviteEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/invites");

        // Public preview so the landing page can show who is inviting before login.
        group.MapGet("/{token}", async (string token, KotirauhaDbContext db) =>
        {
            var invite = await db.Invites
                .Include(i => i.Building)
                .FirstOrDefaultAsync(i => i.Token == token);
            if (invite is null) return Results.NotFound();

            return Results.Ok(new InvitePreviewDto(
                invite.IsUsable(DateTimeOffset.UtcNow), invite.Building!.Name, invite.Title));
        });

        // Redeem after sign-in: joins the building as a resident, no approval.
        group.MapPost("/{token}/redeem", async (string token, RedeemInviteRequest? req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var invite = await db.Invites
                .Include(i => i.Building)
                .FirstOrDefaultAsync(i => i.Token == token);
            if (invite is null) return Results.NotFound();

            var existing = await db.GetMembershipAsync(userId.Value);
            if (existing is not null)
            {
                // Already in this building: treat as success so the link is idempotent.
                if (existing.BuildingId == invite.BuildingId)
                    return Results.Ok(new BuildingDto(
                        invite.BuildingId, invite.Building!.Name, invite.Building!.Address,
                        invite.Building!.SharedLanguage, existing.Role.ToString().ToLowerInvariant(), null));
                return Results.Problem("You already belong to a building.", statusCode: 409);
            }

            if (!invite.IsUsable(DateTimeOffset.UtcNow))
                return Results.Problem("This invitation link is no longer valid.", statusCode: 410);

            db.Memberships.Add(new BuildingMembership
            {
                UserId = userId.Value,
                BuildingId = invite.BuildingId,
                Role = MembershipRole.Resident,
                ApartmentNumber = string.IsNullOrWhiteSpace(req?.ApartmentNumber) ? null : req.ApartmentNumber.Trim(),
                JoinedVia = "invite",
                InviteId = invite.Id,
            });
            invite.UsedCount++;
            await db.SaveChangesAsync();

            return Results.Ok(new BuildingDto(
                invite.BuildingId, invite.Building!.Name, invite.Building!.Address,
                invite.Building!.SharedLanguage, "resident", null));
        }).RequireAuthorization();

        return api;
    }
}
