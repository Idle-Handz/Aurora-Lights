# Character load failures: investigation, September 29, 2026

Initial investigation at Lights `71c6617`, using the frozen September 26 data-17 corpus and preserved 60-character reports. At that stage, no production code, installed content, app settings, or real character saves were changed. Experimental source and outputs are under ignored `buildtmp/character-load-audit-20260929`.

## Local fixes and verification

Implementation on top of `028ebcb`, September 29. Changes are local and uncommitted; no release has been published.

- Repeated selections preserve their concrete element type, separate acquisition records, and independent rules. Daiyu's additional Intelligence is now **+5**, matching the saved choices, instead of +3.
- Rule-graph mutations invalidate the character element cache immediately. Conditional firearm grants converge instead of alternating between neither version and both versions.
- A saved direct grant can move into an eligible current default selection. Explicit saved choices take precedence, and source restrictions and requirements still apply. This restores Zeke's Cure Wounds.
- A renamed selection is recovered only when exactly one active, eligible rule on the same owner fits. Equipment choices are retried after their owners activate. Fang's Perception and Kleeck's owl restore correctly.
- Explicit proficiency aliases also cover their generated item proxies. Generated language/proficiency proxies can recover from a source abbreviation change when their saved grant identifies exactly one replacement. This repairs Spike and The Doc's additional Intimidation/Thieves' Cant items.
- A changed grant can migrate under its original parent when the old and new definitions have identical authored content except their IDs and sources. This repairs Aiden's Safe Haven and Bruce/Novu's Position of Privilege without a global ID remap.
- Separate inventory items retain their independent selection rules during normalization. Following the user's approval, all four of Remy's saved animals now remain active, with separate statistics and saved details.
- The Extras picker includes a **Companions** category. Companions added there have independent instances, including repeated creature templates; their child choices survive save/reopen. Removing an Extra does not remove a class-selected companion using the same template.
- Unresolved choice subtrees, unavailable inventory records, and unresolved summary records survive both saving and tab snapshots. The Doc's original level-9 Ranger metadata survives: save/reopen no longer turns it into another Gunslinger level. An explicit replacement choice supersedes its unresolved predecessor.
- Changing the character/content directory invalidates the preload and awaits a content reload. A failed load from a newly selected directory cannot silently enable the old directory's catalog on the next attempt.
- Tests and rehearsal tools use their own settings files. Source preference seeding can no longer overwrite the user's live settings through the shared test context.

### Results

**49 of 60 characters are now verified to load and reopen without restoration warnings, up from 30.** This combines the earlier full-corpus run (48 clean) with the targeted multiple-companion follow-up, which also repairs Remy (Strahd). Twenty previously failing characters are repaired; one previously accepted character now exposes incompatible saved grants. The full run had stable character state, serialized choices, and inventory for all **60**, and the targeted reruns remain stable. Stability alone is not a claim of full restoration for the eleven still flagged below. The full sixty were not rerun after the companion change.

The repaired cases are Aiden Mercury Shadow, Aurora, Bruce Bruce, Chrysanthemum, Daiyu Ao-shi, Fang, Gieve, Gobcha, Kleeck (Young), Kleeck, Kurumu Kurono, Luna, Michelle Character 1, Novu, Remy Morningstar, Remy Morningstar (Strahd), Rochelle Lee, Spike Spiegel, Zeke, and prepared-paladin.

| Remaining character(s) | Preserved issue requiring a content or character decision |
|---|---|
| Gobta | Missing custom background and two missing background-feature definitions; their dependent skills/languages cannot reconstruct. |
| The Doc | Missing UA20160912 Ranger multiclass and descendants, plus old duplicate-grant expectations. The original Ranger level and choices are retained; the loaded portion remains Gunslinger (5) / Bard (4) on both loads. |
| Inque-a / Inque-b | Removed Homunculus Dexterity trait. No replacement with demonstrably identical mechanics was found. |
| Kiritsugu Emiya | Current Wildspacer has no slots for the saved Gnomish/Dwarvish choices. |
| Lusten Winterblush | Current Astral Elf rules omit the saved Elf/Perception grants. |
| Rochelle Lee (Revised) | Old custom Drakewarden feature and medium companion differ from the current official feature. |
| Kurumu Kurono II | Rare Barrier Tattoo is only in the old summary, absent from both the original equipment and choice tree. |
| Testy McGee | Six Astral Drifter/Magic Initiate expectations remain in the old summary, while the saved choice tree uses Guard. |
| Fresh E | Three summary-only leftovers, an unavailable multiclass Dexterity save, and extra copies of proficiencies excluded by current multiclass rules. The valid Constitution/Charisma saves and simple-weapon proficiency are present; reduced duplicate counts are not total loss of these proficiencies. |
| Kotomine Kirei-a | The save contains both 2014 and 2024 pistol/musket grants. Current content explicitly makes those pairs mutually exclusive. Correct evaluation retains the 2014 pair and flags the incompatible extra 2024 pair. |

