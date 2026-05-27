# Spec 11 — Quick Capture & PWA (mobile-first)

## Goal

The defining moment for this product: a non-technical resident comes home tired,
notices a disturbance (a smell in the stairwell), and wants to write it down in
seconds — on their phone. Everything must bend toward that. This spec reworks
the capture flow for one-thumb speed and makes the app installable as a PWA.

---

## Quick capture

The "New entry" page is a fast capture screen, not a form:

- **Auto-focused text box** on load, so the keyboard is ready — the user can
  just start typing. Friendly placeholder ("e.g. Strong burning smell in the
  stairwell tonight").
- **Category as big emoji chips** (🔊 Noise, 👃 Smell, 🚬 Smoke, 🅿️ Parking,
  ⚠️ Safety, 🧹 Shared space, 📝 Other) — not a dropdown. **"Other" is selected
  by default**, so the user can type and save with zero category interaction;
  one tap refines it.
- **Time defaults to now**; **language defaults to the user's own language**,
  shown as a compact "Writing in …" pill (changeable per entry).
- **Everything optional is hidden** under "Add details (optional)": exact
  time, subject apartment, and photo (camera-capture enabled on phones).
- **One large, thumb-reachable "Save report" button** fixed above the bottom
  nav on phones.
- After saving, return to the timeline with a quiet "Reported — thanks." toast.

Minimum interaction to file a report: focus is automatic → type one sentence →
tap Save. Category, time, and language all have sensible defaults.

### Supporting changes
- Big "＋ Report" floating button on the timeline (phones).
- Language is chosen at **registration** (so a non-English speaker's text is
  tagged with the right source language for translation).

---

## PWA

- Web app manifest (name, standalone display, portrait, theme color `#0f766e`,
  start_url `/timeline`, 192/512 + maskable icons).
- Service worker (Workbox via `vite-plugin-pwa`, autoUpdate) precaching the app
  shell; `navigateFallback` excludes `/api`.
- Mobile meta: viewport-fit cover, theme-color, apple-mobile-web-app tags,
  apple-touch-icon.
- Icons generated from a single `public/icon.svg` (house mark) via a build script.

Native app and true offline write-queue remain out of scope ("PWA for now").

---

## Acceptance criteria

- [ ] On a phone viewport, a report can be filed with type + Save (defaults cover the rest)
- [ ] Category chips, language pill, and "Add details" all work; photo capture is optional
- [ ] Timeline shows a prominent Report action; entry lands translated into the shared language
- [ ] Manifest + service worker are served and the SW registers in the production build
- [ ] Language chosen at registration flows through to entry source language
- [ ] Verified with Playwright at 390×844 and a production preview build

---

## Status
- [x] Shipped. Quick-capture flow, timeline FAB, registration language, and an
      installable PWA (manifest + Workbox SW + icons). Verified via Playwright on
      a phone viewport (filed a Swedish smell report end to end) and confirmed the
      service worker registers on the production preview build.
