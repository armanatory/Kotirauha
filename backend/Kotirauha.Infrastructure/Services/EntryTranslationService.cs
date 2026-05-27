using Kotirauha.Core.Abstractions;
using Kotirauha.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kotirauha.Infrastructure.Services;

// Orchestrates translating an entry into its building's shared language.
// The original text is never modified; translations live in separate rows.
// A failure never blocks documentation — the entry stays usable and the
// translation row is marked Failed for later retry.
public class EntryTranslationService
{
    private readonly KotirauhaDbContext _db;
    private readonly ITranslationProvider _provider;
    private readonly ILogger<EntryTranslationService> _logger;

    public EntryTranslationService(
        KotirauhaDbContext db, ITranslationProvider provider, ILogger<EntryTranslationService> logger)
    {
        _db = db;
        _provider = provider;
        _logger = logger;
    }

    public async Task TranslateEntryAsync(Guid entryId, string targetLanguage, CancellationToken ct = default)
    {
        var entry = await _db.Entries
            .Include(e => e.Translations)
            .FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry is null) return;

        // Same-language entries need no translation: the original is the shared view.
        if (string.Equals(entry.OriginalLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
            return;

        var row = entry.Translations.FirstOrDefault(t => t.TargetLanguage == targetLanguage);
        if (row is null)
        {
            row = new IncidentTranslation
            {
                EntryId = entry.Id,
                TargetLanguage = targetLanguage,
                Status = TranslationStatus.Pending,
            };
            _db.Translations.Add(row);
        }

        try
        {
            var result = await _provider.TranslateAsync(entry.OriginalText, entry.OriginalLanguage, targetLanguage, ct);
            row.TranslatedText = result.TranslatedText;
            row.Provider = result.Provider;
            row.Model = result.Model;
            row.Status = TranslationStatus.Completed;
            row.IsMachineGenerated = true;
            row.CreatedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation failed for entry {EntryId} -> {Lang}", entryId, targetLanguage);
            row.Status = TranslationStatus.Failed;
        }

        await _db.SaveChangesAsync(ct);
    }
}