The user's policy is to preserve unresolved choices and migrate only proven equivalents. No missing source definitions were invented, and no ambiguous choices or stale summary entries were silently removed.

Validation: **30 focused tests passed**. The Windows MAUI target built with **0 warnings and 0 errors**. The corpus ran in four separate processes, followed by targeted reruns of Kleeck (Young), Remy (Strahd), and The Doc after the final equipment-recovery changes. Original copied character hashes all remained unchanged, and the live settings file still matches the hash recorded before the investigation. Real character saves and installed content were not written.

Evidence under `buildtmp/character-load-audit-20260929`: `final-summary.json` combines all sixty results and the three targeted reruns; `final-tests.log` and `final-app-build.log` contain validation output. Individual runs are in `fixed-shard-0` through `fixed-shard-3`, `final-kleeck`, `final-remy`, and `final-doc`. This is shared-engine corpus verification, not an execution of all sixty characters in the installed Legacy UI.

### Multiple-companion follow-up

The overview displays every active companion. The engine calculates each creature's statistics separately; bonuses from an owning class or feature follow its companion rather than being combined with unrelated creatures. The legacy singular companion record is still written for compatibility, alongside the complete collection with individual names and portraits. Extras companions also serialize their own build roots and child choices, so normal loading restores them before restoration validation.

**27 focused tests passed**, including an integration test of a class companion, three Extras (two repeated templates), independent child choices, scoped AC/HP/speed, names, save/reopen, idempotent reapplication, replacement, removal, and clearing the final companion. The Windows MAUI target built with **0 warnings and 0 errors**. Evidence: `companions-tests.log`, `companions-app-build.log`, and `multiple-remy/characters-result.json` in the same evidence directory. Original copied saves and the live settings hash remain unchanged.

Targeted controls also pass without restoration warnings: Kleeck (Young), including its owl (AC 11, HP 1, walking speed 5), and the older Kleeck save without an active companion. Their state, choices, inventory, and input hashes remain stable. Evidence: `multiple-kleeck-young/characters-result.json` and `multiple-kleeck/characters-result.json`.

Remy (Strahd) has no missing choices on either load. Character state (now including per-companion statistics), all serialized choices, and inventory remain stable through save/reopen:

| Companion | AC | Max HP | Speed |
|---|---:|---:|---:|
| Chicken | 10 | 10 | 30 |
| Cow | 10 | 15 | 30 |
| Goat | 10 | 4 | 40 |
| Ox | 10 | 15 | 30 |

## Findings that change the earlier diagnosis

The remaining failures include real losses of ability points and unstable grants. The earlier split-plan description of the ASI family as merely "spurious" was incorrect: stability after saving an already-damaged first load does not establish correctness against the original character.

### 1. Repeated ASIs lose their concrete type in Reflections

`Aurora.Logic/Services/SelectionRuleRegistrationService.cs:63` calls the non-generic `GetFresh` for an already-owned non-spell selection. `Builder.Data/ElementBaseCollection.cs:96` constructs `ElementBase`, whose `AllowMultipleElements` is false. The actual `AbilityScoreImprovement` subtype returns true. `CharacterManager.RegisterElement` then unregisters the first existing element of the same type. A second Dexterity selection can therefore remove an unrelated earlier Intelligence or Wisdom racial increase.

The preserved Daiyu trace explicitly contains "Dexterity ... not allowed for multiple elements" followed by "Unregistering Element: Intelligence". A paired run of current code and an otherwise-identical experimental handler using `GetFresh<AbilityScoreImprovement>` proved:

| Daiyu | Current handler | Typed ASI experiment |
|---|---|---|
| Missing on load/reopen | Intelligence +2 / Intelligence +2 | none / none |
| Additional Intelligence | +3 | +5 |
| Save/reopen state and choices | stable, but already wrong | stable |
| Original input | unchanged | unchanged |

