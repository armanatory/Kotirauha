using Kotirauha.Api.Common;
using Kotirauha.Core.Abstractions;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record AdminOverviewDto(int Users, int Buildings, int Entries, int ArchivedEntries, int Translations);
public record AdminBuildingDto(Guid Id, string Name, string SharedLanguage, int Members, int Entries);
public record TranslationStatusDto(string Provider, bool IsStub, string Note);
public record AdminUserDto(Guid Id, string Email, string DisplayName, bool IsAdmin, Guid? BuildingId, string? BuildingName, string? Role);
public record AssignRequest(Guid UserId, Guid? BuildingId, string Role);

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin").RequireAuthorization();

        group.MapGet("/overview", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Forbidden();
            return Results.Ok(new AdminOverviewDto(
                await db.Users.CountAsync(),
                await db.Buildings.CountAsync(),
                await db.Entries.CountAsync(),
                await db.Entries.CountAsync(e => e.ArchivedAt != null),
                await db.Translations.CountAsync()));
        });

        group.MapGet("/buildings", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Forbidden();
            var buildings = await db.Buildings
                .OrderBy(b => b.Name)
                .Select(b => new { b.Id, b.Name, b.SharedLanguage, Members = b.Memberships.Count() })
                .ToListAsync();
            var entryCounts = (await db.Entries
                .GroupBy(e => e.BuildingId)
                .Select(g => new { BuildingId = g.Key, Count = g.Count() })
                .ToListAsync())
                .ToDictionary(x => x.BuildingId, x => x.Count);

            var rows = buildings.Select(b => new AdminBuildingDto(
                b.Id, b.Name, b.SharedLanguage, b.Members, entryCounts.GetValueOrDefault(b.Id))).ToList();
            return Results.Ok(rows);
        });

        group.MapGet("/users", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Forbidden();
            var users = await db.Users.OrderBy(u => u.Email).ToListAsync();
            var memberships = await db.Memberships.Include(m => m.Building).ToListAsync();

            var rows = users.Select(u =>
            {
                var m = memberships.FirstOrDefault(x => x.UserId == u.Id);
                return new AdminUserDto(
                    u.Id, u.Email, u.DisplayName, u.IsPlatformAdmin,
                    m?.BuildingId, m?.Building?.Name, m?.Role.ToString().ToLowerInvariant());
            }).ToList();
            return Results.Ok(rows);
        });

        // Assign (move) a user to a building with a role, or remove them when BuildingId is null.
        group.MapPost("/assign", async (AssignRequest req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Forbidden();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId);
            if (user is null) return Results.Problem("User not found.", statusCode: 404);

            var existing = await db.Memberships.Where(m => m.UserId == req.UserId).ToListAsync();
            db.Memberships.RemoveRange(existing);

            if (req.BuildingId is not null)
            {
                var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == req.BuildingId);
                if (building is null) return Results.Problem("Building not found.", statusCode: 404);
                if (!Enum.TryParse<MembershipRole>(req.Role, true, out var role)) role = MembershipRole.Resident;

                db.Memberships.Add(new BuildingMembership
                {
                    UserId = req.UserId,
                    BuildingId = req.BuildingId.Value,
                    Role = role,
                });
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        group.MapGet("/translation-status", async (HttpContext ctx, KotirauhaDbContext db, ITranslationProvider provider) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Forbidden();
            var isStub = provider.Name == "stub";
            var note = isStub
                ? "Offline stub active. Translations are placeholders. Set OPENAI_API_KEY (or TRANSLATION_PROVIDER) for real translation."
                : "Real translation provider configured.";
            return Results.Ok(new TranslationStatusDto(provider.Name, isStub, note));
        });

        return api;
    }

    private static IResult Forbidden() => Results.Problem("Platform admin access required.", statusCode: 403);
}
