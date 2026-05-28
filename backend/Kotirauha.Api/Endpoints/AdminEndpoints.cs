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
public record AdminCreateBuildingRequest(string Name, string? Address, string? SharedLanguage);
public record CountRow(string Label, int Count);
public record DayCount(string Date, int Count);
public record AnalyticsDto(
    int TotalVisits, int UniqueVisitors, int Days, bool GeoEnabled,
    List<DayCount> ByDay, List<CountRow> TopPages, List<CountRow> TopReferrers,
    List<CountRow> ByLanguage, List<CountRow> ByCountry);

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

        // Create a building (platform admin only). No membership is added; the
        // admin assigns a board member afterwards via /admin/assign.
        group.MapPost("/buildings", async (AdminCreateBuildingRequest req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Forbidden();
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.Problem("Building name is required.", statusCode: 400);

            var building = new Building
            {
                Name = req.Name.Trim(),
                Address = string.IsNullOrWhiteSpace(req.Address) ? null : req.Address.Trim(),
                SharedLanguage = req.SharedLanguage == "en" ? "en" : "fi",
                JoinCode = await BuildingEndpoints.GenerateUniqueJoinCodeAsync(db),
            };
            db.Buildings.Add(building);
            await db.SaveChangesAsync();
            return Results.Ok(new AdminBuildingDto(building.Id, building.Name, building.SharedLanguage, 0, 0));
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
                    JoinedVia = "admin",
                });
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        // Delete a user (e.g. test accounts). Blocked if they have filed entries,
        // so incident records are never orphaned; and you can't delete yourself.
        group.MapDelete("/users/{userId:guid}", async (Guid userId, HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Forbidden();
            if (ctx.User.GetUserId() == userId)
                return Results.Problem("You cannot delete your own account.", statusCode: 400);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return Results.NotFound();

            if (await db.Entries.AnyAsync(e => e.ReporterUserId == userId))
                return Results.Problem("This user has filed reports, so they cannot be deleted (records must be preserved).", statusCode: 409);

            // Memberships and join requests cascade; clean up their magic-link tokens too.
            var tokens = await db.MagicLinkTokens.Where(t => t.Email == user.Email).ToListAsync();
            db.MagicLinkTokens.RemoveRange(tokens);
            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return Results.Ok(new { deleted = true });
        });

        group.MapGet("/analytics", async (HttpContext ctx, KotirauhaDbContext db, IGeoIpService geo, int? days) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Forbidden();

            var window = Math.Clamp(days ?? 30, 1, 365);
            var since = DateTimeOffset.UtcNow.AddDays(-window);
            var events = db.VisitEvents.Where(v => v.CreatedAt >= since);

            var total = await events.CountAsync();
            var unique = await events.Where(v => v.VisitorHash != null)
                .Select(v => v.VisitorHash).Distinct().CountAsync();

            // Pull just the columns we aggregate; grouping happens in memory so the
            // day series and label rollups stay simple and provider-agnostic.
            var rows = await events
                .Select(v => new { v.CreatedAt, v.Path, v.Referrer, v.Language, v.Country, v.VisitorHash })
                .ToListAsync();

            var byDay = rows
                .GroupBy(r => r.CreatedAt.UtcDateTime.Date)
                .OrderBy(g => g.Key)
                .Select(g => new DayCount(g.Key.ToString("yyyy-MM-dd"), g.Count()))
                .ToList();

            var topPages = rows.Where(r => !string.IsNullOrEmpty(r.Path))
                .GroupBy(r => r.Path).Select(g => new CountRow(g.Key, g.Count()))
                .OrderByDescending(x => x.Count).Take(10).ToList();
            var topReferrers = rows.Where(r => !string.IsNullOrWhiteSpace(r.Referrer))
                .GroupBy(r => r.Referrer!).Select(g => new CountRow(g.Key, g.Count()))
                .OrderByDescending(x => x.Count).Take(10).ToList();
            var byLanguage = rows.Where(r => !string.IsNullOrWhiteSpace(r.Language))
                .GroupBy(r => r.Language!).Select(g => new CountRow(g.Key, g.Count()))
                .OrderByDescending(x => x.Count).ToList();
            var byCountry = rows.Where(r => !string.IsNullOrWhiteSpace(r.Country))
                .GroupBy(r => r.Country!).Select(g => new CountRow(g.Key, g.Count()))
                .OrderByDescending(x => x.Count).Take(20).ToList();

            return Results.Ok(new AnalyticsDto(
                total, unique, window, geo.Enabled,
                byDay, topPages, topReferrers, byLanguage, byCountry));
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
