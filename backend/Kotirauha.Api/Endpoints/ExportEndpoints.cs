using System.Net;
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
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
                return Results.File(BuildXlsx(m.Building!, me.DisplayName, entries, lang),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "kotirauha-raportti.xlsx");
            if (format == "word")
                return Results.File(BuildDocx(m.Building!, me.DisplayName, entries, req, lang),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "kotirauha-raportti.docx");
            // pdf: printable HTML (the client prints to PDF)
            return Results.Content(BuildReport(m.Building!, me.DisplayName, entries, req, lang), "text/html; charset=utf-8");
        });

        return api;
    }

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

    // A genuine .xlsx workbook (not HTML with an .xls name), so Office opens it
    // without the "format and extension don't match" warning.
    private static byte[] BuildXlsx(Building b, string generatedBy, List<IncidentEntry> entries, string lang)
    {
        var L = Labels(lang);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Kotirauha");

        ws.Cell(1, 1).Value = $"Kotirauha — {b.Name}";
        ws.Range(1, 1, 1, 8).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"{L.Generated} {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC · {generatedBy}";
        ws.Range(2, 1, 2, 8).Merge();
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#52606d");

        const int headerRow = 4;
        var headers = new[] { L.Occurred, L.Logged, L.Category, L.Where, L.Reporter, L.Original, L.Translation, L.Status };
        for (var i = 0; i < headers.Length; i++)
        {
            var c = ws.Cell(headerRow, i + 1);
            c.Value = headers[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#0f766e");
        }

        var row = headerRow + 1;
        foreach (var e in entries)
        {
            var trans = e.Translations.FirstOrDefault(t => t.TargetLanguage == b.SharedLanguage && t.Status == TranslationStatus.Completed);
            var status = e.ArchivedAt is not null ? L.Hidden : e.EditedAt is not null ? L.Edited : "";
            ws.Cell(row, 1).Value = e.OccurredAt.ToString("yyyy-MM-dd HH:mm");
            ws.Cell(row, 2).Value = e.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            ws.Cell(row, 3).Value = CategoryName(lang, e.Category);
            ws.Cell(row, 4).Value = e.SubjectApartment ?? "";
            ws.Cell(row, 5).Value = e.Reporter!.DisplayName;
            ws.Cell(row, 6).Value = e.OriginalText;
            ws.Cell(row, 7).Value = trans?.TranslatedText ?? "";
            ws.Cell(row, 8).Value = status;
            row++;
        }

        ws.Cell(row + 1, 1).Value = L.Footer;
        ws.Range(row + 1, 1, row + 1, 8).Merge();
        ws.Cell(row + 1, 1).Style.Font.FontSize = 9;
        ws.Cell(row + 1, 1).Style.Font.FontColor = XLColor.FromHtml("#7b8794");

        ws.Columns().AdjustToContents();
        ws.Column(6).Width = 60;
        ws.Column(7).Width = 60;
        ws.Range(headerRow, 6, Math.Max(headerRow, row - 1), 7).Style.Alignment.WrapText = true;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // A genuine .docx document (OpenXML), so Word opens it without a warning.
    private static byte[] BuildDocx(Building b, string generatedBy, List<IncidentEntry> entries, ExportRequest req, string lang)
    {
        var L = Labels(lang);
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());

            body.AppendChild(TextPara($"Kotirauha — {b.Name}", bold: true, size: 28, color: "0f766e"));
            if (!string.IsNullOrWhiteSpace(b.Address)) body.AppendChild(TextPara(b.Address!, color: "52606d", size: 20));
            body.AppendChild(TextPara($"{L.Generated} {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC · {generatedBy}", color: "52606d", size: 18));
            var range = req.From is not null || req.To is not null
                ? $" · {L.Range}: {req.From?.ToString("yyyy-MM-dd") ?? "…"} – {req.To?.ToString("yyyy-MM-dd") ?? "…"}"
                : "";
            body.AppendChild(TextPara($"{L.Entries}: {entries.Count}{range}", color: "52606d", size: 18));

            foreach (var e in entries)
            {
                var head = $"{CategoryName(lang, e.Category)} · {L.Occurred} {e.OccurredAt:yyyy-MM-dd HH:mm}";
                if (!string.IsNullOrWhiteSpace(e.SubjectApartment)) head += $" · {L.Where}: {e.SubjectApartment}";
                head += $" · {L.Reporter}: {e.Reporter!.DisplayName}";
                if (e.EditedAt is not null) head += $" · {L.Edited}";
                if (e.ArchivedAt is not null) head += $" · {L.Hidden}";
                body.AppendChild(TextPara(head, bold: true, size: 22, spaceBefore: 200));

                body.AppendChild(TextPara($"{L.Original} ({LangName(lang, e.OriginalLanguage)})", color: "7b8794", size: 16));
                body.AppendChild(TextPara(e.OriginalText, size: 20));

                var trans = e.Translations.FirstOrDefault(t => t.TargetLanguage == b.SharedLanguage && t.Status == TranslationStatus.Completed);
                if (trans is not null)
                {
                    body.AppendChild(TextPara($"{L.Translation} ({LangName(lang, b.SharedLanguage)})", color: "7b8794", size: 16));
                    body.AppendChild(TextPara(trans.TranslatedText, size: 20));
                    body.AppendChild(TextPara(string.Format(L.AiNotice, LangName(lang, e.OriginalLanguage)), italic: true, color: "7b8794", size: 16));
                }
            }

            body.AppendChild(TextPara(L.Footer, italic: true, color: "7b8794", size: 16, spaceBefore: 300));
            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static Paragraph TextPara(string text, bool bold = false, bool italic = false,
        int size = 20, string? color = null, int spaceBefore = 0)
    {
        var runProps = new RunProperties();
        if (bold) runProps.AppendChild(new Bold());
        if (italic) runProps.AppendChild(new Italic());
        runProps.AppendChild(new FontSize { Val = size.ToString() }); // half-points
        if (color is not null) runProps.AppendChild(new Color { Val = color });

        var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        run.PrependChild(runProps);

        var para = new Paragraph(run);
        if (spaceBefore > 0)
            para.ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines { Before = spaceBefore.ToString() });
        return para;
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
