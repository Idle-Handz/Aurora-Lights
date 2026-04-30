# Aurora.App Audit Checklist

Last updated: 2026-04-29

This is a running parity and improvement checklist for `Aurora.App`, with a focus on replacing legacy `Aurora.Lights` functionality cleanly.

## Prioritized Plan

### Priority 1 — Must Have For Confident Replacement

- Restore stronger guided-selection behavior on `Build`.
  This is one of the biggest day-to-day usability gaps versus legacy and affects the core character-building flow.

- Strengthen `Magic` parity.
  In particular: richer spell filtering/browsing, better prepared/known spell feedback, and better handling of more complex spellcasting structures.

- Clarify `Session` persistence rules.
  Currency edits living outside the session save path and silent session auto-save failures are real trust issues.

- Restore missing sheet-generation settings.
  `Form Fillable`, formatting-related controls, legacy spellcasting page selection, and related sheet output settings are important parity items.

- Reduce silent no-op behavior across MAUI pages.
  “Nothing happened” states are a replacement blocker because they undermine trust in the app.

- Continue improving SQLite parity/fidelity before leaning on it more heavily.
  Content correctness is more important than startup speed if MAUI is meant to fully replace WPF.

### Priority 2 — High Value After Core Parity

- Improve selection context and previews on `Build`.
- Add richer spell compendium/search integration for `Magic`.
- Improve `Equipment` parity around buy flows, inventory organization, containers, and encumbrance visibility.
- Strengthen content/source discoverability across `Settings` and `Manage`.
- Add folder-picking UX and less technical onboarding for additional content and `.index` sources.
- Strengthen content DB diagnostics and reload/update messaging.
- Keep PDF import improvements moving, especially equipment/portrait handling and clearer diagnostics.
- Review `Overview / Character Detail` for any remaining missing legacy information.

### Priority 3 — Nice To Have / Polish

- Add a real global compendium / quick-search surface.
- Add an app update / news / release-notes surface.
- Expand theme controls beyond the current light-mode toggle.
- Add more shell affordances such as command-palette-style navigation and additional shortcuts.
- Make `Sheet` feel more like a full workspace instead of primarily an export utility.
- Revisit preview UX parity for the character sheet after core feature parity is in place.

## Confirmed Gaps

- Add richer theme customization.
  Current MAUI preferences mainly expose light mode. Legacy WPF had a fuller theme/accent experience.

- Add a clearer app update / news / syndication experience.
  MAUI has content index and DB update flows, but not an obvious equivalent to legacy update/news surfaces.

- Add a real global compendium / quick-search experience.
  MAUI has picker dialogs, but not an obvious user-facing compendium search workflow.

- Improve discoverability of content/source management.
  The functionality exists, but it is distributed across `Settings` and `Manage` and is less obvious than the legacy structure.

- Reduce silent no-op behavior.
  Several pages use guard-clause `return`s when snapshot/tab state is missing. These should be reviewed for user feedback.

- Add a more capable global shell experience.
  MainLayout is intentionally minimal right now. Beyond save and help, MAUI lacks the richer shell affordances that would help it feel like a full desktop replacement.

## Workflow Audit

### Start / Character Library

- Review parity for favorites, grouping, and browsing ergonomics compared to legacy.
- Add clearer preload/loading diagnostics when character load falls back or only partially succeeds.
- Revisit MRU/preload behavior for clarity and robustness.
  There is already defensive logic around preload races in `Start.razor`; this area is worth keeping an eye on.

### Build

- Improve selection context in the build page.
  The current UI is functional, but many choices are shown as label/current value/change without much descriptive context.

- Consider richer inline descriptions or quick-preview affordances for build selections.

- Review silent returns in level up / level down / picker flows and add user feedback where appropriate.

- Inspect ability score flow parity carefully.
  The page appears feature-rich, but ability generation and advancement should be checked step-by-step against legacy behavior.

- Restore stronger guided-selection behavior.
  Legacy had a more explicit selection navigation/focus flow through `SelectionRuleNavigationService`. MAUI currently has a lighter “Next step” banner, but it only changes tabs and does not focus the first unresolved rule.

- Improve standard-array parity.
  Legacy ability generation exposed a drag/drop-style array workflow; MAUI currently uses a simpler dropdown assignment approach.

- Review whether the `Manual` ability method should exist at all.
  It may be useful, but it is also a parity divergence if the legacy build flow did not intentionally expose it the same way.

- Revisit auto-save churn on ability score changes.
  Point-buy and rolling operations can save very frequently when auto-save is on.

- Add richer previews for build options.
  The picker/change flow works, but class/race/background/feat choices would benefit from more at-a-glance description and requirement visibility before selection.

### Magic

- Review parity for spell browsing, spell details, and known/prepared workflows.
- Add clearer diagnostics or user feedback when a spell-change action cannot be applied.
- Consider a stronger spell discovery/search experience beyond the current picker flow.

- Add richer spell filtering and browsing.
  Legacy had a stronger spellcasting browser/filter model, including level, school, class, and source filtering. MAUI currently presents a much simpler list/detail layout.

- Revisit multiclass and extended spellcasting parity.
  The current MAUI page mostly presents a single spellcasting summary and grouped known-spell rows. Legacy spellcasting view models handled extensions and richer spell-list composition.

- Improve prepared-spell interaction feedback.
  Prepared toggles work, but the user feedback is much lighter than legacy status messaging.

- Consider whether cantrips, known spells, prepared spells, and extended spells need stronger separation in the UI.
  Legacy had more distinct spellcasting concepts in the viewmodel and behavior.

- Evaluate whether a dedicated spell compendium/search surface should integrate with Magic.
  Legacy had a fuller spell compendium model that also tied into quick search.

### Equipment

