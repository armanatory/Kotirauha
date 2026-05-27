using Kotirauha.Api.Common;
using Kotirauha.Core.Abstractions;
using Kotirauha.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record AdminOverviewDto(int Users, int Buildings, int Entries, int ArchivedEntries, int Translations);
public record AdminBuildingDto(Guid Id, string Name, string SharedLanguage, int Members, int Entries);
public record TranslationStatusDto(string Provider, bool IsStub, string Note);

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin").RequireAuthorization();

        group.MapGet("/overview", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await IsAdminAsync(ctx, db)) return Results.Problem("Platform admin access required.", statusCode: 403);
            return Results.Ok(new AdminOverviewDto(
                await db.Users.CountAsync(),
                await db.Buildings.CountAsync(),
                await db.Entries.CountAsync(),
                await db.Entries.CountAsync(e => e.ArchivedAt != null),
                await db.Translations.CountAsync()));
        });

        group.MapGet("/buildings", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await IsAdminAsync(ctx, db)) return Results.Problem("Platform admin access required.", statusCode: 403);

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
                b.Id, b.Name, b.SharedLanguage, b.Members,
                entryCounts.GetValueOrDefault(b.Id))).ToList();
            return Results.Ok(rows);
        });

        group.MapGet("/translation-status", async (HttpContext ctx, KotirauhaDbContext db, ITranslationProvider provider) =>
        {
            if (!await IsAdminAsync(ctx, db)) return Results.Problem("Platform admin access required.", statusCode: 403);
            var isStub = provider.Name == "stub";
            var note = isStub
                ? "Offline stub active — translations are placeholders. Set TRANSLATION_PROVIDER=anthropic and ANTHROPIC_API_KEY for real translation."
                : "Real translation provider configured.";
            return Results.Ok(new TranslationStatusDto(provider.Name, isStub, note));
        });

        return api;
    }

    private static async Task<bool> IsAdminAsync(HttpContext ctx, KotirauhaDbContext db)
    {
        var userId = ctx.User.GetUserId();
        if (userId is null) return false;
        return await db.Users.AnyAsync(u => u.Id == userId && u.IsPlatformAdmin);
    }
}
