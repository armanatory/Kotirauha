# Spec 04 — Incident Entries

## Goal

The core of the product: residents create structured incident reports. Entries
are timestamped, attributed to a real reporter, edited with visible history, and
archived (never hard-deleted) on delete — per the integrity rules in ADR 003.

Translation of the entry text is specified separately (Spec 05); this spec
covers the entry lifecycle and storage. An entry is created with its original
text first; translation is triggered as part of creation.

---

## Domain model

### `IncidentEntry`
| Field | Type | Notes |
|---|---|---|
| `Id` | guid | |
| `BuildingId` | guid | scope |
| `ReporterUserId` | guid | non-null — no anonymous entries |
| `OccurredAt` | timestamptz | when the incident happened (reporter-supplied) |
| `Category` | enum | see list below |
| `OriginalText` | string | the reporter's words — immutable after create |
| `OriginalLanguage` | string | BCP-47, declared or detected |
| `SubjectApartment` | string | optional — the apartment the report concerns |
| `CreatedAt` | timestamptz | server-set |
| `EditedAt` | timestamptz? | set when an edit occurs |
| `ArchivedAt` | timestamptz? | soft-delete marker |
| `ArchivedByUserId` | guid? | |

### `IncidentAttachment`
| Field | Type | Notes |
|---|---|---|
| `Id` | guid | |
| `EntryId` | guid | |
| `StorageKey` | string | written via `IAttachmentStore` |
| `ContentType` | string | image/* only in V1 |
| `CreatedAt` | timestamptz | |

V1: at most one image per entry (multi-image is V2).

### `IncidentRevision`
| Field | Type | Notes |
|---|---|---|
| `Id` | guid | |
| `EntryId` | guid | |
| `EditedByUserId` | guid | |
| `PreviousText` | string | snapshot of the description before this edit |
| `EditedAt` | timestamptz | |

---

## Categories

`Noise`, `Smell`, `SmokingOrIncense`, `Parking`, `SafetyConcern`,
`CommonAreaMisuse`, `Other`. Stored as a stable enum; labels are localised in the
frontend i18n bundle.

---

## Endpoints (`/api/v1/entries`)

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `POST` | `/` | user | Create entry (multipart: fields + optional image) → triggers translation (Spec 05) |
| `GET` | `/{id}` | user (same building) | Entry with original + translations + attachment + revision history |
| `PATCH` | `/{id}` | reporter | Edit description / occurredAt / category / subjectApartment → writes a revision + re-translates |
| `POST` | `/{id}/archive` | reporter or board | Soft-delete (set `ArchivedAt`) |
| `POST` | `/{id}/restore` | board | Un-archive |

`OriginalText` and `OriginalLanguage` of the first version are never overwritten;
an edit appends an `IncidentRevision` with the previous text and updates the
current description, then re-translates.

---

## Frontend

- `NewEntryPage`: date/time picker (defaults to now), category select,
  description textarea (in the resident's own language), optional subject
  apartment, optional image upload. On submit, shows the saved entry with its
  translation.
- `EntryDetailPage`: shows original (with language badge), the AI translation
  (labelled — Spec 05), attachment, reporter, timestamps, and an "Edited" badge
  with revision history if any. Reporter sees Edit / Archive; board sees Archive /
  Restore.
- Tone: neutral, factual. No severity rating, no "report this neighbour" framing.

---

## Acceptance criteria

- [ ] A resident creates an entry; it is stored with reporter, `CreatedAt`, and
      `OriginalText`/`OriginalLanguage`
- [ ] Creating an entry triggers translation into the building's shared language
- [ ] An optional image uploads and is retrievable
- [ ] Editing the description appends an `IncidentRevision` and never mutates the
      stored original; an "edited" state is visible
- [ ] Archiving sets `ArchivedAt` and removes the entry from the default timeline
      without deleting the row; board can restore
- [ ] A user cannot create, edit, or read entries outside their building

---

## Status
- [x] Shipped (V1). Multipart create with optional image, edit→revision (original
      preserved), archive/restore, building scoping. Verified via Playwright.
