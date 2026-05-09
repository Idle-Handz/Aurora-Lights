# SQLite Content Correctness Checklist

Last updated: 2026-05-09

This is a focused checklist for the SQLite/content pipeline in `Aurora.App`, `Aurora.Importer`, and the synced content DB workflow.

The main goal is not just speed. It is trust:
- the DB should be fresh
- the DB should be explainable
- the DB should reconstruct Aurora content faithfully
- problems should be diagnosable without guesswork

## Current State

- The importer already has a strong normalized schema in [C:\Users\Ralla\source\repos\Aurora-Lights\Aurora.Importer\Resources\aurora-elements-schema.sql](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Importer/Resources/aurora-elements-schema.sql).
- `Aurora.App` already validates required tables/columns in [C:\Users\Ralla\source\repos\Aurora-Lights\Aurora.App\Services\DbElementLoader.cs](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.App/Services/DbElementLoader.cs).
- Package/source ownership is much better than before, especially with source-based package association and `Internal` treated as fallback.
- The compendium is now DB-backed first, which makes DB correctness more important than ever.

## Highest-Value Next Steps

### 1. Add Real Metadata Tables

- Add a proper metadata table instead of relying only on `PRAGMA user_version`.
- Suggested fields:
  - `schema_version`
  - `data_version`
  - `importer_version`
  - `built_utc`
  - `content_root_hash`
  - `source_file_count`
  - `element_count`
  - `resolved_element_count`
- Use this in both:
  - importer rebuild decisions
  - MAUI stale/incompatible DB detection

Why it matters:
- easier freshness checks
- clearer incompatibility handling
- better diagnostics in the app

### 2. Add DB Health / Validation Reporting

- Build on existing unresolved-link views:
  - `v_unresolved_loader_links`
  - `v_unresolved_loader_link_diagnostics`
- Add a small health-report layer that can answer:
  - unresolved references
  - missing source/package ownership
  - suspicious package/source mismatches
  - elements missing expected subtype/facet rows
  - companion/spell/class relationship gaps

Why it matters:
- turns correctness from “manual suspicion” into something measurable

### 3. Add XML-vs-DB Parity Checks

- Add a repeatable parity harness between XML load and DB load.
- Compare at least:
  - total element counts
  - counts by type
  - counts by source/package
  - spell counts and spell-access counts
  - companion counts and CR/type distributions
  - class/archetype/multiclass relationships
  - a curated known-good sample of important IDs

Why it matters:
- this is the clearest way to know whether the DB loader is “close enough” to trust

### 4. Improve Resolved-Content Explainability

- Add a debug-friendly resolved-content view/report showing:
  - winning element
  - source file
  - content package
  - source book
  - precedence rank
  - shadowed alternatives when applicable
- Make it easy to answer:
  - why did this package win?
  - what got overridden?
  - where did this element come from?

Why it matters:
- dramatically reduces guesswork when something is mapped or grouped wrong

## Loader-Specific Concerns

### 5. Reduce DB Loader Reconstruction Risk

`DbElementLoader` still reconstructs XML-like structures from normalized tables in:
- [C:\Users\Ralla\source\repos\Aurora-Lights\Aurora.App\Services\DbElementLoader.cs](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.App/Services/DbElementLoader.cs)

Follow-ups worth doing:
- identify the most fragile reconstruction paths
- add sample-based regression tests for those paths
- confirm subtype/setter coverage for important element families
- verify spellcasting, multiclass, and companion reconstruction specifically

Why it matters:
- most real correctness bugs now are likely to be reconstruction bugs, not schema-existence bugs

### 6. Consider Loader-Oriented Projection Views

- Add a small set of flatter DB views/tables optimized for MAUI loading.
- Keep the normalized schema, but provide a friendlier projection for the loader.

Why it matters:
- simpler loader code
- fewer reconstruction mistakes
- easier future parity testing

## Package / Source Ownership Follow-Ups

### 7. Add Suspicious Ownership Diagnostics

Flag cases such as:
- file has one dominant non-`Internal` source but package differs
- official-source element assigned to an obviously wrong package
- mixed-source files needing manual review

Why it matters:
- package/source correctness is now central to:
  - compendium browsing
  - ambiguity resolution
  - content trust

### 8. Keep `Internal` as Virtual / Fallback

Current rule to preserve:
- if `Internal` competes with a real source, the real source should win
- prefer official ownership when source evidence supports it

This is already improved, but should remain an explicit rule.

## Good-To-Have Items

### 9. FTS / Better Search Indexing

- Consider full-text search or dedicated search tables for compendium use.
- Particularly useful now that compendium is DB-backed first.

### 10. More Typed Facet Tables / Views

- Continue exposing typed metadata for:
  - spells
  - items
  - companions / beasts
  - classes / archetypes
  - feats / backgrounds

Why it matters:
- easier faceted compendium UX
- less string-parsing in app code

### 11. Developer-Facing “Content Doctor”

- Surface DB health inside the app or a small tooling command.
- Ideal outputs:
  - unresolved links
  - stale DB state
  - package/source conflicts
  - sync recommendations

### 12. Import Diff / Change Reports

- Add a way to report:
  - what changed between two imports
  - what XML sees that DB does not
  - what resolved winners changed after package/source rule changes

Why it matters:
- makes importer changes much safer to evaluate

## Recommended Implementation Order

1. Add metadata table and app-side metadata validation.
2. Add health-report queries/views.
3. Add XML-vs-DB parity checks for core element families.
4. Add resolved-content provenance/debug views.
5. Harden the most fragile `DbElementLoader` reconstruction paths.
6. Add nicer developer tooling like “content doctor” and import diffs.

## Short Version

The schema is already fairly capable. The next big improvements are not about adding many new tables. They are about making the content DB:
- fresher
- more explainable
- easier to validate
- easier to trust as a real replacement data source
