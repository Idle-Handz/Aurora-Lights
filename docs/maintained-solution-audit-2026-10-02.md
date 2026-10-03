# Maintained solution audit — October 2, 2026

Audit base: `c0fa268`. This pass reviews maintained application behavior and load costs,
implements contained fixes, and leaves policy/architecture changes for a decision.
The pre-existing, untracked `docs/audit-2026-10-02.md` was preserved. Its older timings
and source claims are not used as current evidence without verification.

## Scope and limits

Reviewed Aurora.App startup, updates, content, character/tab/save/build/equipment/session
services and principal Razor pages; all Aurora.DataIntegration services; shared Razor
components and their event/rendering paths; Aurora.Web import, workspace, engine/session,
cleanup and resource limits; JSON/PDF import parsing and inference; and release tooling.

No changes were made to Aurora.Lights, Builder.Core, Builder.Data, Aurora.Documents,
restored WPF presentation code, restored progression/character engine code, or vendor
packages. The two changed Aurora.Logic services, `ElementIdAliases` and
`ContentIndexUpdateService`, are maintained additions rather than restored legacy code.

This is a source, regression-test, build and isolated headless-performance audit. It does
not certify every UI workflow or platform. The installed app, user databases, content and
character saves were not changed. Performance rehearsals use the marked disposable corpus
under `buildtmp/corpus-review-20260929-203251/case` with separate result directories.

## Fixed findings

