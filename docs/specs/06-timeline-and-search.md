# Spec 06 — Timeline & Search

## Goal

The shared, chronological view of a building's incidents. Residents browse all
(non-archived) entries for their building, filter by category and date range, and
search by keyword. This is the resident home screen.

---

## Endpoint

`GET /api/v1/entries` (scoped to the caller's building)

Query parameters:
| Param | Notes |
|---|---|
| `category` | optional, one of the category enum |
| `from`, `to` | optional ISO dates, filter by `OccurredAt` |
| `q` | optional keyword search |
| `cursor` / `page` | pagination |

Defaults: non-archived only, sorted by `OccurredAt` descending.

Response items include: id, category, `OccurredAt`, reporter display name,
subject apartment, a snippet of the **shared-language** text (translation if
present, else original), whether an attachment exists, and the `edited` flag.

---

## Search

- Keyword search uses PostgreSQL full-text search over **both** the original text
  and the shared-language translation, so a Finnish-speaking board member can
  find an entry a resident wrote in English, and vice versa.
- Search is building-scoped and excludes archived entries by default.

---

## Frontend

- `TimelinePage`: reverse-chronological list of entry cards. Each card shows
  category icon + label, date, reporter, subject apartment, a one-line snippet,
  an attachment indicator, and an "edited" badge if applicable.
- Filter bar: category chips, date range, search box. Filters combine (AND).
- Empty state: a calm, neutral prompt to create the first entry — no alarmist
  framing.
- Board members get a toggle to include archived entries (clearly marked).

---

## Acceptance criteria

- [ ] Timeline lists the caller's building entries, newest first, excluding archived
- [ ] Category filter, date-range filter, and keyword search each work and combine
- [ ] Keyword search matches text in either the original or the translation
- [ ] Pagination works for large timelines
- [ ] A resident never sees another building's entries
- [ ] Board can optionally include archived entries, clearly marked

---

## Status
- [ ] Not yet started
