# Kotirauha — Agent Notes

Kotirauha is a lightweight, multilingual residential **incident documentation**
platform for apartment buildings and shared housing communities. It lets
residents log recurring disturbances (noise, smells, smoking, parking, etc.)
in their own language, auto-translates each entry into the building's shared
language while preserving the original, and produces neutral, exportable
documentation for housing boards and landlords.

The product foundation lives in [`docs/00_Foundation.md`](docs/00_Foundation.md)
— it is the source of truth for product intent. The delivery plan lives in
[`docs/IMPLEMENTATION_MASTER_PLAN.md`](docs/IMPLEMENTATION_MASTER_PLAN.md).

## Workflow rule — commit and push after every related change

**After each related set of changes, commit and push to `main` — unless I
explicitly tell you not to.** This is the default mode of work for this repo:

- Group the work into one logical change, commit it with a clear message, then
  `git push origin main`. Do not let work pile up uncommitted.
- "Related changes" means one coherent unit (a spec, a feature slice, a fix) —
  not every single file edit. Use judgement, but err toward committing often.
- If I say "don't commit", "wait", "let me review first", or anything similar,
  hold off until I say otherwise.
- Never use `--no-verify`, never force-push `main`, and never amend or rewrite
  commits that are already pushed.
- When writing or changing a feature, update the matching spec under
  `docs/specs/` in the **same** commit.

## Stack

- Backend: .NET 10 minimal API, EF Core 10, PostgreSQL 16, JWT auth
- Frontend: React 18 + Vite + TypeScript (strict) + Tailwind
- AI translation: Anthropic Claude (default) behind a provider interface; one
  active provider per environment (see ADR 002)
- Hosting: Docker Compose + Caddy on a single VPS (see ADR 004)

## Folder layout

```
backend/Kotirauha.Core            # Domain models, interfaces, pure services
backend/Kotirauha.Infrastructure  # EF Core, translation provider, storage
backend/Kotirauha.Api             # Minimal API endpoints, Program.cs
backend/Kotirauha.Tests           # xUnit
frontend/                         # Resident + board React app
docker/                           # Dockerfiles
docs/                             # Foundation, master plan, specs, ADRs
```

## Spec status

- Nothing is built yet. `docs/specs/` holds the V1 plan (01–07); the
  master plan is the ordered backlog.
- When a spec lands in code, mark its `Status` section and note it here.

## Key product rules (do not violate)

- **Neutrality**: the platform records observations, never verdicts. No
  "guilty/innocent", no scoring of residents, no public shaming.
- **Original text is sacred**: AI translation never overwrites or replaces the
  reporter's original wording. Both versions are always viewable, and the
  translation is always labelled "AI-generated".
- **No anonymous reporting**: every entry carries a real reporter identity and
  an automatic timestamp.
- **Edits are visible, deletes are archived**: nothing is silently mutated or
  destroyed; integrity is the product's main value.
- **Tone is calm and administrative**, not emotional. This is documentation,
  not a complaint board or social network.
- TypeScript strict mode is required. Prefer clear, testable code over
  over-engineering.

## Translation rules

- Each entry stores `originalText` + `originalLanguage`. Translations are
  separate rows keyed by `(entry_id, target_language)`.
- Translations are clearly marked machine-generated and show the source
  language. Never present a translation as if a human wrote it.
- The building's shared language (e.g. Finnish) is the default translation
  target; the original is always preserved verbatim.

## Run locally

```powershell
# From repo root (Windows PowerShell), once the scaffold exists:
docker compose up --build
```
