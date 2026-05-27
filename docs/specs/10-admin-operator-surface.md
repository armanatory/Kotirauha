# Spec 10 — Admin / Operator Surface (V4)

## Goal

A platform operator (not a building board) needs visibility across all buildings
and confidence that the translation layer is healthy. V4 adds a platform-admin
role and a small operator dashboard. It is read-mostly; destructive operations
are out of scope for now.

---

## Platform admin identity

- `User.IsPlatformAdmin` (bool, default false). Distinct from building roles —
  it is platform-wide, not per-building.
- Seeding: on startup, if `ADMIN_EMAIL` is set and a matching user exists, that
  user is promoted to platform admin (idempotent). This avoids a chicken-and-egg
  bootstrap with no UI for elevation.
- `CurrentUserDto` gains `isAdmin` so the frontend can gate the admin nav/route.

---

## Endpoints (`/api/v1/admin`, platform-admin only)

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/overview` | platform counts: users, buildings, entries, translations, archived entries |
| `GET` | `/buildings` | every building with member count + entry count + shared language |
| `GET` | `/translation-status` | active provider name, whether it is the real provider or the offline stub, and the configured model |

All admin endpoints return 403 for non-admins.

---

## Frontend

- `/admin` route + an "Admin" nav item, shown only when `isAdmin`.
- Overview cards (counts), a buildings table, and a translation-provider status
  panel that clearly flags when the offline stub is active ("translations are
  placeholders — set a real provider key").
- Styled as an operator console: dense, factual.

---

## Deferred

- Provider token usage / cost / error-rate metrics (needs per-call accounting).
- User management (deactivate, role change) and moderation — additive later.
- Separate admin subdomain/app (single app with role gate is enough at this scale).

---

## Acceptance criteria

- [ ] A user promoted via `ADMIN_EMAIL` sees `/admin`; others get 403 and no nav item
- [ ] Overview shows correct platform counts
- [ ] Buildings table lists all buildings with member + entry counts
- [ ] Translation-status panel reports the active provider and flags the stub
- [ ] Verified with Playwright

---

## Status
- [x] Shipped (V4). `User.IsPlatformAdmin` + `ADMIN_EMAIL` startup seeding,
      `isAdmin` on /auth/me, admin-only /admin endpoints (overview, buildings,
      translation-status), operator console page with admin-only nav gate.
      Verified via Playwright. Token usage/cost metrics + user management deferred.
