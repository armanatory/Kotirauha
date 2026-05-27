# ADR 002 — Translation and AI Provider Strategy

**Date:** 2026-05-27
**Status:** Accepted

---

## Context

Multilingual participation is the heart of Kotirauha: residents write in their
own language, and the building reads in a shared language (e.g. Finnish). This
requires automated translation. Two non-negotiable product rules constrain the
design (see Foundation §6 and §"AI Translation"):

1. The reporter's **original text is never overwritten**. Both original and
   translation must always be viewable.
2. Translations must be **clearly labelled machine-generated**, with the source
   language shown. We never present a translation as if a human wrote it.

We also want to avoid coupling the codebase to one vendor and to keep cost and
reliability observable.

---

## Decision

- All translation goes through a single backend interface,
  `ITranslationProvider`, with one implementation active per environment,
  selected by environment variable (`TRANSLATION_PROVIDER`).
- The default provider is **Anthropic Claude**. Its prompt-caching support keeps
  cost low for the stable system prompt that enforces neutral, literal,
  non-embellished translation. A second provider (e.g. a dedicated MT service or
  OpenAI) can be added behind the same interface.
- Translation happens **on entry creation**: the entry is stored verbatim with
  its detected/declared `originalLanguage`, then a translation row is created for
  the building's shared language. Translations are stored, not regenerated on
  every read, so the record is stable and cheap.
- Translation rows are keyed by `(entry_id, target_language)` and carry
  `provider`, `model`, and an `isMachineGenerated = true` flag. The UI always
  renders the "AI-generated translation from {source}" notice.
- The translation prompt instructs the model to translate **faithfully and
  neutrally**: no softening, no embellishment, no added interpretation, and to
  preserve the factual tone. Profanity and emotional wording are translated as
  written, not sanitised — the record must reflect what the resident actually said.
- If translation fails, the entry is still saved with its original text; the
  translation is marked `pending` and retried. A missing translation must never
  block documentation.

---

## What the AI must NOT do

- It must not rewrite, summarise, or "clean up" the original.
- It must not judge, classify severity, or assign blame.
- It must not produce an unlabelled translation.
- It is a translation aid only — not a moderator and not an oracle.

---

## Consequences

- One env-var swap changes provider; no call sites change.
- Stored translations make the timeline cheap to read and stable over time.
- The neutrality and "original is sacred" rules are enforced at the data layer
  (separate immutable original) and the prompt layer (faithful translation).
- Cost/observability of the provider becomes a V4 operator concern; the
  interface already records provider + model per translation for later auditing.
