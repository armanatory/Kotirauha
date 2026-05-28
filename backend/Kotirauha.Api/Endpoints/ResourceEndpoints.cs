using Kotirauha.Api.Common;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record ResourceLinkDto(Guid Id, string Title, string? Description, string Url, int SortOrder, Guid? BuildingId);
public record CreateResourceRequest(string Title, string? Description, string Url, int? SortOrder);

public static class ResourceEndpoints
{
    public static RouteGroupBuilder MapResourceEndpoints(this RouteGroupBuilder api)
    {
        // Any signed-in member: platform-wide links + their own building's links.
        api.MapGet("/resources", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            var buildingId = m?.BuildingId;

            var links = await db.ResourceLinks
                .Where(r => r.BuildingId == null || r.BuildingId == buildingId)
                .OrderBy(r => r.BuildingId == null ? 0 : 1)
                .ThenBy(r => r.SortOrder).ThenBy(r => r.CreatedAt)
                .Select(r => new ResourceLinkDto(r.Id, r.Title, r.Description, r.Url, r.SortOrder, r.BuildingId))
                .ToListAsync();
            return Results.Ok(links);
        }).RequireAuthorization();

        // ── Platform admin manages the global (BuildingId == null) links. ──
        var admin = api.MapGroup("/admin/resources").RequireAuthorization();

        admin.MapGet("/", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Forbidden();
            var links = await db.ResourceLinks
                .Where(r => r.BuildingId == null)
                .OrderBy(r => r.SortOrder).ThenBy(r => r.CreatedAt)
                .Select(r => new ResourceLinkDto(r.Id, r.Title, r.Description, r.Url, r.SortOrder, r.BuildingId))
                .ToListAsync();
            return Results.Ok(links);
        });

        admin.MapPost("/", async (CreateResourceRequest req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Forbidden();
            return await CreateAsync(db, req, buildingId: null);
        });

        admin.MapPut("/{id:guid}", async (Guid id, CreateResourceRequest req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Forbidden();
            var link = await db.ResourceLinks.FirstOrDefaultAsync(r => r.Id == id && r.BuildingId == null);
            return await UpdateAsync(db, link, req);
        });

        admin.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Forbidden();
            var link = await db.ResourceLinks.FirstOrDefaultAsync(r => r.Id == id && r.BuildingId == null);
            if (link is null) return Results.NotFound();
            db.ResourceLinks.Remove(link);
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        // ── A building's board manages its own links. ──
        var board = api.MapGroup("/buildings/resources").RequireAuthorization();

        board.MapPost("/", async (CreateResourceRequest req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var m = await RequireBoardAsync(ctx, db);
            if (m is null) return BoardOnly();
            return await CreateAsync(db, req, buildingId: m.BuildingId);
        });

        board.MapPut("/{id:guid}", async (Guid id, CreateResourceRequest req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var m = await RequireBoardAsync(ctx, db);
            if (m is null) return BoardOnly();
            var link = await db.ResourceLinks.FirstOrDefaultAsync(r => r.Id == id && r.BuildingId == m.BuildingId);
            return await UpdateAsync(db, link, req);
        });

        board.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var m = await RequireBoardAsync(ctx, db);
            if (m is null) return BoardOnly();
            var link = await db.ResourceLinks.FirstOrDefaultAsync(r => r.Id == id && r.BuildingId == m.BuildingId);
            if (link is null) return Results.NotFound();
            db.ResourceLinks.Remove(link);
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        return api;
    }

    private static async Task<BuildingMembership?> RequireBoardAsync(HttpContext ctx, KotirauhaDbContext db)
    {
        var userId = ctx.User.GetUserId();
        if (userId is null) return null;
        var m = await db.GetMembershipAsync(userId.Value);
        return (m is null || m.Role is MembershipRole.Resident) ? null : m;
    }

    private static async Task<IResult> CreateAsync(KotirauhaDbContext db, CreateResourceRequest req, Guid? buildingId)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Url))
            return Results.Problem("Title and URL are required.", statusCode: 400);
        var link = new ResourceLink
        {
            BuildingId = buildingId,
            Title = req.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            Url = NormalizeUrl(req.Url),
            SortOrder = req.SortOrder ?? 0,
        };
        db.ResourceLinks.Add(link);
        await db.SaveChangesAsync();
        return Results.Ok(new ResourceLinkDto(link.Id, link.Title, link.Description, link.Url, link.SortOrder, link.BuildingId));
    }

    private static async Task<IResult> UpdateAsync(KotirauhaDbContext db, ResourceLink? link, CreateResourceRequest req)
    {
        if (link is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Url))
            return Results.Problem("Title and URL are required.", statusCode: 400);
        link.Title = req.Title.Trim();
        link.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        link.Url = NormalizeUrl(req.Url);
        if (req.SortOrder is not null) link.SortOrder = req.SortOrder.Value;
        await db.SaveChangesAsync();
        return Results.Ok(new ResourceLinkDto(link.Id, link.Title, link.Description, link.Url, link.SortOrder, link.BuildingId));
    }

    private static string NormalizeUrl(string raw)
    {
        var url = raw.Trim();
        if (!url.StartsWith("http://") && !url.StartsWith("https://")) url = "https://" + url;
        return url;
    }

    private static IResult Forbidden() => Results.Problem("Platform admin access required.", statusCode: 403);
    private static IResult BoardOnly() => Results.Problem("Board access required.", statusCode: 403);
}