Fifteen relevant characters were probed. Compared with the frozen reports, fourteen regain ability points and seven experimental cases load/reopen cleanly: Daiyu, Aurora, Gieve, Kurumu Kurono, Luna, Michelle Character 1, and prepared-paladin. Fresh E's summary-only ASI expectation is unaffected. Other warnings vary with the independent grant-cache defect, so this is not a claim that one isolated edit repairs fourteen complete characters.

Legacy's combo-box path (`SelectionRuleComboBoxViewModel.RegisterSelection`) registers the typed selection directly; it does not perform this lossy clone. Production repair should preserve runtime type, independent acquisition, rules, and provenance for repeated selections rather than weaken duplicate validation.

### 2. The element cache makes conditional grants oscillate

Gobcha's old pistol/musket definitions are present, allowed by source restrictions, and eligible against the final loaded IDs, yet absent. Current content makes each 2014 proficiency conditional on the absence of its 2024 counterpart, and vice versa.

`CharacterManager.GetElements` caches the graph. `ProgressionManager.ProcessGrantRules` mutates nested `RuleElements` without invalidating that cache between requirement evaluations. In a current-code probe, four consecutive observations alternate:

1. Neither 2014 nor 2024 pistol/musket grants.
2. Both versions.
3. Neither version.
4. Both versions.

An isolated copy of Aurora.Logic with only the cache-return shortcut removed loads/reopens Gobcha cleanly and keeps the 2014 grants stable through all four observations. The original decompiled Legacy implementation recomputed this graph instead of using that shortcut (cache added in `c2e1297`). This provides stronger evidence than simply retrying or adding a final reprocess pass; an extra pass only flips the broken result.

Repair should make the graph fresh during rule mutation, with focused convergence/performance checks. The experiment disables caching to prove causality; it is not a reviewed production performance solution. Eight of the thirty historical failures report this firearm pair, and the typed-ASI probes expose the same issue on another character as processing order changes.

### 3. Default selections used to depend on Legacy's UI

Zeke's saved Cure Wounds node is a direct grant. Current Divine Soul content supplies it through an active `Spell, Divine Magic` select rule with `default="ID_PHB_SPELL_CURE_WOUNDS"`. The Legacy expander view model applies an eligible default during initialization (`Aurora.Lights/ViewModels/SelectionRuleExpanderViewModel.cs:385`). The MAUI handler has no equivalent initialization step.

An isolated Zeke run confirms the spell is absent after ordinary loading. Applying this exact active default restores it; saving to a disposable path and reopening succeeds with Cure Wounds present. The experiment deliberately edits in-memory state between captures, so its aggregate state-equality result is not a passing unchanged-roundtrip test. Default restoration needs shared-engine behavior with source/eligibility checks and precedence for explicitly saved choices.

## Why Legacy can appear to load successfully

The older loader checked total element counts. Applying that old condition to the recorded Reflections states would accept 25 of these 30 failures: unrelated extra elements outnumber the lost ones. Daiyu has 128 loaded elements against 122 saved while still losing Intelligence +2. This is a counterfactual check on recorded states, not an execution of the original Legacy application.

The existing `legacy-xml-load` harness uses the current `CharacterFile`, MAUI selection handler, and restriction policy. It can compare XML/database content loading, but it is not a full Legacy application oracle. This investigation compared the relevant Legacy source paths and isolated changes to those behaviors; it did not run all 60 through the installed Legacy UI.

## Content changes and stale save records

Some failures are different from the three engine behaviors above. The table covers every unsuccessful September 26 case; missing counts are occurrences on the original first load, not all assumed independent defects. A missing parent can account for many descendants.

