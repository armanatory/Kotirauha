using Kotirauha.Api.Common;
using Kotirauha.Core.Abstractions;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Kotirauha.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kotirauha.Api.Endpoints;

public record TranslationDto(string TargetLanguage, string TranslatedText, string Provider, string Status, bool IsMachineGenerated);
public record EntryListItemDto(
    Guid Id, string Category, DateTimeOffset OccurredAt, string ReporterName,
    string? SubjectApartment, string Snippet, bool HasAttachment, bool Edited, bool Archived);
public record EntryDetailDto(
    Guid Id, string Category, DateTimeOffset OccurredAt, string ReporterName, Guid ReporterUserId,
    string? SubjectApartment, string OriginalText, string OriginalLanguage,
    string SharedLanguage, IReadOnlyList<TranslationDto> Translations,
    IReadOnlyList<Guid> AttachmentIds, DateTimeOffset CreatedAt, DateTimeOffset? EditedAt,
    bool Archived, IReadOnlyList<RevisionDto> Revisions);
public record RevisionDto(Guid Id, string PreviousText, DateTimeOffset EditedAt);

public static class EntryEndpoints
{
    public static RouteGroupBuilder MapEntryEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/entries").RequireAuthorization();

        // --- Create (multipart: fields + optional image) ---
        group.MapPost("/", async (HttpContext ctx, KotirauhaDbContext db,
            IAttachmentStore store, EntryTranslationService translator) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null) return Results.Problem("Join a building before creating entries.", statusCode: 403);

            if (!ctx.Request.HasFormContentType) return Results.Problem("Multipart form expected.", statusCode: 400);
            var form = await ctx.Request.ReadFormAsync();

            var text = form["originalText"].ToString();
            if (string.IsNullOrWhiteSpace(text)) return Results.Problem("Description is required.", statusCode: 400);
            if (!Enum.TryParse<IncidentCategory>(form["category"], true, out var category))
                return Results.Problem("Invalid category.", statusCode: 400);
            if (!DateTimeOffset.TryParse(form["occurredAt"], out var occurredAt)) occurredAt = DateTimeOffset.UtcNow;
            var originalLanguage = form["originalLanguage"].ToString();
            if (string.IsNullOrWhiteSpace(originalLanguage)) originalLanguage = "en";

            var entry = new IncidentEntry
            {
                BuildingId = m.BuildingId,
                ReporterUserId = userId.Value,
                OccurredAt = occurredAt,
                Category = category,
                OriginalText = text.Trim(),
                OriginalLanguage = originalLanguage,
                SubjectApartment = string.IsNullOrWhiteSpace(form["subjectApartment"]) ? null : form["subjectApartment"].ToString().Trim(),
            };
            db.Entries.Add(entry);

            // Accept one or more images (form field "image" may repeat).
            foreach (var file in form.Files.Where(f => f.Length > 0))
            {
                if (!file.ContentType.StartsWith("image/"))
                    return Results.Problem("Only image attachments are allowed.", statusCode: 400);
                if (file.Length > 10 * 1024 * 1024)
                    return Results.Problem("Image too large (max 10 MB).", statusCode: 400);
                await using var s = file.OpenReadStream();
                var key = await store.SaveAsync(s, file.ContentType);
                db.Attachments.Add(new IncidentAttachment { EntryId = entry.Id, StorageKey = key, ContentType = file.ContentType });
            }

            await db.SaveChangesAsync();
            await translator.TranslateEntryAsync(entry.Id, m.Building!.SharedLanguage);

