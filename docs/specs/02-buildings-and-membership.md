# Spec 02 — Buildings & Membership

## Goal

Introduce the `Building` and `BuildingMembership` model. A building is the unit
of tenancy: every incident, every timeline, and every export is scoped to one
building. A building has a **shared language** (the default translation target),
and residents join via a join code.

This spec depends on Spec 03 (auth) for the `User` identity; build them together
if convenient, but the building model and scoping rules are defined here.

---

## Domain model

### `Building`
| Field | Type | Notes |
|---|---|---|
| `Id` | guid | |
| `Name` | string | e.g. "As Oy Mäkikatu 4" |
| `Address` | string | optional in V1 |
| `SharedLanguage` | string | BCP-47 code, default `fi` — the translation target |
| `JoinCode` | string | short, unique, regenerable; used by residents to join |
| `CreatedAt` | timestamptz | server-set |

### `BuildingMembership`
| Field | Type | Notes |
|---|---|---|
| `Id` | guid | |
| `UserId` | guid | FK → User |
| `BuildingId` | guid | FK → Building |
| `Role` | enum | `resident`, `board` (V2 widens), `admin` (V4) |
| `ApartmentNumber` | string | the member's own apartment |
| `JoinedAt` | timestamptz | |

Unique constraint on `(UserId, BuildingId)`. In V1 a user belongs to exactly one
building (the first they join); multi-building membership is V3.

---

## Endpoints (`/api/v1`)

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `POST` | `/buildings` | user | Create a building (creator becomes `board`) |
| `GET` | `/buildings/me` | user | Get the caller's building + their membership |
| `POST` | `/buildings/join` | user | Join via `{ joinCode, apartmentNumber }` |
| `POST` | `/buildings/{id}/join-code` | board | Regenerate the join code |
| `GET` | `/buildings/{id}/members` | board | List members (board oversight) |

All other feature endpoints (entries, timeline, export) resolve the caller's
building from their membership and scope every query by `building_id`.

---

## Rules

- A new user who creates a building becomes its `board` member automatically.
- A user who joins via code becomes a `resident`.
- `SharedLanguage` is chosen at building creation (default `fi`) and is the
  default translation target for every entry in that building.
- The join code is the only way to enter a building in V1 (no public discovery).
- Membership role gates read scope and export, never the integrity rules: a
  `board` member can read and export all entries but cannot alter another
  resident's original text (see ADR 003).

---

## Frontend

- `BuildingPage` (`/building`): shows building name, shared language, the user's
  apartment and role. For `board`: the join code (with regenerate) and member list.
- First-run flow: a user with no membership is routed to a "Create or join a
  building" screen after login.

---

## Acceptance criteria

- [ ] A user can create a building and is recorded as its `board` member
- [ ] A second user can join with the join code + apartment number as `resident`
- [ ] `GET /buildings/me` returns the caller's building and membership
- [ ] A board member can list members and regenerate the join code
- [ ] Every entry/timeline/export query is scoped to the caller's building
- [ ] A user with no building is routed to the create/join screen

---

## Status
- [x] Shipped (V1). Create/join via code, board auto-role, members list, join-code
      regenerate, building-scoped access. Verified via Playwright.
