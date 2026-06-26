# Wishlist & Improvement Checklist

Last reviewed: 2026-06-17

An actionable to-do list organized by theme. Check items off as they land. Use
`CLIENT_FEATURE_COMPARISON.md` as the reference for current parity status and
`ROADMAP.md` for the broader strategic direction.

---

## Aurora.Web — Parity Gaps vs. Aurora: Reflections

Features that exist and work on the desktop client but have no equivalent in the
browser workflow yet.

### Build Page

- [ ] **Guided creation banner** — "Next step: ..." prompt pointing at the first
  unresolved required selection. Mirrors the App's `BuildGuidanceTarget` /
  `BuildGuidanceActionKind` pattern. Should also suggest navigating to Equipment
  once all build choices are done.
- [ ] **Class features display** — show unlocked class features (hit die, feature
  name, expandable description) inside the build group for each class level. The
  engine already returns this data through the class progression snapshot.
- [ ] **Background narrative fields** — backstory, deity, alignment, age, height,
  weight, eyes, hair, skin, gender inputs. Currently no place in the Web flow to
  set or read these. Should appear below the background build selections, matching
  the App's Background tab extras.
- [ ] **ASI / Feat selections section** — origin ability-score improvements and feat
  picks appear as a dedicated section below the ability score table in the App.
  `GetCurrentBuildStateAsync` already returns these entries in the build groups;
  they just need to be surfaced and highlighted on the Build page.
- [ ] **Custom Features (Extras)** — "Add Custom Feature" / remove flow. The engine
  exposes `AddCustomFeatureAsync` / `RemoveCustomFeatureAsync`; nothing calls them
  from the Web yet. Includes the element search picker backed by
  `SearchCustomFeaturesAsync`.
- [ ] **Multiclass during Level Up** — when a character has multiple classes, the App
  shows a picker so the player chooses which class to advance. The Web
  `LevelUpCurrentCharacterAsync` always calls `LevelUpMain` (main class only).
  Needs `GetLevelUpClassesAsync` wired up with a picker panel.

### Session / Play Mode (entire feature missing)

- [ ] **Session page** — HP tracker (current / temp / max), initiative, death saves,
  conditions, short/long rest buttons, currency, personality notes, and sidecar
  persistence (`.session.json` alongside the `.dnd5e`). This is the most-used
  play-time screen in the desktop client and has no browser equivalent at all.

### Equipment Page

- [ ] **Starting Equipment wizard** — grants class/background starting gear on new
  characters. The App has `StartingEquipmentDialog` backed by the engine; the Web
  equipment page has no entry point for it. Most relevant immediately after creating
  a new character.
- [ ] **Extract equipment packs** — replace a bundle such as Explorer's Pack with its
  component items. Low priority but part of complete equipment parity.

### Utility

- [ ] **Console / debug log page** — engine log entries bridged via `EngineLogBridge`;
  useful for diagnosing content pack issues. The App has `ConsolePage.razor`;
  the Web has no equivalent surface.
- [ ] **About / Preferences page** — version info and basic preference toggles. Trivial
  but missing entirely from the Web shell.

---

## Cross-Platform — New Features

Items that don't exist on any client yet, or exist only in a very early/partial form.

### Content & Character

- [ ] **Dragonmark spellcasting ability (EFA)** — EFA dragonmark feats describe but
  don't implement the INT/WIS/CHA choice. Fix by mirroring the Khoravar species
  pattern. Self-contained content change; no engine work required.
  *(see also: memory `efa_dragonmark_spellcasting_ability.md`)*
- [ ] **Portrait picker and bundled token bank** — character avatar in the header card.
  Design is agreed on; implementation parked. Requires a bundled bank of token art
  and a file-picker / asset-picker UI on both clients.
  *(see also: memory `project_portrait_feature.md`)*
- [ ] **Compendium expansion** — creatures table, `ImportCompanion`, SRD creature
  import, `CompendiumService`, and richer creature detail view. Partially planned.
  *(see also: memory `project_compendium_plan.md`)*

### Distribution & Trust

- [ ] **Azure Trusted Signing (Windows, App)** — eliminates SmartScreen "unknown
  publisher" warning on Velopack-installed builds.
  *(see also: memory `project_signing_roadmap.md`)*
