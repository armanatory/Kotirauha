using Kotirauha.Api.Common;
using Kotirauha.Core.Abstractions;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Kotirauha.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

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
                await using var s = file.OpenReadStream();
                var key = await store.SaveAsync(s, file.ContentType);
                db.Attachments.Add(new IncidentAttachment { EntryId = entry.Id, StorageKey = key, ContentType = file.ContentType });
            }

            await db.SaveChangesAsync();
            await translator.TranslateEntryAsync(entry.Id, m.Building!.SharedLanguage);

            return Results.Created($"/api/v1/entries/{entry.Id}", new { id = entry.Id });
        }).DisableAntiforgery();

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

        group.MapPost("/{id:guid}/archive", async (Guid id, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null) return Results.NotFound();

            var e = await db.Entries.FirstOrDefaultAsync(x => x.Id == id && x.BuildingId == m.BuildingId);
            if (e is null) return Results.NotFound();

            var isBoard = m.Role is MembershipRole.Board or MembershipRole.Admin;
            if (e.ReporterUserId != userId && !isBoard)
                return Results.Problem("Only the reporter or a board member can archive this entry.", statusCode: 403);

            e.ArchivedAt = DateTimeOffset.UtcNow;
            e.ArchivedByUserId = userId;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = e.Id });
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