| Area | Problem and corrected behavior | Evidence |
| --- | --- | --- |
| Character files | Same-name imports/new characters could reuse a second-resolution timestamp and overwrite an existing character. Creation now reserves a free name atomically; failed imports remove only their newly created partial file. | Three simultaneous imports reproduced identical destinations before the fix; collision and interrupted-copy regression coverage. |
| Tab rehydration and save rollback | Those load paths skipped restoring equipped-slot references, unlike initial character load. Both now restore slots and recalculate statistics. | Both armor/choice refresh scenarios failed on a missing equipped armor reference before the fix. |
| Equipment UI | Dialogs and actions could use a different active tab or an old character object after awaiting context rehydration. Actions retain their original tab, check it after awaits, use its newly hydrated character, and mark that tab dirty. | Reviewed await/context paths in Equipment.razor; native interaction remains a manual follow-up. |
| Build and Magic UI | Picker option queries read singleton character state without holding CharacterContext. Magic also retained the selected detail model across character changes. | Context scope added around queries, stale-tab results discarded, selected detail reset. |
| Optional choices | Optional feats, companions, ASIs and miscellaneous choices still appeared in required guidance or unresolved badges. | Five category regressions, including a control that the same choice becomes required when its optional flag is cleared. |
| Equipment searches | Static membership sets were allocated for each examined element; dynamic slot access raised exceptions for unrelated types; culture-sensitive casing missed PRIMARY under Turkish culture. | Allocation and Turkish-culture regressions; fixed tables are reused and item slots use ordinal comparisons. |
| Aliases | Diagnostic snapshots and failed candidates could replace the live catalog's aliases. | Snapshot failure reproduced; staged async alias maps now publish only with a successful catalog. Tests also cover generated aliases, concurrent readers, failed postprocessing and delayed callbacks. |
| Folder roots | Trimming all separators changed `C:\` into drive-relative `C:`. | Resolver regression; content-directory and character-directory entry points preserve roots too. |
| Database status | Early refresh failures left SyncState idle or at its previous value. | Missing/unavailable directory regressions now report Failed. |
| Parity UI | The prepared-XML parity path performed synchronous parsing before its first await, blocking the caller's UI thread. | Parsing now runs on a worker, while retaining the existing snapshot/error contract. |
| Source update validators | File length did not detect same-size restores; uncached local timestamps were incorrectly treated as server validators. | Both cases reproduced stale content. Validators now require a matching SHA-256 fingerprint; old cache entries are revalidated once. |
| Bad downloads | HTTP 200 containing truncated XML or an invalid index could overwrite the last usable local file and be cached. | Three invalid-payload regressions. XML/index validation precedes file replacement and cache publication. |
| Protected corrections | An obsolete entry could delete a correction that automatic replacement was already forbidden to overwrite. | Protected and ordinary obsolete files tested together; only the ordinary file is removed. |
| Installed index paths | A publisher-controlled update filename was passed directly to Path.Combine for installation; removal likewise accepted a path. | Both entry points now enforce a `.index` basename. Seven traversal/type cases and a normal publisher filename are covered. |
| Update state | Manual downloads did not set the refresh-pending flag; queued Progress callbacks could overwrite the completed status. | Manual update and delayed SynchronizationContext regression coverage; progress state is applied before completion. |
| Compendium | Loaded copies of database winners were converted, normalized and sorted only to be discarded by the merge. | Skip covered IDs before projection. Tests preserve database precedence, ordinal IDs, loaded-only entries, exclusions and duplicate fallback order. |
| Spell double-click | Known-spell double-click discarded the async callback task, ending the event lifecycle before selection finished. | Deferred callback test reproduced premature completion; handler now awaits it. |
| JSON import | Optional null background/spell sections threw; a later higher-level class could replace an explicitly starting class. | Six JSON fixtures exercise null sections and both class array orders. Multiclass distribution remains a separate issue below. |
| Web import | Uploads hit the framework's default ten-file limit despite a larger configured allowance; clearing imports retained the character session. | Component tests import eleven files and verify session reset; upload control is disabled during import. |
| Release notes | `--to` selected paths from history but read their contents from today's checkout; first-release notes also used today's index. | An isolated historical-release regression fails against the old compiler and passes with tree-specific reads. |
| Release tag | Draft creation could associate newly created tags with a newer default-branch commit than the build used. | Workflow now passes the build's GITHUB_SHA as the release target. Verified CLI contract locally; no release was published during this audit. |

Primary implementation locations: `Aurora.App/Services/CharacterService.cs`,
`CharacterContext.cs`, `BuildService.BuildTabs.cs`, `EquipmentService.cs`,
`ContentService.cs`, `CompendiumService.cs`; `Aurora.DataIntegration/DbElementLoader.cs`,
`ContentDatabaseService.cs`, `ContentDirectoryResolver.cs`, `ContentDatabaseParityService.cs`;
the maintained alias/updater services; principal MAUI Razor pages; shared Magic workspace;
JSON importer; web import page; and `tools/build-release-notes.mjs`.

## Performance evidence

All measurements are local and diagnostic. Debug/headless or synthetic results must not
be presented as a promised GUI startup improvement. OS file-cache state and other machine
activity were not controlled.

| Measurement | Before | After | Interpretation |
| --- | ---: | ---: | --- |
| Content snapshot, same 21,194 elements | 11.255 s | 8.865 s | One fresh-process pair. Per-load file-date/release-date memoization removes repeated metadata work. |
| Timestamp probes for this corpus | 20,946 element rows | 1,161 distinct paths | Approximately 18 times fewer file-date probes; cached only for the current load so later refreshes see changes. |
| Warm equipment category scan, 10,000 unrelated elements | 8,480,576 allocated bytes; 31.07 ms | 880,568 bytes; 1.45 ms | Synthetic scan, excluding catalog construction; approximately 90% less allocation. |
| Loaded compendium fallback, 20,000 entries already covered by DB | 211,531,840 bytes; 307.64 ms | 672 bytes; 1.85 ms | Five warm-run medians. Excludes SQL, XML loading and overall startup. Both versions yield zero fallback entries. |

A fresh pre-fix full Debug headless load took **25.014 seconds**, retained **544.87 MiB**,
and peaked at **662.41 MiB**. The catalog grew from **21,194** elements to **101,422** during
legacy postprocessing. That expansion is a substantial remaining cost, but changing the
legacy generator is outside the requested edit scope. Lazy generation also needs to keep
saved generated IDs resolvable. The fallback projection retained **5.38 MiB** in this run;
the older draft's 11.2 MiB figure was not reproduced.

Reproduction/evidence: `buildtmp/audit-20261002-content-*`,
`buildtmp/audit-20261002-regressions-after.log`, and
`buildtmp/audit-compendium-benchmark.ps1` / `audit-20261002-compendium-benchmark.log`.
These disposable outputs are ignored build artifacts, not shipped content.

## Remaining decisions, in suggested order

### 1. Make character changes transactional

`BuildService.SaveCharacterFile` (`Aurora.App/Services/BuildService.cs:643`) performs
separate character, Extras, session-sidecar and text persistence operations. A failure
after the first write can leave partial completion. Several level/options/Extras paths
also keep mutated memory on save failure, unlike the selection path's rollback.

**Recommendation:** a dedicated save/rollback change that assembles a complete character
document before atomic replacement and explicitly handles the sidecar as part of the
operation. Define whether every failed action rolls back automatically and how recovery
is presented. This is a larger persistence contract, not a safe one-line audit fix.

### 2. Decide how to handle multiclass imports

`Aurora.PdfImport/DndBeyondJsonImporter.cs:85` keeps one ClassName but sums all class levels;
`CharacterInferenceEngine.cs:95` then assigns that total to the single class. Preserving
the correct starting class fixes ordering but does not preserve the class distribution.

**Recommendation:** warn and stop automatic conversion of multiclass characters until
the import model can represent each class, subclass and its levels. Alternative: fund
full multiclass mapping now. Silently continuing can create a materially wrong character.

### 3. Finish maintained-code load optimizations with explicit invalidation

- `Aurora.App/Components/Pages/Magic.razor:83` builds full spell details for list metadata,
  and its cache is cleared on tab changes. Separate lightweight metadata from lazy detail
  generation; key character-dependent details by character/content generation.
- `Aurora.DataIntegration/XmlContentFallbackService.cs:128` builds a full live-catalog
  dictionary per fallback request. Reuse an index owned by the published catalog, with
  refresh invalidation. No per-character timing benefit was measured in this pass.
- `Aurora.Web/Services/WebCharacterEngineService.cs:1004` reloads content and rebuilds item
  search on every character open. Reuse work by workspace/content generation while
  respecting the web prototype's single-engine ownership restriction.
- Character portraits are cached by character path without a portrait-file stamp.
  Skipping all rereads would hide external portrait changes, so that proposed shortcut
  was rejected. Add image-aware invalidation before avoiding the repeated encode/render.
- `CompendiumService.EnrichEntryAsync` prevents invalidated work from repopulating its
  cache, but can still return the old in-flight detail to an existing view. Decide whether
  open views should reload immediately on a refresh or receive generation-tagged entries.

**Recommendation:** prioritize Magic lazy details and a published-catalog lookup/index
contract. Measure real character/navigation workloads before claiming a startup gain.
Any legacy generator/evaluator redesign remains a separate, explicitly scoped task.

### 4. Clarify Extras and starting-equipment restrictions

`EquipmentService.GetCustomFeatureCategories`, `SearchCustomFeatures` and the starting
equipment category search do not apply the same source restrictions as normal inventory
pickers. `BuildService.AddCustomFeatureAsync` also permits companions without checking
their class requirements. These may be intentional freeform overrides.

**Decision:** enforce the normal restrictions, or keep Extras freeform with an explicit
override indicator? Decide separately whether starting-equipment choices may bypass
disabled sources. No selection policy was silently changed in this audit.

### 5. Define web session lifetime and hosting budget

`Aurora.Web/Services/PhaseZeroSessionCleanupService.cs:45` expires workspaces using directory
timestamps after 120 minutes. Several reads and PDF exports in `WebCharacterSessionService`
do not refresh that timestamp, so an active read-only session can lose its workspace.

**Recommendation:** track activity/connected circuits explicitly and do not expire an
active session. Choose the inactivity window and whether disconnected sessions should be
recoverable. Before public hosting, also choose an aggregate storage budget/session cap:
current protections limit individual files/archives/imports rather than total usage.
The existing singleton-engine restriction is already documented and was not removed.

### 6. Choose degraded-content and concurrent-refresh behavior

- `DbElementLoader.LoadElementAliases` (`Aurora.DataIntegration/DbElementLoader.cs:568`)
  catches SQLite exceptions and proceeds without aliases. A corrupt alias table can
  therefore leave old saved IDs unresolved. Decide between a visible degraded-mode
  warning and rejecting the candidate while keeping the previous catalog.
- `ContentDatabaseService.SyncAsync` serializes overlapping calls even though its comment
  says they coalesce. Current app UI guards prevent ordinary overlap; shared-result and
  cancellation semantics need defining before changing service behavior.

**Recommendation:** keep the prior usable catalog for an invalid current-format alias
table and expose the cause. Coalescing is lower priority than persistence and load work.

## Validation

- Baseline full Aurora.Tests suite: **748 passed, 0 failed**.
- Initial combined regression run: **27 failed as expected, 16 passed**. The separate
  public import collision case also failed before its fix. Later positive controls and
  additional concurrency/error cases were added to those regressions.
- Post-fix focused run: **91 passed, 0 failed**.
- Release-note compiler: **3 passed**; historical-tree regression independently fails
  against a disposable copy of the old compiler.
- Rehearsal build passed and both corpus snapshots loaded **21,194** elements with zero
  skipped elements.
- Final full Aurora.Tests suite: **789 passed, 0 failed, 0 skipped** (3 minutes 5 seconds).
- Windows x64 Debug app build: **succeeded, 0 warnings, 0 errors** using
  `dotnet build Aurora.App/Aurora.App.csproj -f net10.0-windows10.0.19041.0 --no-restore -m:1`.

Native MAUI two-tab equipment/picker interaction and Magic navigation need a manual smoke
pass; shared-component tests do not render the MAUI pages. Android/MacCatalyst native builds,
installer/update application and a fresh 60-character corpus roundtrip were not run. No
claim is made that the source audit exhaustively proves all platform behavior.
