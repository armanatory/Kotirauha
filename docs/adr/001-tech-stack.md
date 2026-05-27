# ADR 001 — Technology Stack Selection

**Date:** 2026-05-27
**Status:** Accepted

---

## Context

Kotirauha is a multilingual residential incident-documentation platform. It
starts small (one building, a handful of residents) but must keep the door open
for multi-building support, a board/operator surface, and an AI translation
layer. The stack must support:

- a responsive, phone-first web experience for residents writing quick entries
- a clean JSON API so a board surface (and later an operator app) can be added
  without rewriting the core
- reliable relational storage with good full-text search for the timeline
- an AI translation layer behind a swappable provider interface
- container-native deployment on a cheap single VPS
- solo development with AI assistance

---

## Decision

| Layer | Choice | Reason |
|---|---|---|
| Frontend | React 18 + Vite + TypeScript (strict) + Tailwind | Best responsive UX, large ecosystem, decoupled from backend |
| Backend | .NET 10 minimal API (C#) | Performance, maturity, strong EF Core / Postgres story |
| Database | PostgreSQL 16 | Relational, reliable, native full-text search for the timeline |
| ORM | EF Core 10 + Npgsql | Code-first migrations, good async story |
| Auth (V1) | Email/password + JWT | Simplest viable auth; no anonymous access by design |
| Translation | Anthropic Claude (default), provider interface | Quality multilingual translation; see ADR 002 |
| Image storage | Local Docker volume (V1) behind `IAttachmentStore` | Simple now; swap to object storage later with no API change |
| Hosting | Docker Compose + Caddy on one VPS | Cheap, container-native; Caddy handles TLS (ADR 004) |
| CI/CD | GitHub Actions | Standard, free tier sufficient |

JWT expiry is 7 days in V1 (acceptable for low-friction resident use; to be
shortened with refresh tokens later).

---

## Consequences

- React + Vite + TS gives a clean phone-first resident UX and room for the
  board oversight views without a second app in V1.
- A single shared backend with a versioned `/api/v1/*` contract lets a dedicated
  operator app be added in V4 as a pure additive change.
- PostgreSQL full-text search covers the timeline keyword search in V1 without a
  separate search service.
- Storing attachments behind `IAttachmentStore` means the V1 local-volume choice
  is not a lock-in; moving to S3/Backblaze later is an infrastructure change only.
- Container-native from day one keeps local dev and production identical in shape.