            return Results.Created($"/api/v1/entries/{entry.Id}", new { id = entry.Id });
        }).DisableAntiforgery();

        // --- AI "start writing" suggestions for the capture box ---
        // Returns short, tappable starters in the building's shared language,
        // loosely informed by recent notes. Cached per building to limit cost.
        group.MapGet("/suggestions", async (HttpContext ctx, KotirauhaDbContext db,
            ISuggestionProvider suggester, IMemoryCache cache, bool? refresh) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null) return Results.Ok(new { suggestions = Array.Empty<string>() });

            var lang = m.Building!.SharedLanguage == "en" ? "en" : "fi";
            var cacheKey = $"suggestions:{m.BuildingId}:{lang}";
            if (refresh != true && cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached) && cached is not null)
                return Results.Ok(new { suggestions = cached });

            var recent = await db.Entries
                .Where(e => e.BuildingId == m.BuildingId && e.ArchivedAt == null)
                .Include(e => e.Translations)
                .OrderByDescending(e => e.OccurredAt)
                .Take(20)
                .ToListAsync();

            var examples = recent
                .Select(e => $"{TimeOfDay(e.OccurredAt, lang)}, {CategoryWord(e.Category, lang)}: {Snippet(SharedText(e, m.Building!.SharedLanguage))}")
                .ToList();

            IReadOnlyList<string> suggestions;
            try { suggestions = await suggester.SuggestEntryTextsAsync(lang, examples); }
            catch { suggestions = Array.Empty<string>(); }

            if (suggestions.Count > 0)
                cache.Set(cacheKey, suggestions, TimeSpan.FromMinutes(30));
            return Results.Ok(new { suggestions });
        }).RequireRateLimiting("ai");

        // --- AI guess of category + location from the typed text ---
        group.MapPost("/classify", async (ClassifyRequest req, HttpContext ctx, KotirauhaDbContext db, ISuggestionProvider suggester) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var text = req.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length < 8)
                return Results.Ok(new EntryClassification(null, null));
            try { return Results.Ok(await suggester.ClassifyAsync(text)); }
            catch { return Results.Ok(new EntryClassification(null, null)); }
        }).RequireRateLimiting("ai");

        // --- Timeline list with filters + keyword search ---
        group.MapGet("/", async (HttpContext ctx, KotirauhaDbContext db,
            string? category, DateTimeOffset? from, DateTimeOffset? to, string? q, bool? includeArchived) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null) return Results.Ok(Array.Empty<EntryListItemDto>());

            var canSeeArchived = m.Role is MembershipRole.Board or MembershipRole.Admin;
            var query = db.Entries
                .Where(e => e.BuildingId == m.BuildingId)
                .Include(e => e.Reporter)
                .Include(e => e.Translations)
                .Include(e => e.Attachments)
                .AsQueryable();

            if (!(includeArchived == true && canSeeArchived))
                query = query.Where(e => e.ArchivedAt == null);

            if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<IncidentCategory>(category, true, out var cat))
                query = query.Where(e => e.Category == cat);
            if (from is not null) query = query.Where(e => e.OccurredAt >= from);
            if (to is not null) query = query.Where(e => e.OccurredAt <= to);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = $"%{q.Trim()}%";
                query = query.Where(e =>
                    EF.Functions.ILike(e.OriginalText, pattern) ||
                    e.Translations.Any(t => EF.Functions.ILike(t.TranslatedText, pattern)));
            }

            var entries = await query
                .OrderByDescending(e => e.OccurredAt)
                .Take(200)
                .ToListAsync();

            var items = entries.Select(e => new EntryListItemDto(
                e.Id,
                e.Category.ToString(),
                e.OccurredAt,
                e.Reporter!.DisplayName,
                e.SubjectApartment,
                Snippet(SharedText(e, m.Building!.SharedLanguage)),
                e.Attachments.Any(),
                e.EditedAt is not null,
                e.ArchivedAt is not null));

            return Results.Ok(items);
        });

        // --- Detail ---
        group.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null) return Results.NotFound();

            var e = await db.Entries
                .Include(x => x.Reporter)
                .Include(x => x.Translations)
                .Include(x => x.Attachments)
                .Include(x => x.Revisions)
                .FirstOrDefaultAsync(x => x.Id == id && x.BuildingId == m.BuildingId);
            if (e is null) return Results.NotFound();

            return Results.Ok(ToDetail(e, m.Building!.SharedLanguage));
        });

        // --- Edit (reporter only) -> revision + re-translate ---
        group.MapPatch("/{id:guid}", async (Guid id, EditEntryRequest req, HttpContext ctx,
            KotirauhaDbContext db, EntryTranslationService translator) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null) return Results.NotFound();

            var e = await db.Entries.FirstOrDefaultAsync(x => x.Id == id && x.BuildingId == m.BuildingId);
            if (e is null) return Results.NotFound();
            if (e.ReporterUserId != userId) return Results.Problem("Only the reporter can edit this entry.", statusCode: 403);

            var textChanged = !string.IsNullOrWhiteSpace(req.OriginalText) && req.OriginalText.Trim() != e.OriginalText;
            if (textChanged)
            {
                db.Revisions.Add(new IncidentRevision
                {
                    EntryId = e.Id,
                    EditedByUserId = userId.Value,
                    PreviousText = e.OriginalText,
                });
                e.OriginalText = req.OriginalText!.Trim();
            }
            if (req.OccurredAt is not null) e.OccurredAt = req.OccurredAt.Value;
            if (req.Category is not null && Enum.TryParse<IncidentCategory>(req.Category, true, out var cat)) e.Category = cat;
            if (req.SubjectApartment is not null) e.SubjectApartment = string.IsNullOrWhiteSpace(req.SubjectApartment) ? null : req.SubjectApartment.Trim();
            e.EditedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();
            if (textChanged) await translator.TranslateEntryAsync(e.Id, m.Building!.SharedLanguage);

            return Results.Ok(new { id = e.Id });
        });

        // Hide (archive) — board only, reversible.
        group.MapPost("/{id:guid}/archive", async (Guid id, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null || m.Role is MembershipRole.Resident)
                return Results.Problem("Board access required.", statusCode: 403);

            var e = await db.Entries.FirstOrDefaultAsync(x => x.Id == id && x.BuildingId == m.BuildingId);
            if (e is null) return Results.NotFound();

            e.ArchivedAt = DateTimeOffset.UtcNow;
            e.ArchivedByUserId = userId;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = e.Id });
        });

        // Remove (permanent delete) — the reporter of the entry, or a platform admin.
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, KotirauhaDbContext db, IAttachmentStore store) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var e = await db.Entries.Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return Results.NotFound();

            var isAdmin = await db.Users.AnyAsync(u => u.Id == userId && u.IsPlatformAdmin);
            if (e.ReporterUserId != userId && !isAdmin)
                return Results.Problem("Only the reporter or an admin can remove this entry.", statusCode: 403);

            foreach (var a in e.Attachments)
            {
                try { await store.DeleteAsync(a.StorageKey); } catch { /* best effort */ }
            }
            db.Entries.Remove(e); // cascades translations, attachments, revisions
            await db.SaveChangesAsync();
            return Results.Ok(new { deleted = true });
        });

        group.MapPost("/{id:guid}/restore", async (Guid id, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null || m.Role is MembershipRole.Resident)
                return Results.Problem("Board access required.", statusCode: 403);

            var e = await db.Entries.FirstOrDefaultAsync(x => x.Id == id && x.BuildingId == m.BuildingId);
            if (e is null) return Results.NotFound();
            e.ArchivedAt = null;
            e.ArchivedByUserId = null;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = e.Id });
        });

        // --- On-demand translation into any language (any member) ---
        group.MapPost("/{id:guid}/translate", async (Guid id, TranslateRequest req, HttpContext ctx,
            KotirauhaDbContext db, EntryTranslationService translator) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null) return Results.NotFound();

            var lang = req.Language?.Trim();
            if (string.IsNullOrWhiteSpace(lang)) return Results.Problem("Language is required.", statusCode: 400);

            var entry = await db.Entries
                .Include(e => e.Translations)
                .FirstOrDefaultAsync(e => e.Id == id && e.BuildingId == m.BuildingId);
            if (entry is null) return Results.NotFound();

            var existing = entry.Translations.FirstOrDefault(x => x.TargetLanguage == lang);
            if (existing is null || existing.Status != TranslationStatus.Completed)
                await translator.TranslateEntryAsync(entry.Id, lang);

            var t = await db.Translations.FirstOrDefaultAsync(x => x.EntryId == entry.Id && x.TargetLanguage == lang);
            if (t is null)
                return Results.Ok(new TranslationDto(lang, entry.OriginalText, "none", "completed", false));
            return Results.Ok(new TranslationDto(
                t.TargetLanguage, t.TranslatedText, t.Provider, t.Status.ToString().ToLowerInvariant(), t.IsMachineGenerated));
        }).RequireRateLimiting("ai");

        // --- Attachment fetch by attachment id (building-scoped) ---
        group.MapGet("/{id:guid}/attachments/{attachmentId:guid}", async (
            Guid id, Guid attachmentId, HttpContext ctx, KotirauhaDbContext db, IAttachmentStore store) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null) return Results.NotFound();

            var att = await db.Attachments
                .Include(a => a.Entry)
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.EntryId == id && a.Entry!.BuildingId == m.BuildingId);
            if (att is null) return Results.NotFound();

            var file = await store.GetAsync(att.StorageKey);
            if (file is null) return Results.NotFound();
            return Results.Stream(file.Value.Content, file.Value.ContentType);
        });

        return api;
    }

    internal static string SharedText(IncidentEntry e, string sharedLanguage)
    {
        if (string.Equals(e.OriginalLanguage, sharedLanguage, StringComparison.OrdinalIgnoreCase))
            return e.OriginalText;
        var t = e.Translations.FirstOrDefault(x => x.TargetLanguage == sharedLanguage && x.Status == TranslationStatus.Completed);
        return t?.TranslatedText ?? e.OriginalText;
    }

    private static string Snippet(string text) =>
        text.Length <= 160 ? text : text[..160] + "…";

    // Coarse time-of-day in Helsinki time, so suggestions can echo when things
    // tend to happen without leaking exact timestamps to the model.
    private static readonly TimeZoneInfo Helsinki = ResolveHelsinki();
    private static TimeZoneInfo ResolveHelsinki()
    {
        foreach (var id in new[] { "Europe/Helsinki", "FLE Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { /* try next */ }
        }
        return TimeZoneInfo.Utc;
    }

    private static string TimeOfDay(DateTimeOffset at, string lang)
    {
        var h = TimeZoneInfo.ConvertTime(at, Helsinki).Hour;
        var slot = h < 6 ? 0 : h < 12 ? 1 : h < 18 ? 2 : 3;
        return lang == "en"
            ? slot switch { 0 => "night", 1 => "morning", 2 => "daytime", _ => "evening" }
            : slot switch { 0 => "yö", 1 => "aamu", 2 => "päivä", _ => "ilta" };
    }

    private static string CategoryWord(IncidentCategory c, string lang) => lang == "en"
        ? c switch
        {
            IncidentCategory.Noise => "noise", IncidentCategory.Smell => "smell",
            IncidentCategory.SmokingOrIncense => "smoking", IncidentCategory.Parking => "parking",
            IncidentCategory.SafetyConcern => "safety", IncidentCategory.CommonAreaMisuse => "shared space",
            _ => "other",
        }
        : c switch
        {
            IncidentCategory.Noise => "melu", IncidentCategory.Smell => "haju",
            IncidentCategory.SmokingOrIncense => "tupakointi", IncidentCategory.Parking => "pysäköinti",
            IncidentCategory.SafetyConcern => "turvallisuus", IncidentCategory.CommonAreaMisuse => "yhteiset tilat",
            _ => "muu",
        };

    internal static EntryDetailDto ToDetail(IncidentEntry e, string sharedLanguage) => new(
        e.Id,
        e.Category.ToString(),
        e.OccurredAt,
        e.Reporter!.DisplayName,
        e.ReporterUserId,
        e.SubjectApartment,
        e.OriginalText,
        e.OriginalLanguage,
        sharedLanguage,
        e.Translations.Select(t => new TranslationDto(
            t.TargetLanguage, t.TranslatedText, t.Provider, t.Status.ToString().ToLowerInvariant(), t.IsMachineGenerated)).ToList(),
        e.Attachments.Select(a => a.Id).ToList(),
        e.CreatedAt,
        e.EditedAt,
        e.ArchivedAt is not null,
        e.Revisions.OrderByDescending(r => r.EditedAt).Select(r => new RevisionDto(r.Id, r.PreviousText, r.EditedAt)).ToList());
}

public record EditEntryRequest(string? OriginalText, DateTimeOffset? OccurredAt, string? Category, string? SubjectApartment);
public record TranslateRequest(string Language);
public record ClassifyRequest(string Text);
