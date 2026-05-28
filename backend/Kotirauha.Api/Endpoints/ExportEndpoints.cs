using System.Net;
using System.Text;
using Kotirauha.Api.Common;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record ExportRequest(
    DateTimeOffset? From, DateTimeOffset? To, string[]? Categories,
    string? SubjectApartment, bool IncludeArchived, string? Format);

public static class ExportEndpoints
{
    public static RouteGroupBuilder MapExportEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/export").RequireAuthorization();

        group.MapPost("/", async (ExportRequest req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null) return Results.Problem("Join a building first.", statusCode: 403);

            var me = await db.Users.FirstAsync(u => u.Id == userId);
            var lang = me.PreferredLanguage == "en" ? "en" : "fi";
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

            var format = (req.Format ?? "pdf").ToLowerInvariant();
            if (format == "excel")
            {
                var html = BuildTable(m.Building!, me.DisplayName, entries, req, lang);
                return Results.File(Bom(html), "application/vnd.ms-excel", "kotirauha-raportti.xls");
            }
            if (format == "word")
            {
                var html = BuildReport(m.Building!, me.DisplayName, entries, req, lang);
                return Results.File(Bom(html), "application/msword", "kotirauha-raportti.doc");
            }
            // pdf: printable HTML (the client prints to PDF)
            return Results.Content(BuildReport(m.Building!, me.DisplayName, entries, req, lang), "text/html; charset=utf-8");
        });

        return api;
    }

    private static byte[] Bom(string html) =>
        [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(html)];

    private static string BuildReport(Building b, string generatedBy, List<IncidentEntry> entries, ExportRequest req, string lang)
    {
        var L = Labels(lang);
        string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder();
        sb.Append($$"""
        <!doctype html><html><head><meta charset="utf-8"><title>{{E(L.Title)}} — {{E(b.Name)}}</title>
        <style>
          body{font-family:system-ui,Segoe UI,Roboto,sans-serif;color:#1f2933;margin:32px;line-height:1.45}
          h1{font-size:22px;margin:0 0 4px;color:#0f766e} .meta{color:#52606d;font-size:13px;margin-bottom:24px}
          .entry{border:1px solid #e4e7eb;border-radius:8px;padding:14px 16px;margin-bottom:14px;page-break-inside:avoid}
          .head{font-size:13px;color:#52606d;margin-bottom:8px;display:flex;flex-wrap:wrap;gap:12px}
          .cat{font-weight:600;color:#1f2933}
          .label{font-size:11px;text-transform:uppercase;letter-spacing:.04em;color:#7b8794;margin:8px 0 2px}
          .orig{white-space:pre-wrap} .trans{white-space:pre-wrap;background:#ecfdf5;border-radius:6px;padding:8px}
          .notice{font-size:11px;color:#7b8794;font-style:italic;margin-top:4px}
          .flag{display:inline-block;font-size:11px;padding:1px 6px;border-radius:4px;background:#fde8e8;color:#9b2c2c}
          .footer{margin-top:28px;border-top:1px solid #e4e7eb;padding-top:12px;font-size:12px;color:#7b8794}
        </style></head><body>
        """);
        sb.Append($"<h1>Kotirauha — {E(b.Name)}</h1><div class=\"meta\">");
        if (!string.IsNullOrWhiteSpace(b.Address)) sb.Append($"{E(b.Address)}<br>");
        sb.Append($"{E(L.Generated)} {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC · {E(generatedBy)}<br>");
        sb.Append($"{E(L.Entries)}: {entries.Count}");
        if (req.From is not null || req.To is not null)
            sb.Append($" · {E(L.Range)}: {req.From?.ToString("yyyy-MM-dd") ?? "…"} – {req.To?.ToString("yyyy-MM-dd") ?? "…"}");
        sb.Append("</div>");

        foreach (var e in entries)
        {
            sb.Append("<div class=\"entry\"><div class=\"head\">");
            sb.Append($"<span class=\"cat\">{E(CategoryName(lang, e.Category))}</span>");
            sb.Append($"<span>{E(L.Occurred)}: {e.OccurredAt:yyyy-MM-dd HH:mm}</span>");
            sb.Append($"<span>{E(L.Logged)}: {e.CreatedAt:yyyy-MM-dd HH:mm}</span>");
            sb.Append($"<span>{E(L.Reporter)}: {E(e.Reporter!.DisplayName)}</span>");
            if (!string.IsNullOrWhiteSpace(e.SubjectApartment)) sb.Append($"<span>{E(L.Where)}: {E(e.SubjectApartment)}</span>");
            if (e.EditedAt is not null) sb.Append($"<span class=\"flag\">{E(L.Edited)}</span>");
            if (e.ArchivedAt is not null) sb.Append($"<span class=\"flag\">{E(L.Hidden)}</span>");
            sb.Append("</div>");
            sb.Append($"<div class=\"label\">{E(L.Original)} ({E(LangName(lang, e.OriginalLanguage))})</div>");
            sb.Append($"<div class=\"orig\">{E(e.OriginalText)}</div>");
            var trans = e.Translations.FirstOrDefault(t => t.TargetLanguage == b.SharedLanguage && t.Status == TranslationStatus.Completed);
            if (trans is not null)
            {
                sb.Append($"<div class=\"label\">{E(L.Translation)} ({E(LangName(lang, b.SharedLanguage))})</div>");
                sb.Append($"<div class=\"trans\">{E(trans.TranslatedText)}</div>");
                sb.Append($"<div class=\"notice\">{E(string.Format(L.AiNotice, LangName(lang, e.OriginalLanguage)))}</div>");
            }
            sb.Append("</div>");
        }
        sb.Append($"<div class=\"footer\">{E(L.Footer)}</div></body></html>");
        return sb.ToString();
    }

    private static string BuildTable(Building b, string generatedBy, List<IncidentEntry> entries, ExportRequest req, string lang)
    {
        var L = Labels(lang);
        string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"></head><body>");
        sb.Append($"<h3>Kotirauha — {E(b.Name)} ({E(L.Generated)} {DateTimeOffset.UtcNow:yyyy-MM-dd})</h3>");
        sb.Append("<table border=\"1\" cellspacing=\"0\" cellpadding=\"4\"><tr>");
        foreach (var h in new[] { L.Occurred, L.Logged, L.Category, L.Where, L.Reporter, L.Original, L.Translation, L.Status })
            sb.Append($"<th>{E(h)}</th>");
        sb.Append("</tr>");
        foreach (var e in entries)
        {
            var trans = e.Translations.FirstOrDefault(t => t.TargetLanguage == b.SharedLanguage && t.Status == TranslationStatus.Completed);
            var status = e.ArchivedAt is not null ? L.Hidden : e.EditedAt is not null ? L.Edited : "";
            sb.Append("<tr>");
            sb.Append($"<td>{e.OccurredAt:yyyy-MM-dd HH:mm}</td>");
            sb.Append($"<td>{e.CreatedAt:yyyy-MM-dd HH:mm}</td>");
            sb.Append($"<td>{E(CategoryName(lang, e.Category))}</td>");
            sb.Append($"<td>{E(e.SubjectApartment)}</td>");
            sb.Append($"<td>{E(e.Reporter!.DisplayName)}</td>");
            sb.Append($"<td>{E(e.OriginalText)}</td>");
            sb.Append($"<td>{E(trans?.TranslatedText)}</td>");
            sb.Append($"<td>{E(status)}</td>");
            sb.Append("</tr>");
        }
        sb.Append($"</table><p style=\"font-size:11px;color:#777\">{E(L.Footer)}</p></body></html>");
        return sb.ToString();
    }

    private record LabelSet(string Title, string Generated, string Entries, string Range, string Occurred, string Logged,
        string Reporter, string Where, string Category, string Original, string Translation, string Status,
        string Edited, string Hidden, string AiNotice, string Footer);

    private static LabelSet Labels(string lang) => lang == "en"
        ? new LabelSet("Kotirauha report", "Generated", "Entries", "Range", "Occurred", "Logged", "Reporter",
            "Where", "Category", "Original", "Translation", "Status", "edited", "hidden",
            "AI-generated translation from {0}.",
            "This document is a factual record of reported observations. It does not assign guilt and is not a legal determination.")
        : new LabelSet("Kotirauha-raportti", "Luotu", "Ilmoituksia", "Aikaväli", "Tapahtui", "Kirjattu", "Ilmoittaja",
            "Missä", "Luokka", "Alkuperäinen", "Käännös", "Tila", "muokattu", "piilotettu",
            "Tekoälyn tuottama käännös kielestä {0}.",
            "Tämä asiakirja on tiedollinen kirjaus ilmoitetuista havainnoista. Se ei osoita syyllisyyttä eikä ole oikeudellinen päätös.");

    private static string LangName(string uiLang, string code) =>
        code == "en" ? (uiLang == "en" ? "English" : "englanti") : (uiLang == "en" ? "Finnish" : "suomi");

    private static string CategoryName(string lang, IncidentCategory c) => lang == "en"
        ? c switch
        {
            IncidentCategory.Noise => "Noise", IncidentCategory.Smell => "Smell",
            IncidentCategory.SmokingOrIncense => "Smoking / Incense", IncidentCategory.Parking => "Parking",
            IncidentCategory.SafetyConcern => "Safety concern", IncidentCategory.CommonAreaMisuse => "Common area misuse",
            _ => "Other",
        }
        : c switch
        {
            IncidentCategory.Noise => "Melu", IncidentCategory.Smell => "Haju",
            IncidentCategory.SmokingOrIncense => "Tupakointi / suitsuke", IncidentCategory.Parking => "Pysäköinti",
            IncidentCategory.SafetyConcern => "Turvallisuus", IncidentCategory.CommonAreaMisuse => "Yhteisten tilojen väärinkäyttö",
            _ => "Muu",
        };
}
