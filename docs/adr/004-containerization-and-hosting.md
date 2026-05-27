# ADR 004 — Containerization and Hosting

**Date:** 2026-05-27
**Status:** Accepted

---

## Context

Kotirauha is a small-scale product (one building initially, modest traffic). It
needs reliable, cheap hosting with TLS, persistent storage for the database and
uploaded images, and a deployment shape identical to local development so the
solo developer + AI assistant can move fast without environment drift.

---

## Decision

- **Containerise everything from day one.** The backend, frontend, and database
  all run as Docker containers. Local dev and production share the same
  `docker-compose` shape.
- **Single VPS** (e.g. Hetzner) running Docker Compose. No Kubernetes, no
  managed cloud platform in V1 — overkill for the scale.
- **Caddy** as the reverse proxy and TLS terminator. It serves the built
  frontend static files, proxies `/api/*` to the backend container, and obtains
  Let's Encrypt certificates automatically.
- **PostgreSQL 16** as a container with a named volume for persistence.
- **Uploaded images** live on a mounted volume in V1, written through
  `IAttachmentStore` so a later move to object storage is a code-free infra swap.
- Secrets (JWT secret, DB password, translation API key) are provided via
  environment variables / a `.env` file that is never committed.

### Compose services (V1)
- `postgres` — PostgreSQL 16, internal only, named volume `pgdata`
- `backend` — .NET API, depends on `postgres` healthy, runs EF migrations on start
- `frontend` — built static assets served by Caddy
- `caddy` — reverse proxy + TLS, the only publicly exposed service

---

## Consequences

- One cheap VPS covers V1 with room to grow; vertical scaling is enough for a
  long time at this scale.
- Identical local/prod shape eliminates "works on my machine" drift.
- Caddy removes the operational burden of manual TLS certificate management.
- The single-VPS, single-compose choice is a deliberate trade-off: simple and
  cheap now, with migration to managed hosting or object storage available later
  as pure infrastructure changes (no application code impact).
- Backups are an operational task (scheduled `pg_dump` + volume snapshot);
  defined during the Spec 01 setup, not left implicit.
