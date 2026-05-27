# Spec 03 — Authentication

## Goal

Email/password authentication with JWT sessions. There is **no anonymous
access** — every action that creates or reads building data requires an
authenticated user, because every incident entry must carry a real reporter
identity (Foundation: "anonymous reporting is disabled").

---

## Domain model

### `User`
| Field | Type | Notes |
|---|---|---|
| `Id` | guid | |
| `Email` | string | unique, lowercased |
| `PasswordHash` | string | BCrypt |
| `DisplayName` | string | shown as the reporter on entries |
| `PreferredLanguage` | string | BCP-47, for UI + (V3) personal re-translation |
| `CreatedAt` | timestamptz | |
| `IsActive` | bool | deactivation flag |

Role is stored on `BuildingMembership` (Spec 02), not on `User`, so a user could
hold different roles in different buildings later (V3).

---

## Endpoints (`/api/v1/auth`)

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/register` | `{ email, password, displayName, preferredLanguage }` → user + JWT |
| `POST` | `/login` | `{ email, password }` → JWT |
| `GET` | `/me` | current user + building membership summary |
| `POST` | `/forgot-password` | request reset (email link) |
| `POST` | `/reset-password` | `{ token, newPassword }` |

JWT carries `sub` (user id), `email`, and is 7-day expiry in V1. Building and
role are resolved server-side per request from membership (not baked into the
token, so a role change takes effect immediately).

---

## Authorization policies

- `RequireUser` — any authenticated, active user.
- `RequireBoard` — caller has a `board` membership in the target building.
- (`RequireAdmin` arrives in V4.)

Row-level rule everywhere: queries are scoped to the caller's building, and
write/edit/archive of an entry requires the caller to be its reporter
(board/admin widen read + export only).

---

## Frontend

- `LoginPage`, `RegisterPage` under `AuthLayout`.
- Password fields have a show/hide toggle.
- Auth token stored and attached by the Axios client; 401 → redirect to `/login`.
- After login/register, route to building create/join (Spec 02) if no membership,
  else to `/timeline`.

---

## Acceptance criteria

- [ ] A user can register, log in, and `GET /auth/me` returns their profile
- [ ] Passwords are BCrypt-hashed; never returned by any endpoint
- [ ] Email is unique and case-insensitive
- [ ] All building/entry endpoints reject unauthenticated requests with 401
- [ ] Forgot/reset password works end to end
- [ ] No endpoint allows creating an entry without an authenticated reporter

---

## Status
- [x] Reworked to **passwordless magic-link** (no passwords — by product decision).
      `POST /auth/magic-link` emails a one-time link (Mailjet; dev returns the link
      in the response), `POST /auth/verify` exchanges the token for a JWT, single-use
      + 20-min expiry. `PATCH /auth/me` edits name/language; new users complete their
      name after first sign-in. Google sign-in planned later. No password, no
      forgot/reset flow. Verified via Playwright.
