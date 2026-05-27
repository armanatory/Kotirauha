# ADR 003 — Data Model and Record Integrity

**Date:** 2026-05-27
**Status:** Accepted

---

## Context

Kotirauha's entire value proposition is that its records are **trustworthy**:
neutral, timestamped, attributed, and tamper-evident. If residents or boards
suspect entries can be silently edited or deleted, the documentation is
worthless for official processes. The Foundation states the integrity rules
plainly: entries are timestamped automatically, edits are visible, deleted
records are archived, and anonymous reporting is disabled.

This ADR fixes how the data model enforces those rules.

---

## Decision

### Identity and attribution
- Every `IncidentEntry` has a non-null `reporterUserId`. There is no anonymous
  path. The reporter's identity is part of the record.
- Every entry has a server-set `createdAt` (UTC). Clients cannot set it.

### Original text is immutable
- An entry stores `originalText` + `originalLanguage`. These represent what the
  reporter actually wrote and are never mutated after creation.
- Translations live in a separate `IncidentTranslation` table keyed by
  `(entry_id, target_language)`. Translating never touches the original.

### Edits are versioned, not destructive
- Editing an entry's description writes the previous state to
  `IncidentRevision` (editor, before snapshot, `editedAt`) before applying the
  change, and re-translates. The UI exposes "edited" state and revision history.
- `originalText` of the *first* version is always retained as the canonical
  original; edits append revisions rather than erasing history.

### Deletes are archival
- Deleting an entry sets `archivedAt` + `archivedByUserId` (soft delete). Rows
  are never hard-deleted from the timeline path. Archived entries drop out of the
  default timeline but remain in the record and in exports flagged as archived.
- Hard purge, if ever needed (e.g. GDPR), is a separate explicit operator action
  in a later version, not the default delete.

### Scoping and access
- All entry queries are scoped by `building_id`. A user only ever sees their own
  building's records.
- A reporter may edit/archive only their own entries. Board/admin roles widen
  *read* scope (and export) but do not gain the right to alter another resident's
  original text.

---

## Consequences

- The model is append-friendly: revisions and archives accumulate rather than
  overwrite, which is exactly what an evidentiary record needs.
- Soft delete + revisions add storage and query-filtering overhead (every
  timeline query filters `archivedAt IS NULL`), accepted as the cost of trust.
- Exports can faithfully represent "this entry was edited / archived", which is
  important for board and landlord credibility.
- GDPR hard-delete is intentionally deferred and made explicit, so the default
  behaviour never silently destroys evidence.
