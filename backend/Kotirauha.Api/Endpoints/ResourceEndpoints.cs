using Kotirauha.Api.Common;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record ResourceLinkDto(Guid Id, string Title, string? Description, string Url, int SortOrder);
public record CreateResourceRequest(string Title, string? Description, string Url, int? SortOrder);

public static class ResourceEndpoints
{
    public static RouteGroupBuilder MapResourceEndpoints(this RouteGroupBuilder api)
    {
        // Any signed-in member can read the curated links.
        api.MapGet("/resources", async (KotirauhaDbContext db) =>
        {
            var links = await db.ResourceLinks
                .OrderBy(r => r.SortOrder).ThenBy(r => r.CreatedAt)
                .Select(r => new ResourceLinkDto(r.Id, r.Title, r.Description, r.Url, r.SortOrder))
                .ToListAsync();
            return Results.Ok(links);
        }).RequireAuthorization();

        // Platform admin manages the links.
        var admin = api.MapGroup("/admin/resources").RequireAuthorization();

        admin.MapPost("/", async (CreateResourceRequest req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Results.Problem("Platform admin access required.", statusCode: 403);
            if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Url))
                return Results.Problem("Title and URL are required.", statusCode: 400);

            var url = req.Url.Trim();
            if (!url.StartsWith("http://") && !url.StartsWith("https://")) url = "https://" + url;

            var link = new ResourceLink
            {
                Title = req.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
                Url = url,
                SortOrder = req.SortOrder ?? 0,
            };
            db.ResourceLinks.Add(link);
            await db.SaveChangesAsync();
            return Results.Ok(new ResourceLinkDto(link.Id, link.Title, link.Description, link.Url, link.SortOrder));
        });

        admin.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, KotirauhaDbContext db) =>
        {
            if (!await AdminGuard.IsAdminAsync(ctx, db)) return Results.Problem("Platform admin access required.", statusCode: 403);
            var link = await db.ResourceLinks.FirstOrDefaultAsync(r => r.Id == id);
            if (link is null) return Results.NotFound();
            db.ResourceLinks.Remove(link);
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        return api;
    }
}
