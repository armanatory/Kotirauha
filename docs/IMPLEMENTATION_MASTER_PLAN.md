# Kotirauha — Implementation Master Plan

> This document operationalises the Product Foundation
> ([`00_Foundation.md`](00_Foundation.md)) for implementation. It captures the
> delivery plan, architecture choices, phased roadmap, and the ordered list of
> specs that will be fed to the code-writing agent.
>
> If this document conflicts with the Product Foundation on product philosophy
> or long-term intent, the Foundation takes precedence.

---

## 1. Project overview

**Kotirauha** ("domestic peace" in Finnish) is a web-based residential incident
documentation platform for multilingual apartment communities. Residents log
recurring disturbances in their own language; the system auto-translates each
entry into the building's shared language while preserving the original, and
accumulates a neutral, exportable timeline that housing boards and landlords can
use for official processes.

The core frustrations it solves:

- incidents are discussed verbally and the record is lost or fragmented
- residents write in different languages, so the record is inconsistent
- housing boards receive incomplete, emotional, second-hand accounts
- there is no neutral, timestamped, tamper-evident history of a recurring problem

The product is deliberately **not** a social network, a complaint board, or a
tool for accusing neighbours. It is a documentation tool whose value is
neutrality, transparency, and integrity.

---

## 2. Scope discipline and versioning rules

### 2.1 Version 1 — MVP
The core documentation loop for a single building:

- email/password authentication with JWT (no anonymous use)
- buildings + membership (a resident belongs to one building; one shared language)
- incident entries: date/time, category, description, optional image, subject
  apartment, real reporter identity
- AI translation of each entry into the building's shared language, original
  preserved verbatim and labelled
- chronological timeline with category/date filters and keyword search
- edit-with-history and archive-on-delete (integrity)
- export to a neutral PDF/printable report

### 2.2 Version 2
Reporting depth and board tooling:

- board/manager role with read-only oversight and richer export grouping
- pattern view (recurring disturbances grouped by category / apartment / time)
- multi-image attachments and basic evidence handling

### 2.3 Version 3
Scale and self-service:

- multi-building support with per-building join codes / invitations
- per-user UI language and on-demand re-translation into any member's language
- notification digests (e.g. weekly summary to board)

### 2.4 Version 4
Operator surface:

- admin role for platform operators (building provisioning, user management)
- translation provider monitoring (status, token usage, cost, error rate)
- moderation tooling for flagged entries

### 2.5 Out of scope until later
- native mobile app, offline/PWA sync
- direct messaging between residents
- integrations with housing-company ERP systems
- automated legal/warning generation

### 2.6 Planning rule
Only **Version 1** is active implementation scope. V2–V4 are directional plans,
not build-now commitments. Every new feature must justify itself against the
design principles in §17 of the Foundation.

---

## 3. Tech stack decisions

| Layer | Choice | Reason |
|---|---|---|
| Frontend | React 18 + Vite + TypeScript (strict) + Tailwind | Responsive, phone-first for residents; one bundle is enough for V1 |
| Backend | .NET 10 minimal API (C#) | Performance, mature EF Core / Postgres story |
| Database | PostgreSQL 16 | Relational, reliable, good full-text search for the timeline |
| ORM | EF Core 10 + Npgsql | Code-first migrations, version-controlled schema |
| Auth (V1) | Email/password + JWT | Simplest viable auth; no anonymous access |
| Translation | Anthropic Claude (default) behind a provider interface | Quality multilingual translation; prompt caching; swappable (ADR 002) |
| Image storage | Local volume in V1, object storage later | Simple now, swappable behind an `IAttachmentStore` |
| Hosting | Docker Compose + Caddy on one VPS | Cheap, container-native, TLS handled by Caddy (ADR 004) |
| CI/CD | GitHub Actions | Standard, free tier sufficient |

See ADRs 001–004 for the detailed rationale.

---

## 4. High-level architecture

- One shared backend exposing a versioned JSON API under `/api/v1/*`.
- One React frontend serving both residents and board members (role gates the
  oversight/export-all views). A dedicated operator app is deferred to V4.
- Row-level access control: every query is scoped to the caller's building
  (`WHERE building_id = caller_building_id`); reporters can only edit their own
  entries; board/admin roles widen read scope but never bypass neutrality rules.
- Translation runs through `ITranslationProvider`; one active provider per
  environment, selected by env var.
- The original entry text is immutable after translation; edits create new
  versions and re-translate, but never overwrite the stored original.

---

## 5. Core entities

- `User` — email, password hash, display name, role, building membership
- `Building` — name, address, shared language (e.g. `fi`), join code
- `BuildingMembership` — `(user_id, building_id)`, role, apartment number
- `IncidentEntry` — building, reporter, occurred-at, category, original text,
  original language, subject apartment, created/edited timestamps, archived flag
- `IncidentTranslation` — `(entry_id, target_language)`, translated text,
  provider, model, machine-generated flag, created-at
- `IncidentAttachment` — entry, storage key, content type
- `IncidentRevision` — entry, editor, before/after snapshot, timestamp
- `ExportReport` (V2+) — generated report metadata

See ADR 003 for data-model and integrity decisions.

---

## 6. Ordered spec backlog (V1)

| # | Spec | Theme |
|---|---|---|
| 01 | [Project setup](specs/01-project-setup.md) | Repo scaffold, Docker, CI, app shell |
| 02 | [Buildings & membership](specs/02-buildings-and-membership.md) | Building model, join code, membership, shared language |
| 03 | [Authentication](specs/03-auth.md) | Email/password, JWT, roles, no anonymous access |
| 04 | [Incident entries](specs/04-incident-entries.md) | Create/edit/archive entries, categories, attachments, revisions |
| 05 | [AI translation](specs/05-ai-translation.md) | Provider abstraction, translate-on-create, original preserved |
| 06 | [Timeline & search](specs/06-timeline-and-search.md) | Chronological view, filters, keyword search |
| 07 | [Export & reporting](specs/07-export-and-reporting.md) | Neutral PDF/printable report export |

Build in order. Each spec is self-contained and ends with acceptance criteria.

---

## 7. Definition of done for V1

A single building can honestly say:

- residents register, log in, and join their building (no anonymous entries)
- a resident creates an incident in their own language in under a minute
- the entry is stored verbatim and shown with a labelled Finnish translation
- the shared timeline is filterable by category and date and is keyword-searchable
- edits are visible as history and deletes are archived, not destroyed
- the board can export a neutral, timestamped report for official use

If those hold, the foundation is strong enough to carry V2–V4.