| Character | Historical missing occurrences | Evidence / disposition |
|---|---:|---|
| Aiden Mercury Shadow | 3 | Repeated ASI type loss; firearm grant oscillation. Typed probe restores Dexterity, but exposes a Safe Haven grant discrepancy; investigate after cache repair. |
| Aurora | 4 | Repeated ASI type loss removes the racial choice parent and its three children. Typed probe is clean. |
| Bruce Bruce | 1 | Vigilante now grants ID_TBOX_BACKGROUND_FEATURE_POSITIONOFPRIVILEGE; save expects ID_BACKGROUND_FEATURE_POSITIONOFPRIVILEGE. New feature is present; requires content-aware migration. |
| Chrysanthemum | 2 | Pistol/musket conditional grants: same IDs and requirement pattern as the confirmed Gobcha cache oscillation. |
| Daiyu Ao-shi | 1 | Confirmed repeated ASI type loss. Current-vs-typed paired run restores Intelligence +2; both load and reopen become clean. |
| Fang | 1 | Saved selector name is Skill Proficiency, Hunter’s Instincts; current name is Skill Proficiency (Hunter’s Instincts). Exact-name candidate filtering skips Perception. |
| Fresh E | 4 | Three expectations exist only in the saved sum, including the old feat/spell and Dexterity ASI; a saved multiclass Dexterity saving-throw grant is also absent. Typed probe has no effect. Needs stale-summary/current-rule adjudication. |
| Gieve | 1 | Repeated ASI type loss. Typed probe restores Charisma and is clean. |
| Gobcha | 2 | Confirmed stale element-cache oscillation between 2014/2024 pistol/musket grants. Uncached paired run is clean and stable across four reprocess observations. |
| Gobta | 13 | Repeated ASI type loss plus missing custom background ID_NV_TBOX_BACKGROUND_CUSTOM and descendant definitions; also firearm grants. Typed probe restores ability points but cannot supply absent content. |
| Inque-a | 1 | Saved Homunculus Dexterity racial trait ID is absent from the frozen catalog; current race rules changed. |
| Inque-b | 1 | Same removed Homunculus Dexterity racial trait as Inque-a. |
| Kiritsugu Emiya | 3 | Repeated ASI type loss plus old Gnomish/Dwarvish selections under Wildspacer. Current Wildspacer has no language selection rules. Typed probe leaves just the two languages. |
| Kleeck (Young) | 1 | Saved Familiar Selection child is named Companion; current selector is Familiar. Exact-name matching skips the owl. |
| Kleeck | 2 | Pistol/musket conditional grants, same family as Gobcha. |
| Kurumu Kurono II | 1 | Rare Barrier Tattoo exists in the saved sum but is absent from the saved equipment and choice tree. Stale summary expectation, not a missing inventory item during this load. |
| Kurumu Kurono | 1 | Repeated ASI type loss. Typed probe restores Charisma and is clean. |
| Luna | 2 | Repeated ASI type loss removes an earlier split Strength/Wisdom choice. Typed probe is clean. |
| Lusten Winterblush | 5 | Repeated ASI type loss plus old Astral Elf elf/perception grants. Typed probe restores racial ASIs; two changed-grant expectations remain. |
| Michelle Character 1 | 3 | Repeated ASI type loss removes the racial choice parent/children. Typed probe is clean. |
| Novu | 4 | Repeated ASI type loss, Vigilante feature identity change, and firearm grants. Typed probe leaves the old Position of Privilege expectation. |
| prepared-paladin | 1 | Repeated ASI type loss. Typed probe restores Wisdom and is clean. |
| Remy Morningstar (Strahd) | 3 | Four saved companion-proxy choices share a companion type limited to one active element; original sum contains only the ox. Trace shows Chicken -> Cow -> Goat -> Ox replacement, then proxy deduplication; roundtrip gains Chicken. Needs separate duplicate-proxy/selection persistence repair. |
| Remy Morningstar | 1 | Repeated ASI type loss. Typed probe restores Wisdom +2, but exposes the pistol/musket oscillation; not a clean overall result. |
| Rochelle Lee (Revised) | 4 | Repeated ASI type loss plus old custom Drakewarden feature/medium-companion subtree. Current subclass grants the official feature instead of the saved TBOX feature. Typed probe leaves those two expectations. |
| Rochelle Lee | 2 | Pistol/musket conditional grants, same family as Gobcha. |
| Spike Spiegel | 2 | Saved Additional Proficiency proxy still uses LASTER_PISTOL. Target proficiency has a LASER alias, but the generated proxy ID has no mapping, so the parent and child are not restored. |
| Testy McGee | 6 | Saved sum retains six Astral Drifter language/Magic Initiate entries although the choice tree now has Guard. Stale summary expectations from a previous build. |
| The Doc | 62 | Saved UA20160912 Ranger multiclass definition is absent. Descendant choices/grants cannot reconstruct. On roundtrip its unresolved level is reassigned: Gunslinger (5) / Bard (4) becomes Gunslinger (6) / Bard (4). Additional old proxy IDs also fail. Do not resolve this by suppressing diagnostics. |
| Zeke | 1 | Cure Wounds changed from a saved direct grant to a current select rule with a default. Legacy initializes defaults in its selection UI; MAUI registration does not. Applying that exact active default in an isolated probe restores the spell and gives a clean reopen. |

