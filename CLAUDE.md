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

- Backend: .NET 10 minimal API, EF Core 10, PostgreSQL 16
- Auth: **passwordless email magic-link** (JWT session); Google sign-in planned
  later. Never reintroduce passwords.
- Frontend: React 19 + Vite + TypeScript (strict) + Tailwind v4; installable PWA
- AI translation: provider interface; **OpenAI** when `OPENAI_API_KEY` is set,
  else Anthropic, else an offline stub. Prompt is strict: faithful, no fluff,
  spelling-only fixes, never an em dash (see ADR 002).
- Email: Mailjet (magic-link login emails) behind `IEmailSender`; no-op when unset
- Languages: **bilingual only — English and Finnish**
- Hosting: Docker Compose + Caddy on a single VPS (see ADR 004)

## Secrets / .env

The backend loads `.env` from the repo root on startup (DotNetEnv). Relevant
vars: `OPENAI_API_KEY` (+ optional `OPENAI_MODEL`), `MAILJET_API_KEY` /
`MAILJET_API_SECRET` / `MAILJET_FROM_EMAIL` / `MAILJET_FROM_NAME`,
`APP_BASE_URL` (for magic-link URLs), `JWT_SECRET`, optional `ADMIN_EMAIL`.
Never read, print, or commit `.env`.

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

- **V1 shipped (specs 01–07)**: scaffold, auth, buildings/membership, incident
  entries (create/edit-with-revision/archive), AI translation (provider interface
  + Anthropic/stub), timeline + search, printable export with neutrality footer.
- **V2 (spec 08)**: board insights/patterns + multi-image attachments.
- **V3 (spec 09)**: on-demand per-user translation (multi-building switching +
  digests deferred).
- **V4 (spec 10)**: platform-admin operator console (stats, buildings,
  translation-provider status).
- All verified end-to-end with Playwright against live Postgres. Each spec's
  `Status` section records what shipped vs deferred.
- When a spec lands in code, mark its `Status` section and note it here.

## Running it locally (dev)

- Postgres in Docker: `docker run -d --name kotirauha-pg -e POSTGRES_DB=kotirauha
  -e POSTGRES_USER=kotirauha -e POSTGRES_PASSWORD=kotirauha -p 5432:5432 postgres:16`
- Backend: from `backend/Kotirauha.Api`, `dotnet run` (listens on :5000, migrates
  on start). Env: `DATABASE_URL`, optional `ADMIN_EMAIL` to seed a platform admin,
  optional `TRANSLATION_PROVIDER=anthropic` + `ANTHROPIC_API_KEY` for real translation.
- Frontend: from `frontend`, `npm run dev` (Vite :5173, proxies `/api` → :5000).

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
