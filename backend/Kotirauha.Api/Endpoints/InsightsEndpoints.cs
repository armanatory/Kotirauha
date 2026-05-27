using Kotirauha.Api.Common;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record CategoryCount(string Category, int Count);
public record ApartmentCount(string Apartment, int Count);
public record MonthCount(string Month, int Count);
public record RecurringPattern(string Category, string SubjectApartment, int Count, DateTimeOffset FirstAt, DateTimeOffset LastAt);
public record InsightsDto(
    int TotalEntries,
    IReadOnlyList<CategoryCount> ByCategory,
    IReadOnlyList<ApartmentCount> BySubjectApartment,
    IReadOnlyList<MonthCount> ByMonth,
    IReadOnlyList<RecurringPattern> TopRecurring);

public static class InsightsEndpoints
{
    public static RouteGroupBuilder MapInsightsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/insights", async (HttpContext ctx, KotirauhaDbContext db,
            DateTimeOffset? from, DateTimeOffset? to, bool? includeArchived) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null || m.Role is MembershipRole.Resident)
                return Results.Problem("Board access required.", statusCode: 403);

            var query = db.Entries.Where(e => e.BuildingId == m.BuildingId);
            if (!(includeArchived == true)) query = query.Where(e => e.ArchivedAt == null);
            if (from is not null) query = query.Where(e => e.OccurredAt >= from);
            if (to is not null) query = query.Where(e => e.OccurredAt <= to);

            var entries = await query
                .Select(e => new { e.Category, e.SubjectApartment, e.OccurredAt })
                .ToListAsync();

            var byCategory = entries
                .GroupBy(e => e.Category)
                .Select(g => new CategoryCount(g.Key.ToString(), g.Count()))
                .OrderByDescending(c => c.Count).ToList();

            var byApartment = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.SubjectApartment))
                .GroupBy(e => e.SubjectApartment!)
                .Select(g => new ApartmentCount(g.Key, g.Count()))
                .OrderByDescending(c => c.Count).ToList();

            var byMonth = entries
                .GroupBy(e => e.OccurredAt.ToString("yyyy-MM"))
                .Select(g => new MonthCount(g.Key, g.Count()))
                .OrderBy(c => c.Month).ToList();

            var topRecurring = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.SubjectApartment))
                .GroupBy(e => new { e.Category, Apt = e.SubjectApartment! })
                .Where(g => g.Count() >= 2)
                .Select(g => new RecurringPattern(
                    g.Key.Category.ToString(), g.Key.Apt, g.Count(),
                    g.Min(x => x.OccurredAt), g.Max(x => x.OccurredAt)))
                .OrderByDescending(p => p.Count).Take(20).ToList();

            return Results.Ok(new InsightsDto(entries.Count, byCategory, byApartment, byMonth, topRecurring));
        }).RequireAuthorization();

        return api;
    }
}