Absent definitions need restoration or an explicit, content-supported migration. Renamed choice labels need a constrained compatibility mapping. Summary-only leftovers should be diagnosed separately from active selections. Neither blindly registering everything in the old sum nor treating all missing IDs as harmless would preserve character correctness.

## Directory-change status before the fixes

At the investigation snapshot, directory changes did not reload automatically. `ApplyCustomCharactersDirectory` changed settings/directories and cleared the file-list cache; `HandleContentRootChanged` only set `_needsContentReload`. An explicit database refresh or restart was required. This is now covered by the implementation above; the original investigation used the correct frozen content root.

## Evidence and limits

- Historical input/results: `buildtmp/parity-rerun-20260926-164637-e33776`, 60 results, 30 unsuccessful.
- Paired Daiyu: `current-daiyu/characters-result.json`, `typed-daiyu/characters-result.json`.
- Typed ASI probes: `typed-summary.json` and `typed-corpus/<file>/characters-result.json`.
- Gobcha current eligibility: `details-gobcha/missing-definition-details.json`.
- Gobcha oscillation and uncached comparison: `reprocess-gobcha/reprocess-probe.json`, `uncached-gobcha/reprocess-probe.json`, and corresponding `characters-result.json`.
- Zeke: `default-zeke/default-probe.json` and `default-zeke/characters-result.json`.
- Old-count counterfactual: `count-validation-comparison.json`.
- All experiment paths above are beneath `buildtmp/character-load-audit-20260929`.
- Harness builds succeeded. Initial restore reported an offline NU1900 vulnerability-feed warning; subsequent isolated builds used cached dependencies. No broad test suite was run.
- Original copied character hashes are verified by the harness. The real settings file's SHA-256 remained unchanged during the investigation.

Suggested implementation order: preserve repeated-selection types; fix graph freshness during grant processing; restore eligible defaults in shared loading; protect unresolved multiclass/proxy state during save; then adjudicate content migrations and stale summaries. Re-run affected cases against the same corpus after each fix, and use a genuine Legacy execution path before claiming full application parity.

## Review pass, September 29, 2026

A second read of the implementation against `028ebcb`. The earlier validation ran focused tests
and a MAUI build; this pass ran the whole suite, the baseline it should be compared against, and
the Builder.Data legacy gates.

### The suite was red

`Aurora.Tests` at `028ebcb`: **711/711 pass**. With the implementation applied: **719 pass, 4
fail** — every `CompendiumServiceTests` case that loads elements. The failures were deterministic,
not the known source-restriction flakiness: the class passes in isolation and alongside
`CharacterContentRefreshTests`, and fails only in a full run.

Cause: `CharacterService.EnsureElementsLoadedAsync` now marks a preserved catalog reusable only
when `_loadedContentDirectory` matches the current one, and that field is null until a load
succeeds. `CompendiumServiceTests` depended on the previous behaviour, where a failed reload marked
itself initialised so the next call succeeded.

The guarantee worth keeping is the one the change was written for, and it does not need the null
case: `_loadedContentDirectory` is assigned only on success, so after a folder switch it still
names the folder being left and the comparison already fails. Null means this service has no record
of loading anything, so there is nothing for the current folder to contradict — and refusing a
catalog that is already in memory leaves the app unable to start rather than safer. The condition
now accepts that case, which restores the first-load behaviour while still rejecting a switch.

An earlier attempt fixed this in the test instead, by clearing the element singleton so the load
ran there. That worked, but replacing the catalog with the bundled-XML fallback leaves a single
source behind, and the tests needing a *restrictable* source then fail for whichever run they land
after — turning the intermittent catalog degradation recorded against
`DefaultSourceRestrictionFallbackTests` into something a test actively causes. Three consecutive
full runs are clean with the production condition instead.

### Defects found and fixed