- Review parity for inventory browsing, equip flows, and gear slot interactions.
- Confirm whether save/error feedback should be made more visible here, similar to `Build` and `Magic`.
- Review whether any legacy equipment affordances are missing, especially around bulk item management and notes.

- Review parity for buying vs adding items.
  Legacy equipment flow had stronger coin-aware buy behavior, while MAUI currently centers around add/equip/remove interactions.

- Add stronger inventory organization controls if needed.
  Legacy desktop inventory supported richer item ordering and management behaviors.

- Evaluate whether attack/equipment coupling needs more surface area.
  Legacy inventory flow had more explicit attack-related affordances around equipment items.

- Review whether weight/encumbrance should be made more visible.
  The current MAUI equipment workspace shows item weights, but not a strong overall carry/encumbrance summary.

- Consider whether storage/containers need dedicated UI.
  Legacy equipment view model had explicit storage-related concepts that MAUI does not appear to surface.

### Session

- Review parity for resource tracking, conditions, rest flows, and session save behavior.
- Consider whether rests and destructive session changes should have optional confirmations.
- Check whether session edits should surface stronger save state feedback.

- Clarify persistence boundaries inside Session.
  HP, death saves, conditions, spell slots, and custom resources are session-state data, but currency edits on the Session page currently modify the character snapshot and require a normal character save instead of the session save button.

- Stop swallowing session save failures silently.
  `AutoSave()` currently suppresses save exceptions, which risks silent data-loss behavior.

- Consider deriving trackable resources automatically.
  The Session page currently relies on manual custom resources rather than surfacing class/feature resources from loaded character data.

- Review the built-in condition list for extensibility.
  The current list is a fixed static set rather than content-driven.

- Review rest behavior assumptions.
  Long-rest and short-rest effects are convenient, but some of the current reset/recovery behavior may need configurability or clearer messaging.

### Sheet

- Review parity for preview/export flow compared to legacy.
- Consider whether the page needs a richer in-app sheet browsing experience beyond preview/save.
- Revisit non-Windows export/open-folder behavior after real macOS testing.

- Restore missing sheet-generation settings from legacy.
  WPF exposed more output controls, including form-fillable generation, formatting toggles, legacy spellcasting page selection, and additional sheet-related preferences. MAUI currently exposes only page/card toggles.

- Consider whether sheet settings should be more discoverable from the sheet page itself.
  Legacy exposed a dedicated character-sheet settings entry point directly from the sheet surface. MAUI currently splits related toggles between `Sheet` and `Preferences`.

- Review preview parity.
  Legacy embedded a persistent preview panel alongside sheet actions, while MAUI opens a separate preview window. The current MAUI flow is functional, but it is a meaningful UX difference.

- Review support for extra fillable/override fields.
  Legacy had an "Additional Fields" concept for temporary/manual population of some not-yet-implemented sheet fields. MAUI does not appear to surface an equivalent.

- Review whether save/export messaging is sufficient.
  MAUI shows save success and exposes an "Open Folder" affordance, but the export surface still feels closer to a utility workflow than a full character-sheet workspace.

### Overview / Character Detail

- Review parity for portrait management, biography, background, features, and progression detail.
- Confirm that all information shown in the legacy overview is either present here or intentionally relocated.

## Content / Data / Platform Improvements

- Add folder-picking UX for additional content directories.
  The current settings page uses text entry for paths.

- Review package/source management UX for clarity after the content DB changes.

- Improve content/source discoverability.
  Content packages live in `Settings`, while character source restrictions live in `Manage`. The functionality is present, but the split is not very obvious for new users.

- Review content index/source onboarding.
  Fetching `.index` files by URL works, but it is still a fairly technical workflow compared to a more guided content-library experience.

- Strengthen content DB diagnostics.
  Good next candidates: clearer version/build metadata, loaded-from-DB vs fallback state, and more human-readable sync details.

- Continue improving SQLite parity/fidelity before depending on it more heavily.

- Keep PDF import improvements moving.
  Current known rough spots include portrait extraction, equipment import, and diagnostic coverage for unapplied choices.

## Reliability / Logic Follow-Ups

- Audit guard-clause returns across MAUI pages and decide which should show user feedback instead of silently doing nothing.

- Review shared logic calls that may still behave like stubs or partial implementations in MAUI contexts.

- `CharacterManager.GenerateCharacterName()` is still not implemented in shared logic.
  If name generation is intended to exist in the modern app, this should be addressed.

- Continue checking dialog/confirmation behavior on non-Windows MAUI targets.

## Nice-To-Have Features

- Better keyboard shortcut coverage beyond save.
- More global shell affordances: search, command palette, or quick navigation.
- Better progress and messaging around content reloads and package toggles.
- More polished cross-platform file/open/export flows.

- Add a real global compendium / quick-search surface.
  Current search exists only inside specific picker dialogs like item and selection pickers. There is no obvious user-facing global compendium browser or shell-level quick search.

- Add an app update / news / release-notes surface.
  MAUI currently exposes content/index update flows, but not a broader application updates, “what’s new”, or syndication/news experience.

- Expand theme controls.
  MAUI currently offers a single light-mode toggle. Legacy had a fuller theme/settings experience, and MAUI custom layout areas still intentionally stay dark even when light mode is enabled.

## Recommended Next Audit Order

1. Build parity walkthrough
2. Magic parity walkthrough
3. Equipment parity walkthrough
4. Session parity walkthrough
5. Sheet parity walkthrough
6. Global shell affordances (search, updates, themes)

## Recommended Implementation Order

1. Build guidance and no-op/reliability fixes
2. Magic parity improvements
3. Session persistence cleanup
4. Sheet settings parity
5. SQLite/content correctness hardening
6. Equipment depth improvements
7. Content/source management discoverability
8. Shell polish: search, updates/news, themes
