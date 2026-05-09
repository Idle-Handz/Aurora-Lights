# Aurora.App Audit Checklist

Last updated: 2026-05-09

This is a running parity and improvement checklist for `Aurora.App`, with a focus on replacing legacy `Aurora.Lights` functionality cleanly.

Related reference:
- [SQLite Content Correctness Checklist](C:/Users/Ralla/source/repos/Aurora-Lights/docs/sqlite-content-correctness-checklist.md)
  Use this for the deeper importer / synced DB / loader fidelity work that sits underneath several `Aurora.App` parity items.

## Current Snapshot

Recently addressed:
- `Settings` consolidation is in.
  Top-level app configuration is no longer split awkwardly across `Preferences` and `Content DB`.
- A first real `Compendium` now exists.
  It is DB-backed first, search-first, shell-accessible, and supports spell, item, and companion/beast filtering.
- `Build` guidance has a meaningful first pass.
  Tab ordering, next-step guidance, unresolved counts, and row-level guidance/highlighting are better than before.
- `Magic` has a meaningful first pass of parity work.
  Better filters, clearer feedback, multicaster sections, and extension spellcasting folding are now in place.
- `Session` persistence is in a better place.
  Coin edits and session save behavior are more coherent, and save failures are no longer silently swallowed in the same way.
- `Sheet` has a first pass of missing settings restored.
- MAUI dialog/message service is no longer just a broken stub.

Still clearly open:
- `Build` is still less guided and less descriptive than legacy.
- `Magic` is still thinner than legacy in nuanced spell-state presentation and some advanced spellcasting behavior.
- `Equipment` remains materially simpler than legacy.
- `Sheet` still feels more like an export utility than a full workspace.
- App-level updates/news and richer theme controls are still missing.
- SQLite/content trust work is still a major replacement concern.

## Prioritized Plan

### Priority 1 - Must Have For Confident Replacement

- Finish `Build` guidance parity.
  First-pass guidance exists now, but the page still needs richer unresolved-choice context, better focus/jump behavior, and clearer user feedback when an action cannot proceed.

- Finish `Magic` depth/parity.
  Multicaster support and filtering are much better, but the page still needs clearer treatment of known/prepared/always-prepared/granted spells and more confidence around advanced spellcasting edge cases.

- Reduce silent no-op behavior across MAUI pages.
  This is still one of the biggest trust issues in the app.

- Continue improving SQLite parity/fidelity before leaning on it more heavily.
  Content correctness is more important than startup speed if MAUI is meant to fully replace WPF.

### Priority 2 - High Value After Core Parity

- Improve `Equipment` parity around buy flows, inventory organization, containers, and encumbrance visibility.
- Make `Sheet` feel more like a full workspace instead of primarily an export utility.
- Strengthen content/source discoverability across `Settings`, `Manage`, and `Compendium`.
- Add folder-picking UX and less technical onboarding for additional content and `.index` sources.
- Strengthen content DB diagnostics and reload/update messaging.
- Keep PDF import improvements moving, especially equipment/portrait handling and clearer diagnostics.
- Review `Overview / Character Detail` for any remaining missing legacy information.

### Priority 3 - Nice To Have / Polish

- Add an app update / news / release-notes surface.
- Expand theme controls beyond the current light-mode toggle.
- Add more shell affordances such as command-palette-style navigation and additional shortcuts.
- Revisit preview UX parity for the character sheet after core feature parity is in place.

## Remaining Major Gaps

- `Build` still needs stronger guidance and selection context.
- `Magic` still needs deeper spell-state parity.
- `Equipment` is still simplified compared to legacy.
- `Sheet` still needs more workspace-like polish.
- Updates/news are still absent.
- Theme support is still thin.
- SQLite/content correctness still needs dedicated hardening work.

## Workflow Audit

### Start / Character Library

Current state:
- Functional and generally solid.
- Character library, import, creation, and opening flows are present.

Still worth reviewing:
- favorites, grouping, and browsing ergonomics versus legacy
- preload/loading diagnostics when character load falls back or only partially succeeds
- MRU/preload clarity and robustness

### Build

Recently improved:
- tab ordering is now more intentional
- next-step guidance is no longer just a loose tab hint
- unresolved counts exist on tabs
- unresolved rows can be highlighted and targeted more precisely

Still open:
- richer preview/context before opening pickers
- stronger "required and still unfilled" row treatment
- review silent returns in level-up / level-down / picker flows
- confirm ability-score method policy, especially where MAUI intentionally diverges
- review auto-save churn on ability editing

### Magic

Recently improved:
- better browsing/filtering
- clearer prepared/known feedback
- multi-spellcaster support
- extension spellcasting folded into parent sections
- `CharacterDetail` and `Session` are more spellcasting-aware than before

Still open:
- clearer separation between:
  - known
  - prepared
  - always prepared
  - granted / extended
- deeper parity for advanced spellcasting edge cases
- more confidence around unusual custom/renamed spellcasting sections
- continued polish for multicaster presentation and explanation

### Equipment

Current state:
- core usability is decent
- add/equip/remove/change-quantity flows are present

Still open:
- buy flows versus add flows
- richer inventory organization
- containers / storage concepts
- better carry / encumbrance visibility
- any remaining attack/equipment coupling affordances legacy exposed

### Session

Recently improved:
- persistence rules are clearer
- coin changes and session persistence no longer feel as split-brain
- save failures are surfaced more clearly

Still open:
- optional confirmations for destructive resets/rests where appropriate
- review whether custom resources should be partly derived from character data
- review whether conditions and rest assumptions need more configurability

### Sheet

Recently improved:
- first pass of missing settings restored

Still open:
- richer workspace feel beyond export/save/preview
- preview UX parity decisions
- support for extra fillable/override field concepts if still desired
- continued cross-platform polish for export/open-folder flows

### Overview / Character Detail

Current state:
- generally strong
- portrait, biography, and high-level character information are present

Still worth reviewing:
- feature/progression detail completeness
- whether all useful legacy overview data is present or intentionally moved elsewhere

## Shell / Navigation / Discoverability

Recently improved:
- app-level settings are consolidated
- compendium exists as a first-class nav item
- shell-level compendium search exists

Still open:
- updates/news surface
- richer theme controls
- clearer onboarding for content/source management
- optional future shell affordances like command palette / more shortcuts

## Content / Data / Platform Improvements

- Add folder-picking UX for additional content directories.
  The current settings page still relies too much on text entry for paths.

- Review package/source management UX for clarity after the content DB changes.

- Strengthen content DB diagnostics.
  Good next candidates:
  - clearer version/build metadata
  - loaded-from-DB vs fallback state
  - more human-readable sync details

- Continue improving SQLite parity/fidelity before depending on it more heavily.

- Keep PDF import improvements moving.
  Current known rough spots still include portrait extraction, equipment import, and diagnostic coverage for unapplied choices.

## Reliability / Logic Follow-Ups

- Audit guard-clause returns across MAUI pages and decide which should show user feedback instead of silently doing nothing.

- Review shared logic calls that may still behave like stubs or partial implementations in MAUI contexts.

- `CharacterManager.GenerateCharacterName()` is still not implemented in shared logic.
  If name generation is intended to exist in the modern app, this should be addressed.

- Continue checking dialog/confirmation behavior on non-Windows MAUI targets.

## Recommended Near-Term Order

1. Finish `Build` guidance parity
2. Finish `Magic` depth/parity
3. Do a dedicated MAUI silent no-op / user-feedback sweep
4. Return to SQLite/content correctness
5. Deepen `Equipment` parity
6. Continue polishing `Sheet`