- **`Character.Companion` and `Companions` raised no change notification.** `Companion` became a
  computed property, and `Aurora.Lights` binds `Character.Companion.*` throughout
  `shellwindow.xaml` and `characterinformationslider.xaml`. Those bindings resolve once, so they
  would have stayed on the empty companion for the life of the view.
- **A saved `<companions>` record that matched nothing discarded the name and portrait.** The node
  is written on every save, and its presence returned before the legacy single record could be
  read, so there was no fallback. The companion id was also the one saved id in this change read
  without `ElementIdAliases.Resolve`, so a renamed template lost its name outright. Both fixed; an
  unmatched record is now logged.
- **`CloneSelectionElement` shared `SpellcastingInformation` between copies.** Registration and
  removal are keyed on its `UniqueIdentifier` (`ProgressionManager` lines 239 and 480), so a second
  copy of a repeatable element carrying `<spellcasting>` never registered a section and removing
  either copy took the section from both. The copy now gets its own instance and its own list
  identifiers. `Activator.CreateInstance` also no longer replaces the old explanatory error with
  `MissingMethodException`.
- **Proxy alias forwarding covered one generated family of five.** `InternalElementsGenerator`
  makes feat, language, proficiency, ASI and spell proxies; only proficiency forwarded. A save
  names the proxy rather than the definition underneath, so an alias on a language, feat, ASI or
  spell definition never reached it. All five now forward through one shared helper.
- **`CompanionRuleScope.For` searched the whole element collection per element.** Replaced with
  three indexes built in one pass; first-writer-wins preserves the element the old scan found. It
  runs once per creature on every recalculation.
- **A companion-only statistics pass re-ran the character-wide inline rules.** Those write onto the
  elements themselves, so the work repeated once per extra creature. Skipped when a companion is
  named. The interleaved `abilities1` writes in the same method are left alone: the scope filter
  only drops `companion:`-prefixed rules, so the character's values are unchanged, and splitting
  the interleaved blocks in decompiled code to remove a no-op is the worse trade.
- **`UnresolvedCharacterBuild` re-added an unavailable item on every save when it had no
  `identifier`.** The loader treats that attribute as optional; the merge keyed on it alone, and a
  row matching nothing was added again each time. Falls back to id and quantity.
- **`CompanionElementParser` overwrote an authored `allow duplicate` setter.** The base parser
  already reads it, so content stating its own answer now keeps it.

### Legacy gates

Both Builder.Data gates were run, since the companion work changes that project:

- `Compare-BuilderDataBehavior.ps1`: **passes** against both restored source and the production
  oracle, 10/10 twice.
- `Compare-RestoredAssemblyApi.ps1`: **passes** — 1,362 required signatures preserved, **2
  additions permitted**, up from the one recorded on 2026-09-20. The additions are
  `PreservedAttributes` (pre-existing) and `AllowMultipleElements` (the new `CompanionElement`
  override).

### Verification

**Aurora.Tests: 733/733 pass**, up from 711 at `028ebcb` and 723 with the implementation alone.
This pass added ten cases. Seven cover the defects above and were each confirmed to fail with their
fix reverted; the other three characterise behaviour the implementation already had — that a
rebuilt companion list keeps the object holding a name just read, that the scope follows the
acquiring feature rather than the template id, and that a copy keeps its runtime type.

### Still open

- **The sixty-character corpus has not been re-run since the companion change**, which the earlier
  section already notes. That remains the highest-value outstanding check, and it now also covers
  the fixes in this section.
- **No performance measurement.** Convergence is covered by
  `MutuallyExclusiveGrantsSeeChangesImmediatelyAndConverge`; cost is not. `ProcessGrantRules`
  invalidates the element cache at twelve points and calls `GetElements()` five times, so each
  grant and ungrant rebuilds the graph.
- **`SavedGrantRecovery.FindEquivalentProxy` still matches only the proficiency and language
  markers.** Widening it to feats, ASIs and spells would extend equivalence guessing beyond what
  the corpus has shown, which the stated policy of migrating only proven equivalents argues
  against. Decide it with corpus evidence rather than by symmetry with the alias forwarding.
- **Items carrying select rules are no longer deduplicated.** Folding renewed rule identifiers into
  `GetDuplicateElementKey` is what keeps independent companion proxies alive, but it also means a
  genuine double-add of such an item will not be normalised away.
- **`ProgressionManager` reaches for `CharacterManager.Current` in twelve new places.** Harmless
  while that is a single get-only singleton, but the class holds no reference to its own owner.