- [ ] **Google Play Store (Android, App)** — same goal for Android installs.
  *(see also: memory `project_signing_roadmap.md`)*
- [ ] **macOS signing** — deferred; no concrete plan yet.

### Web-Specific

- [ ] **Aurora XML Studio** — a structured, form-based element authoring tool that
  complements AuroraXMLHelper (which converts PDFs to XML via deterministic
  parsing) by letting users build elements from scratch with no PDF input. The
  Aurora element schema already codified in `aurora-xml-shape.js` (element types,
  required fields, valid attribute values, grant/rule structures) would drive the
  form scaffolding. Users download the resulting XML locally and re-upload it via
  the existing Import workflow — no server-side storage required.
  **Planned development path**: prototype the form-based authoring inside
  AuroraXMLHelper first, where the schema knowledge already lives. Once proven,
  bring it into Aurora.Web and/or Aurora.Lights — either as an embedded feature
  or by linking out to a rebranded standalone Studio. The delivery mechanism is
  TBD once the tool is working end-to-end.
  Suggested element types for a first pass: Races, Classes, Subclasses,
  Backgrounds, Feats, Spells, Items, Magic Items.

- [ ] **Session persistence hint** — dismissible banner on the Web character list
  reminding users to download their `.dnd5e` before closing the tab. The ephemeral
  model still surprises new users.
- [ ] **Import drag-and-drop** — drag a `.dnd5e` / `.xml` / `.zip` file anywhere onto
  the Web shell rather than using the file picker on the Import page.
- [ ] **Shareable read-only character link** — generate a short-lived URL that lets
  others view (not edit) the active character sheet; useful for sharing with a DM
  or fellow players.

---

## Polish / UX Debt

Small but felt. Neither client specific unless noted.

- [ ] **Web: selection picker as modal overlay** — the picker currently renders inline
  below the rule list, requiring significant scroll to reach it. A slide-up panel
  or modal dialog would keep the build list in context.
- [ ] **Web: Build page pillar tabs** — tabbing by Race / Class / Background / Abilities
  matches the desktop experience and reduces vertical scroll for characters with
  many open choices. The flat group list works but it scales poorly.
- [ ] **App: coach mark for HP method toggle** — a first-time user doesn't know the
  Avg / Roll toggle exists until they've already leveled. A one-time tooltip or
  onboarding hint would help.
- [ ] **App: confirm before Level Down** — currently one click with no undo or
  confirmation dialog; easy to trigger accidentally.
- [ ] **App: spell slot rest recovery in Session** — short-rest slot recovery and pact
  magic recovery; rests currently restore HP and remove conditions but don't reset
  slot state in the Magic page toggle layer.

---

## Recently Completed

Move items here (with a date) as they land rather than deleting them, so the list
stays useful as a history of what shipped.

- [x] **Web: Level Up / Level Down** — added 2026-06-17 via
  `LevelUpCurrentCharacterAsync` / `LevelDownCurrentCharacterAsync`.
- [x] **Web: HP method toggle (Avg / Roll)** — added 2026-06-17 via
  `SetCurrentHpMethodAsync`; shown in the Build page advancement strip.
- [x] **Web: Ability Scores section** — added 2026-06-17; Manual, Roll 4d6, Roll 3d6,
  Standard Array, and Point Buy methods with the same 4-row table layout as the
  App.
- [x] **App: Rollback on external file change** — when `CharacterFileExternalChangeException`
  is thrown during a build selection save, `ReloadFromDiskAsync` now reverts the
  engine to the on-disk state before re-snapping.
- [x] **App: Session XML-patch failure snackbar** — the background currency/notes patch
  now surfaces a Warning snackbar if it fails, rather than swallowing the error.
- [x] **App: Ability Scores tab fallback message** — empty state message shown when no
  character is loaded.
- [x] **App: HP roll button loading indicators** — Roll All and per-stat 🎲 buttons now
  gate on `_saving`.
- [x] **Starting equipment (all SRD classes + backgrounds)** — completed 2026-05-18.
  *(see also: memory `project_starting_equipment.md`)*
