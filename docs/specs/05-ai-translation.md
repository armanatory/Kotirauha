# Spec 05 — AI Translation

## Goal

Translate every incident entry into the building's shared language while
preserving the reporter's original verbatim. Translations are stored, clearly
labelled machine-generated, and never overwrite the original. See ADR 002.

---

## Provider abstraction

```csharp
public interface ITranslationProvider
{
    Task<TranslationResult> TranslateAsync(
        string text, string sourceLanguage, string targetLanguage, CancellationToken ct);
}

public record TranslationResult(string TranslatedText, string Provider, string Model);
```

- One implementation active per environment, selected by `TRANSLATION_PROVIDER`.
- V1 default: `AnthropicTranslationProvider` (Claude). API key from
  `ANTHROPIC_API_KEY`.
- The system prompt instructs the model to translate **faithfully and
  neutrally**: preserve meaning and tone, do not soften, embellish, summarise,
  sanitise, or add interpretation; translate emotional or strong wording as
  written. The model is a translator, not a moderator.

---

## Data model

### `IncidentTranslation`
| Field | Type | Notes |
|---|---|---|
| `Id` | guid | |
| `EntryId` | guid | |
| `TargetLanguage` | string | BCP-47 (the building shared language in V1) |
| `TranslatedText` | string | |
| `Provider` | string | e.g. `anthropic` |
| `Model` | string | e.g. `claude-...` |
| `Status` | enum | `completed`, `pending`, `failed` |
| `IsMachineGenerated` | bool | always `true` |
| `CreatedAt` | timestamptz | |

Unique on `(EntryId, TargetLanguage)`.

---

## Flow

1. Entry is created (Spec 04) and saved with its original text first.
2. If `OriginalLanguage == building.SharedLanguage`, no translation is needed —
   the original is also the shared-language view.
3. Otherwise, create an `IncidentTranslation` row (`status = pending`) and call
   the provider. On success → `completed`. On failure → `failed`, retried by a
   background retry; the entry remains fully usable meanwhile.
4. On entry edit (Spec 04), the shared-language translation is regenerated (a new
   `completed` row replaces the prior one for that target language; the original
   is untouched).

**A missing or failed translation never blocks documentation.** The original is
always shown; the translation area shows a "translation pending/failed, retrying"
state.

---

## Frontend

- `EntryDetailPage` shows two clearly separated blocks:
  - **Original** — the reporter's text with a source-language badge.
  - **Translation** — the shared-language text with a persistent notice:
    *"AI-generated translation from {sourceLanguageName}."*
- The translation is never styled to look authoritative over the original; the
  original is the primary, the translation is the aid.

---

## Acceptance criteria

- [ ] Creating a non-shared-language entry produces a stored translation row
- [ ] Same-language entries skip translation cleanly
- [ ] The original text is byte-for-byte preserved and never overwritten
- [ ] The translation always renders with an "AI-generated from {lang}" label
- [ ] Provider is swappable via `TRANSLATION_PROVIDER` with no call-site changes
- [ ] A provider failure leaves the entry saved with a retrying translation state
- [ ] Editing an entry regenerates the translation without touching the original

---

## Status
- [x] Shipped (V1). ITranslationProvider with Anthropic + stub impls (stub used
      when no API key), translate-on-create and on-edit into shared language,
      labelled UI, original never overwritten. Background retry of failed
      translations still TODO. Verified via Playwright (stub provider).
