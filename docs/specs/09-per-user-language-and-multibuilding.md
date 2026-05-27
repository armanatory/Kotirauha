# Spec 09 — Per-User Language & On-Demand Translation (V3)

## Goal

The building has one shared language, but individual members may not read it. V3
lets any member request a translation of an entry into *their own* language,
on demand, stored and reused. The per-user UI language already exists (Profile
language switcher, spec 03/13). This spec adds entry re-translation.

Multi-building membership (one user in several buildings with a building switcher)
and notification digests are also V3-scoped; see "Deferred" below.

---

## On-demand translation

`POST /api/v1/entries/{id}/translate` body `{ "language": "ar" }`
(building-scoped, any member).

- If a `completed` translation for that language already exists, return it.
- Otherwise create/queue one via `EntryTranslationService.TranslateEntryAsync`
  (same provider, same neutral prompt, same labelling rules as spec 05).
- The original is never modified; this only adds an `IncidentTranslation` row.
- Returns the `TranslationDto`.

### Frontend
- On the entry detail page, when the viewer's preferred language is neither the
  original nor the shared language (or any translation already present), show a
  "Translate to {my language}" button. Clicking it adds and displays that
  translation, with the same "AI-generated translation from {source}" notice.
- All translations on the entry are listed, each clearly labelled machine-generated.

---

## Deferred within V3 (larger follow-ups)

- **Multi-building membership**: the schema already allows multiple
  `BuildingMembership` rows per user; what's missing is an "active building"
  switcher and replacing the single-membership resolver. This is a cross-cutting
  refactor (every building-scoped query) and is intentionally a separate change
  so it doesn't destabilise the shipped single-building flow.
- **Notification digests**: weekly summary email to the board. Depends on an
  email provider, out of scope until one is chosen.

---

## Acceptance criteria

- [ ] A member can request a translation into a new language; it is stored and labelled
- [ ] Requesting an existing language returns the existing translation (no duplicate)
- [ ] Original text is never modified
- [ ] Entry detail offers "translate to my language" when relevant and lists all translations
- [ ] Verified with Playwright

---

## Status
- [x] On-demand translation shipped (V3): POST /entries/{id}/translate, idempotent
      for completed languages, original never touched; entry detail lists all
      translations and offers "translate to my language". Verified via Playwright
      (Swedish resident translated an English entry). Multi-building switching and
      notification digests remain deferred as noted above.
