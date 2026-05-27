# Spec 07 — Export & Reporting

## Goal

Generate a neutral, timestamped, printable report of a building's incidents,
suitable for housing-company communication, landlord discussions, or
environmental-health reporting. The export is the bridge between the in-app
timeline and official, off-platform processes.

---

## Endpoint

`POST /api/v1/export` (scoped to the caller's building)

Body:
```json
{
  "from": "2026-01-01",
  "to": "2026-05-27",
  "categories": ["Noise", "SmokingOrIncense"],
  "subjectApartment": "B12",
  "includeArchived": false,
  "format": "pdf"
}
```
All filters optional. `format` is `pdf` (V1) — printable HTML is the fallback.

Authorisation: any resident may export the building's record; board members are
the primary consumers. (Neutrality applies — see below.)

---

## Report contents

- **Header**: building name + address, generated-at timestamp, generated-by
  (reporter identity), applied filters, total entry count.
- **Per entry**, in chronological order:
  - occurred-at + created-at timestamps
  - category, subject apartment, reporter display name
  - the **original** text with its language label
  - the **shared-language translation** with the "AI-generated from {lang}" notice
  - an "edited" / "archived" marker where applicable
  - a thumbnail/reference for any attachment
- **Footer**: a neutrality statement — *"This document is a factual record of
  reported observations. It does not assign guilt and is not a legal
  determination."*

The report preserves the dual original+translation presentation; it never drops
the original in favour of the translation.

---

## Generation

- Server renders an HTML template → PDF (e.g. a headless-Chromium / PDF library).
- Generated reports are streamed to the client; storing report metadata
  (`ExportReport`) for an audit trail is a V2 addition, not required in V1.

---

## Frontend

- `ExportPage`: filter form mirroring the timeline filters + format choice +
  a preview of how many entries match, then "Generate report" → download.

---

## Acceptance criteria

- [ ] An export produces a PDF containing the filtered entries in chronological order
- [ ] Each entry shows original + labelled translation, timestamps, reporter, category
- [ ] Edited and archived states are visibly marked in the export
- [ ] The neutrality footer is present on every report
- [ ] Filters (date, category, subject apartment, includeArchived) apply correctly
- [ ] Export is scoped to the caller's building only

---

## Status
- [x] Shipped (V1) as printable HTML (print-to-PDF in browser). Filters, dual
      original+labelled translation, edited/archived flags, neutrality footer.
      Native server-side PDF generation is a later enhancement. Verified via Playwright.