- **The legacy character sheet renders one companion.** `CharacterSheetGenerator` reads
  `Character.Companion`, so a character with four creatures prints the last.

### Corpus re-run, all sixty in one pass

Evidence: `buildtmp/corpus-review-20260929-203251`, four read-only shards against one disposable copy of
`buildtmp/parity-rerun-20260926-164637-e33776/installed`. The frozen corpus was copied rather than
used in place, because `RehearsalContext` now points `AppSettingsStore` at the case root and a
settings write would otherwise land inside it. Harness built Release from the working tree, data
version 17, 580-870 s per shard, peak 1.1-1.2 GiB each.

**49 of 60 clean, 11 flagged — the same 49 and the same 11 as the combined earlier runs.** Twenty
repaired characters are all still clean; no character moved in either direction. This closes the gap
the section above recorded: the full sixty had not been re-run since the companion change, and had
only ever been reported as a 48-clean run plus targeted reruns. Sixty in one pass now agree.

- **0 errors and 0 timeouts.**
- **0 input hashes changed**, so no frozen copy was written.
- **`stateStable`, `choicesStable` and `inventoryStable` hold for all sixty**, the eleven flagged
  included. Every flagged character reports the identical missing set on both loads, which is the
  `UnresolvedCharacterBuild` contract doing its job.
- **`lostChoices` and `addedChoices` are empty for all sixty.** The Doc's round-trip level
  reassignment — Gunslinger (5) / Bard (4) becoming Gunslinger (6) / Bard (4) — does not reproduce.
- **0 missing grants** across all four shards. The thirteen load warnings are the missing-element
  reports for the eleven flagged characters and name the same content gaps as the table above.

Companions were the least-verified part of the change, having only been checked against Kleeck,
Kleeck (Young) and Remy (Strahd). The corpus holds **six** characters with an active companion, and
all six are in the clean set with identical statistics on both loads:

| Character | Companion | AC | Max HP | Speed |
|---|---|---:|---:|---:|
| Emiya Rava | Homunculus Servant | 13 | 8 | 20 |
| Gobric | Steel Defender | 15 | 25 | 40 |
| Kleeck (Young) | Owl | 11 | 1 | 5 |
| Pila Muralis | Giant Fire Beetle | 13 | 4 | 30 |
| Rochelle Lee | Drake Companion | 17 | 30 | 40 |
| Remy Morningstar (Strahd) | Chicken / Cow / Goat / Ox | 10 / 10 / 10 / 10 | 10 / 15 / 4 / 15 | 30 / 30 / 40 / 30 |

Kleeck (Young) and Remy (Strahd) match the figures reported earlier. Emiya Rava, Gobric, Pila
Muralis and Rochelle Lee are verified here for the first time, each with per-creature statistics
that survive save and reopen.

This is still shared-engine corpus verification, not an execution of the installed Legacy UI.

## Commit-readiness follow-up

The subsequent review reproduced three regressions in the hardening changes. All three are now
fixed and covered by permanent tests in `Aurora.Tests`:

- Anonymous unavailable inventory rows match by complete saved content, once per occurrence.
  Equal IDs and quantities no longer collapse distinct items, and identical rows keep their count
  across repeated saves.
- Cloned spellcasting sections get independent authored lists while borrowed extensions retain
  their granting feature's identity. Removing either of two equivalent grants removes only its own
  extension from every active clone, including a clone of a clone.
- A legacy companion record with an ID restores details only to that creature or a proven alias.
  ID-less legacy records remain compatible. Names and portraits for retained unresolved companion
  choices survive saves separately until those choices are restored or explicitly replaced.

The preceding Extras fix also uses the granted definition's duplicate permission in both the picker
and registration checks. It protects progression grants and still permits multiple companions,
repeatable pet templates, and repeatable ability increases.

Validation for this follow-up: **37 focused tests passed**, the Windows MAUI target built with
**0 warnings and 0 errors**, and `git diff --check` reported no whitespace errors. Evidence is in
`buildtmp/claude-load-review-20260929/readiness-fixes-tests.log` and
`buildtmp/claude-load-review-20260929/readiness-fixes-app-build.log`. The full-suite and sixty-character
results above predate this follow-up; neither was rerun for these targeted fixes. Changes remain
local and uncommitted.
