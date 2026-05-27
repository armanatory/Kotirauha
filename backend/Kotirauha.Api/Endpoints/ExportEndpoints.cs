using System.Net;
using System.Text;
using Kotirauha.Api.Common;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record ExportRequest(
    DateTimeOffset? From, DateTimeOffset? To, string[]? Categories,
    string? SubjectApartment, bool IncludeArchived);

public static class ExportEndpoints
{
    public static RouteGroupBuilder MapExportEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/export").RequireAuthorization();

        // Returns a self-contained printable HTML report (print to PDF in the browser).
        group.MapPost("/", async (ExportRequest req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null) return Results.Problem("Join a building first.", statusCode: 403);

            var me = await db.Users.FirstAsync(u => u.Id == userId);
            var canSeeArchived = m.Role is MembershipRole.Board or MembershipRole.Admin;

            var query = db.Entries
                .Where(e => e.BuildingId == m.BuildingId)
                .Include(e => e.Reporter)
                .Include(e => e.Translations)
                .Include(e => e.Attachments)
                .AsQueryable();

            if (!(req.IncludeArchived && canSeeArchived))
                query = query.Where(e => e.ArchivedAt == null);
            if (req.From is not null) query = query.Where(e => e.OccurredAt >= req.From);
            if (req.To is not null) query = query.Where(e => e.OccurredAt <= req.To);
            if (!string.IsNullOrWhiteSpace(req.SubjectApartment))
                query = query.Where(e => e.SubjectApartment == req.SubjectApartment);

            var cats = (req.Categories ?? [])
                .Select(c => Enum.TryParse<IncidentCategory>(c, true, out var v) ? (IncidentCategory?)v : null)
                .Where(v => v is not null).Select(v => v!.Value).ToHashSet();
            if (cats.Count > 0) query = query.Where(e => cats.Contains(e.Category));

            var entries = await query.OrderBy(e => e.OccurredAt).ToListAsync();

            var html = BuildHtml(m.Building!, me.DisplayName, entries, req);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        return api;
    }

    private static string BuildHtml(Building building, string generatedBy, List<IncidentEntry> entries, ExportRequest req)
    {
        string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder();
        sb.Append($$"""
        <!doctype html><html><head><meta charset="utf-8"><title>Kotirauha report — {{E(building.Name)}}</title>
        <style>
          body{font-family:system-ui,Segoe UI,Roboto,sans-serif;color:#1f2933;margin:32px;line-height:1.45}
          h1{font-size:22px;margin:0 0 4px} .meta{color:#52606d;font-size:13px;margin-bottom:24px}
          .entry{border:1px solid #e4e7eb;border-radius:8px;padding:14px 16px;margin-bottom:14px;page-break-inside:avoid}
          .head{font-size:13px;color:#52606d;margin-bottom:8px;display:flex;flex-wrap:wrap;gap:12px}
          .cat{font-weight:600;color:#1f2933}
          .label{font-size:11px;text-transform:uppercase;letter-spacing:.04em;color:#7b8794;margin:8px 0 2px}
          .orig{white-space:pre-wrap} .trans{white-space:pre-wrap;background:#f5f7fa;border-radius:6px;padding:8px}
          .notice{font-size:11px;color:#7b8794;font-style:italic;margin-top:4px}
          .flag{display:inline-block;font-size:11px;padding:1px 6px;border-radius:4px;background:#fde8e8;color:#9b2c2c}
          .footer{margin-top:28px;border-top:1px solid #e4e7eb;padding-top:12px;font-size:12px;color:#7b8794}
        </style></head><body>
        """);

        sb.Append($"<h1>Kotirauha — {E(building.Name)}</h1>");
        sb.Append("<div class=\"meta\">");
        if (!string.IsNullOrWhiteSpace(building.Address)) sb.Append($"{E(building.Address)}<br>");
        sb.Append($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC by {E(generatedBy)}<br>");
        sb.Append($"Entries: {entries.Count}");
        if (req.From is not null || req.To is not null)
            sb.Append($" · Range: {req.From?.ToString("yyyy-MM-dd") ?? "…"} – {req.To?.ToString("yyyy-MM-dd") ?? "…"}");
        sb.Append("</div>");

        foreach (var e in entries)
        {
            sb.Append("<div class=\"entry\">");
            sb.Append("<div class=\"head\">");
            sb.Append($"<span class=\"cat\">{E(e.Category.ToString())}</span>");
            sb.Append($"<span>Occurred: {e.OccurredAt:yyyy-MM-dd HH:mm}</span>");
            sb.Append($"<span>Logged: {e.CreatedAt:yyyy-MM-dd HH:mm}</span>");
            sb.Append($"<span>Reporter: {E(e.Reporter!.DisplayName)}</span>");
            if (!string.IsNullOrWhiteSpace(e.SubjectApartment)) sb.Append($"<span>Location: {E(e.SubjectApartment)}</span>");
            if (e.EditedAt is not null) sb.Append("<span class=\"flag\">edited</span>");
            if (e.ArchivedAt is not null) sb.Append("<span class=\"flag\">archived</span>");
            sb.Append("</div>");

            sb.Append($"<div class=\"label\">Original ({E(e.OriginalLanguage)})</div>");
            sb.Append($"<div class=\"orig\">{E(e.OriginalText)}</div>");

            var trans = e.Translations.FirstOrDefault(t => t.TargetLanguage == building.SharedLanguage && t.Status == TranslationStatus.Completed);
            if (trans is not null)
            {
                sb.Append($"<div class=\"label\">Translation ({E(building.SharedLanguage)})</div>");
                sb.Append($"<div class=\"trans\">{E(trans.TranslatedText)}</div>");
                sb.Append($"<div class=\"notice\">AI-generated translation from {E(e.OriginalLanguage)}.</div>");
            }
            if (e.Attachments.Any()) sb.Append("<div class=\"notice\">[image attachment on file]</div>");
            sb.Append("</div>");
        }

        sb.Append("""
        <div class="footer">This document is a factual record of reported observations.
        It does not assign guilt and is not a legal determination.</div>
        </body></html>
        """);
        return sb.ToString();
    }
}
