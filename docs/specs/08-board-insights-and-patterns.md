# Spec 08 — Board Insights & Patterns (V2)

## Goal

Give board/manager members a read-only overview that surfaces *recurring*
disturbances — the core reason a housing board needs this product. Most board
oversight primitives already exist from V1 (board role, member list,
include-archived, export). This spec adds an aggregation/insights surface.

Neutrality still applies: the insights describe *frequency and distribution of
reported observations*, never guilt. No resident is "ranked" as a culprit; the
grouping is by category, apartment subject, and time.

---

## Endpoint

`GET /api/v1/insights` (board/admin only, building-scoped)

Query: optional `from`, `to`.

Response:
```json
{
  "totalEntries": 42,
  "byCategory": [{ "category": "Noise", "count": 18 }, ...],
  "bySubjectApartment": [{ "apartment": "C12", "count": 7 }, ...],
  "byMonth": [{ "month": "2026-03", "count": 5 }, ...],
  "topRecurring": [
    { "category": "Noise", "subjectApartment": "C12", "count": 6, "firstAt": "...", "lastAt": "..." }
  ]
}
```

- Excludes archived entries by default (board may pass `includeArchived=true`).
- `topRecurring` groups by `(category, subjectApartment)` where count ≥ 2,
  ordered by count desc — the "recurring problem" signal.

---

## Frontend

- New `/insights` page, board-only (nav item hidden for residents).
- Cards: total entries, a category bar breakdown, a by-month sparkline/bars,
  and a "Recurring patterns" table (category × apartment × count × date range)
  with a link to a filtered timeline view.
- Calm, administrative styling — counts and tables, not alarming visuals.

---

## Acceptance criteria

- [ ] Board sees `/insights`; residents do not (endpoint 403s for residents)
- [ ] Category, apartment, and month breakdowns reflect the building's entries
- [ ] Recurring table groups (category, apartment) with count ≥ 2
- [ ] Date-range filter applies; archived excluded unless explicitly included
- [ ] Verified with Playwright

---

## Status
- [x] Shipped (V2). Board-only /insights with totals, category/month breakdowns,
      recurring (category × apartment, count ≥ 2) table. Multi-image attachments
      also landed (entry create accepts multiple images; detail renders all).
      Verified via Playwright.
