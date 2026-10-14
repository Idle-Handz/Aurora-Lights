# Audit Findings Backlog

Last updated: **2026-10-14**. Audit baseline: `a5348aa`; implementation commit: `4e4b484`.

This is the implementation backlog for the October solution audit and the
October 13 correctness review: **18 implemented fixes, 1 open hosting milestone,
and 4 previously resolved P1 findings**. The original implementation is committed
in `4e4b484`. Follow-up desktop acceptance fixes and their current verification
are recorded below; no published release is certified by this document.
It includes correctness, UI, performance, and the Web hosting constraint discussed
in that review. The [roadmap](ROADMAP.md) and [wishlist](WISHLIST.md) retain broader
product work; this document does not claim to re-audit every older entry there.

## Evidence and status

- **Reproduced earlier:** the October audit executed an isolated reproduction.
  The described production path remains present at consolidation. Those
  reproductions were not rerun for this documentation change.
- **Code trace:** source inspection supports the finding, but the complete user
  interaction or failure scenario still needs runtime verification.
- **Performance candidate:** inspected work appears avoidable; no measured
  user-facing improvement is claimed. Benchmark before expanding a change.
- **Documented constraint:** an intentional limitation requiring a separate
  implementation or product decision, rather than a newly reproduced defect.
- **Resolved:** the correction and its regression coverage are committed.
  This does not certify a particular installed binary or published release.
- **Local fix:** implementation is present in the working tree, with the focused
  evidence recorded below. It is not committed or released; acceptance of local
  binaries is recorded separately.

Suggested order is independent of evidence confidence: **Next** covers rules
correctness and potential loss of edits; **Following** covers workflow and
responsiveness; **Later** requires measurement or a hosting milestone. Verify
code-trace findings before treating their full user-visible impact as reproduced.

| ID | Item | Order | Evidence | Status |
| --- | --- | --- | --- | --- |
| [COR-01](#cor-01-negative-statistic-contributions-are-discarded) | Negative statistic contributions | Next | Primitive and calculator checks pass | Resolved |
| [COR-02](#cor-02-magical-effects-activate-without-required-attunement) | Magical effects without attunement | Next | Equipment lifecycle and native AC checks pass | Resolved; local hand-transition follow-up |
| [COR-03](#cor-03-equipment-restoration-changes-saved-attunement) | Saved attunement changes on restoration | Next | Save/load, native tab switching and disk-conflict reload pass | Resolved; local hand-transition follow-up |
| [COR-04](#cor-04-web-spell-slot-usage-is-off-by-one) | Web spell-slot usage | Next | Regression checks pass | Resolved |
| [COR-05](#cor-05-local-session-save-completion-and-dirty-state) | Local save completion and dirty state | Next | Pending/failure/retry checks pass | Resolved |
| [COR-06](#cor-06-multiclass-imports-collapse-class-levels) | Multiclass import fidelity | Next | Import/save/reload checks pass | Resolved |
| [COR-07](#cor-07-import-preview-can-overwrite-an-existing-portrait) | Portrait overwrite during import preview | Next | File lifecycle and native ready/in-flight cancellation pass | Resolved |
| [COR-08](#cor-08-standard-array-assignments-cannot-be-rearranged) | Standard-array reassignment | Following | Helper and Web browser checks pass | Resolved |
| [COR-09](#cor-09-point-buy-conversion-bypasses-the-normal-edit-workflow) | Point Buy conversion persistence | Following | Native autosave on/off and disk values pass | Resolved |
| [COR-10](#cor-10-ctrls-can-save-stale-narrative-text) | Narrative text and Ctrl+S | Next | Native Backstory/Age with autosave on/off pass | Resolved; local save-coalescing follow-up |
| [COR-11](#cor-11-web-magic-builds-only-the-first-casting-profile) | Multiple Web casting profiles | Following | Profile/persistence checks pass | Resolved |
| [UX-01](#ux-01-web-character-selection-clears-search) | Web character search resets | Following | Web browser check passes | Resolved |
| [UX-02](#ux-02-shared-magic-controls-lack-accessible-names-and-state) | Shared Magic accessibility | Following | NVDA labels/state, autosave focus and forward/reverse traversal pass; audible speech confirmed | Resolved; local follow-ups verified |
| [PERF-01](#perf-01-cloud-requests-hold-the-global-character-lock) | Cloud requests block other characters | Following | Paused-provider checks pass | Resolved |
| [PERF-02](#perf-02-dirty-state-reads-hash-whole-files) | File hashing during dirty-state reads | Later | Read-count and invalidation checks pass | Resolved |
| [PERF-03](#perf-03-note-and-session-writes-are-not-coalesced) | Repeated note and session writes | Following | Draft and XML write checks pass | Resolved |
| [PERF-04](#perf-04-inventory-loading-recalculates-weight-per-row) | Repeated inventory weight calculations | Later | Synthetic bulk measurements/checks pass | Resolved |
| [PERF-05](#perf-05-repeated-statistic-searches-and-compendium-projections) | Repeated lookups and projections | Later | Lookup/projection measurements/checks pass | Resolved |
| [WEB-01](#web-01-concurrent-hosting-requires-session-local-engine-state) | Concurrent Web character sessions | Before concurrent public hosting | Documented constraint | Open |

## Implementation verification: 2026-10-13

- **159 focused .NET cases passed across the final runs:** 149 passed together;
  ten prior P1 identity cases passed after their test fixture installed its
  selection/spell handlers before resetting the global character. That fixture
  change removes an order dependency; it does not change application behavior.
  The set includes the new backlog regressions plus existing import, load,
  snapshot, cloud recovery and file-write checks. No full corpus sweep was run.
- **Three Chromium desktop browser checks passed:** retained character search,
  complete standard-array swap across navigation, and note typing/explicit
  save/blur/navigation persistence. Tests clear their isolated temporary workspaces
  to release the current single-owner Web engine.
- **Windows App build passed.** Existing nullable/analyzer warnings and NU1900
  vulnerability-feed connectivity warnings remain separate from test outcomes.
- **Manual acceptance still needed:** native import cancellation, App Point Buy
  with autosave on/off, immediate Ctrl+S in narrative inputs, equipment tab
  rehydration/rollback interaction, and screen-reader keyboard interaction.
- **Publication:** working-tree changes only; no commit, push, installation or
  release was performed. The initial README/roadmap/wishlist edits were preserved.

## Commit-readiness verification: 2026-10-14

- A scoped diff review found and corrected two remaining issues within this
  batch: exported class-name suffixes breaking subclass/spell ownership, and
  an earlier local narrative save clearing the dirty flag for newer typing.
- **103 focused .NET cases passed together**, with zero failures or skips,
  after those corrections. The two new archived/legacy import cases failed
  before the correction and passed afterward, including subclass save/reload
  and preparation isolation between classes.
- **Windows App build passed** with zero errors. Existing nullable and NU1900
  vulnerability-feed warnings remain. `git diff --check` is clean.
- The three Chromium checks above remain prior evidence; they were not rerun
  for these App/import changes. Native and screen-reader acceptance checks
  remain open. WEB-01 remains a separate hosting milestone.
- That implementation was subsequently committed as `4e4b484`; the checks above
  remain evidence from the commit review, not reruns during desktop acceptance.

## Desktop release acceptance: 2026-10-14

Scope: Windows x64 desktop. WEB-01 remains excluded. Only uniquely named synthetic
characters were edited during native acceptance.

- **Import cancellation passed in the native app:** canceling a completed JSON
  preview removed its staged portrait. Canceling during a controlled 12-second
  portrait download left the dialog closed after the response completed, removed
  the late staged file, and created no character. The Windows picker was exercised
  through its filename keyboard shortcut; no file-picker limitation remains.
- **Equipment baseline checks passed:** armor/weapon/shield references and saved
  attunement survived tab switches. Unattuning the ring changed AC from 19 to 18.
  An external disk edit produced the reload warning and discarded the attempted
  conflicting edit instead of overwriting the file.
- **Point Buy with autosave disabled passed:** 18/7/10/10/10/10 became
  15/8/10/10/10/10, with 17 points spent; disk stayed unchanged until Save, then
  contained the clamped values. With autosave enabled in the packaged candidate,
  a separate fixture wrote the same clamped values without an explicit Save.
- **Native Magic keyboard checks passed:** Space changed the first slot from used
  to available; Enter expanded a collapsed spell-level group; Tab/Space unprepared
  Cure Wounds. Windows accessibility exposed slot position/usage and preparation
  names. This is UI Automation and keyboard evidence, not a listening test with
  Narrator or NVDA.
- **Follow-up fixes found during acceptance:** focused Ctrl+S could leave a
  false dirty state with autosave disabled or show a competing-save error with it
  enabled; a two-handed weapon could leave a displaced shield active; spell-filter
  selects had no accessible names. Save coalescing and version guards now preserve
  real failures/newer edits while avoiding duplicate-save errors. Native Backstory
  and Age Ctrl+S checks pass with autosave on and off, including disk contents and
  the clean indicator. Filter names are confirmed in native accessibility output.
  Equipment now removes displaced hand effects, corrects conflicting older saves,
  prefers the shield's explicit secondary slot, and preserves attunement when
  moving the same item or changing versatile grip.
- **Final packaged equipment retest passed:** the inventory shield Equip button
  selected Secondary Hand, unequipped the two-handed greatsword, and produced
  AC 18. Equipping the greatsword again unequipped the shield and restored AC 16.
  Ctrl+S completed successfully and the tab closed without an unsaved-change prompt.
- **71 distinct focused .NET cases passed across the final relevant runs:**
  64 passed together; after the final hand-placement changes, all 35 equipment
  cases passed (28 repeated, seven additional). There were zero failures or skips.
  Coverage includes hand conflicts/attunement, duplicate and newer save requests,
  narrative fields, Magic accessible names, staged portraits, session completion
  and character identity. A final scoped save-coalescing review found no blockers.
- **Windows packaging correction:** the release CLI now matches the app's
  Velopack 1.0.1 dependency and declares WebView2 for both Windows installers.
  The earlier local x64 Release publish and Setup/portable/full-package build passed
  after the initial acceptance fixes. Archive CRCs, payload contents and release-manifest
  hashes passed verification. The extracted portable launcher started the app,
  and its final equipment retest passed. Artifacts are unsigned and remain in
  `artifacts/desktop-release-check-2026-10-14/bin/win-x64-check/final2-packages/`.
  Existing compiler warnings remain; the publish completed without errors.
- **Prior candidate Setup acceptance passed in Windows Sandbox:** after enabling
  Sandbox and completing the required host restart, the unmodified candidate was
  installed in a fresh disposable guest. First launch, synthetic character/session
  save, exit and reopen passed. No existing host installation or personal character
  files were replaced. WebView2 was already installed in the guest, so this does
  not establish missing-runtime bootstrap behavior.
- **Both original diagnostic update paths passed:** isolated copies used local
  release metadata and a local Velopack feed for `0.0.2-localcheck.20261014` to
  `0.0.3-localcheck.20261014`. Immediate update installed B and restarted into it.
  Deferred update left A running with the same process ID for **86.2 seconds**;
  normal close applied B without restarting, and shortcut relaunch opened B.
  Synthetic character XML and session-sidecar hashes were preserved in both paths.
  The updater's process-wait access-denied warning was followed by successful
  lifecycle hooks, package application and restart in the immediate case; it was
  not an observed update failure. These checks exercise the app's update lifecycle
  with diagnostic feed seams, not production GitHub discovery or downloads.
- **Immediate-update follow-up:** installing and restarting now requires all
  character tabs to be closed. The guard checks before download and again in the
  same synchronous UI callback as restart; leaving Settings cancels the restart.
  Existing tab-close Save/Discard/Cancel handling is reused. All **14 focused
  `AppUpdateRestartTests` cases passed**, including pending session writes, tabs
  opened during a paused download, navigation, queued UI work and clean-tabs retry.
  The unmodified **final3 Windows x64 Release publish and package build passed**
  with this safeguard.
- **Final3 Setup acceptance passed:** the unmodified final3 installer completed
  a fresh Sandbox installation, usable first launch, normal exit and shortcut
  relaunch. The install hook completed in 1,888.7178 ms. Evidence is under
  `artifacts/desktop-release-check-2026-10-14/bin/sandbox/final3-results/`.
- **Native immediate-update guard and retry passed:** with autosave disabled,
  the synthetic equipment-note draft was `y` while disk still contained `x`.
  Immediate update was blocked and A's process (8152) stayed alive. Canceling
  tab close retained the draft; choosing Save wrote `y` and closed the tab.
  Retrying with all tabs closed updated to B (`0.0.3-localcheck.20261014`) and
  automatically relaunched. Reopening the character displayed note `y`, Temp HP 1
  and Inspiration enabled; saved character and session-sidecar hashes were
  unchanged by the update. Lifecycle hooks succeeded despite the same nonfatal
  process-wait access-denied warning. This used the isolated diagnostic feed;
  evidence is under
  `artifacts/desktop-release-check-2026-10-14/bin/sandbox/diagnostic-restart-guard/results-guard/`.
- **Windows native-close safeguard implemented:** closing the main window now
  uses the existing character Save/Discard/Cancel workflow. Cancel/save failure
  retains the window; full/session writes and engine release finish before close.
  Repeated tab/window requests share one prompt, newer edits are rechecked, and
  pending engine release also blocks immediate restart after a tab disappears.
  Loading-tab cancellation is preserved. **38 final focused cases passed** (17
  window-close, 14 immediate-update and 7 session-save cases), with independent
  review complete. Final5 Release publish/package, fresh installation, clean
  native exit and shortcut relaunch passed. Native Cancel preserved the unsaved
  draft and the same A process **111.3 seconds** after update preparation. Save
  persisted the draft and applied B without restart; shortcut relaunch retained
  the note, Temp HP and Inspiration. Discard of a later draft exited without
  changing saved XML/sidecar hashes. Both update callbacks succeeded. Evidence:
  `artifacts/desktop-release-check-2026-10-14/bin/sandbox/diagnostic-window-close-final/results-close/`.
  All disposable guests were discarded after export; the host installation was
  not changed.
- **Remaining release gates and limits:** missing-WebView2 bootstrap passed in
  the clean Windows 10 LTSC guest described below. Actual NVDA speech and autosave focus
  now have native evidence and the user confirmed understandable audible output;
  two final forward/reverse-Tab retests passed. Production GitHub release discovery and
  package download, signing/SmartScreen behavior, delta updates and Windows ARM64
  remain unverified. See the
  [installer acceptance checklist](desktop-installer-acceptance.md) for artifact
  hashes, completed lifecycle evidence and the remaining steps. No published
  release is certified by these local checks.
- **NVDA follow-up passed:** portable NVDA 2026.2 generated the expected
  search/filter names and values across all three Magic views, slot position and
  used/available state, group expansion state, and preparation/limit-disabled
  announcements. This is actual NVDA speech-output evidence; the user confirmed
  audible and understandable playback. Native testing found focus loss after
  preparation autosave and indistinguishable labels for same-named spells from
  different sources. Source-aware labels, stable row keys and pending-change
  guards passed ten focused component cases. Final6 still lost focus because the
  page-wide save lock uses `inert`. The final7 layout helper preserves that lock
  and passed 17 real Chromium cases. Final7 native NVDA reports confirm focus on
  the correct checkbox after both unprepare and prepare saves, and Tab reaches
  the distinct 2024 source label. Two complete forward/reverse traversals passed
  in the fresh Windows 10 LTSC guest, including after keyboard preparation and
  unpreparation saves, without bypassing NVDA. Generated speech and explicit
  focus reports confirm the expected source-specific controls. That basic VM
  connection has no audio endpoint; human listening remains the earlier Sandbox
  confirmation. Transient IA2/UIA and watchdog warnings are retained in the logs.
  Release publish/package and integrity checks passed. Raw logs and failed and
  successful retest evidence are preserved under `bin/sandbox/runtime-accessibility`
  in the acceptance artifact directory; final native evidence and synthetic saves
  are also in `bin/missing-runtime-vm/results-final`.
- **Missing-WebView2 bootstrap passed:** Windows 10 Enterprise LTSC 2021
  evaluation build 19044 x64 was installed offline from Microsoft's official,
  hash-verified ISO in the dedicated Hyper-V guest. The default loader returned
  `0x80070002` with null version, with no runtime registrations/files or loader
  overrides, both offline and 172 ms before the hash-verified final7 Setup launch.
  The native prerequisite prompt, download/install and usable Aurora first launch
  passed. Velopack's explicit `/install` and the user updater's interactive
  `otherinstallcmd` handoff correlate to the same `--user-level` archive and
  successful result 0 before Velopack resumed. The post-install API returned
  `S_OK`, version `155.0.4283.45`. A concurrent background machine EdgeUpdate
  completed later; the final HKLM runtime is not solely attributed to final7,
  while its successful per-user bootstrap is independently logged. Evidence:
  `bin/missing-runtime-vm/results-bootstrap` and `results-bootstrap-extra` under
  the acceptance artifact directory. Aurora/NVDA exited normally, final evidence
  was exported, and the guest was shut down with networking disconnected. With
  explicit user approval, its VM registration and generated disk/configuration
  were removed, reclaiming 12.57 GiB. Installers, the Windows ISO and all exported
  evidence remain preserved; `bin/missing-runtime-vm/disposed-vm-result.json`
  records cleanup.
  No host WebView2 installation was modified. See the
  [exact acceptance evidence](desktop-installer-acceptance.md#missing-webview2-runtime).
- **Cleanup:** all five synthetic library characters and four session sidecars
  were archived under the ignored acceptance artifacts and removed from the
  library, returning it to its original 60 characters. The app was closed and
  autosave was restored to its original disabled setting. All acceptance Sandbox
  guests were subsequently disposed; `wsb list` confirmed no remaining guests.
- **Publication:** follow-up changes are local; no commit, push, tag or release
  publication has been performed. Installations and upgrades above were confined
  to disposable Sandbox and Hyper-V guests; no existing host installation was replaced.

## Correctness

### COR-01: Negative statistic contributions are discarded

- **2026-10-13 local implementation:** `Merge` preserves negative and zero-sum
  groups and their source keys. `StatisticMergeRegressionTests` covers primitive
  accumulation and the production AC, spell-attack and spell-save calculation paths.

- **Impact/evidence:** `StatisticValuesGroup.Merge` rejects any incoming group
  whose sum is zero or negative. The earlier primitive reproduction merged AC 10
  with a -2 contribution and obtained 10 instead of 8. This concerns contributions
  routed through this merge, not every negative modifier in the engine.
- **Location:** [StatisticValuesGroup.cs](../Aurora.Logic/Services/Calculator/StatisticValuesGroup.cs),
  `Merge`; [StatisticsHandler2.cs](../Aurora.Logic/Services/Calculator/StatisticsHandler2.cs)
  calls it for AC components and spell attack/save contributions.
- **Correction:** retain valid negative and zero-sum groups without changing the
  existing source-key accumulation rules.
- **Done when:** focused checks cover positive, negative, mixed, and zero-sum
  contributions; a representative AC penalty and spell attack/save penalty reach
  the resulting character statistics correctly.

### COR-02: Magical effects activate without required attunement

- **2026-10-13 local implementation:** base and template registration independently
  obey each component's requirement. Unattuning removes only gated effects;
  unrestricted equipped effects remain. `EquipmentAttunementRegressionTests`
  covers all requirement combinations and standalone items through the lifecycle.

- **Impact/evidence:** a base weapon with an attunement-required magic template
  can register the template's effects while `IsAttuned` is false. The equip branch
  checks `Item.RequiresAttunement` before registering `AdornerItem`.
- **Location:** [RefactoredEquipmentItem.cs](../Aurora.Logic/ViewModels/Shell/Items/RefactoredEquipmentItem.cs),
  `Activate` / `RegisterAdorner`.
- **Correction:** evaluate base-item and magic-template activation requirements
  separately; equipping the ordinary base must not grant gated template effects.
- **Done when:** equip, attune, unattune, unequip, and reload preserve the correct
  effects for both attunement-required and unrestricted templates. An eligible
  but unattuned item does not grant its attunement-gated effects.

### COR-03: Equipment restoration changes saved attunement

- **2026-10-13 local implementation:** equip helpers accept a restoration override;
  load restoration explicitly passes saved `IsAttuned` and preserves two-handed
  and versatile slot references. `EquipmentAttunementRegressionTests` saves,
  loads and repeatedly restores armor/primary/secondary/two-handed/versatile items
  with both attunement states. Native tab-switch/rollback interaction is not certified
  by those service-level fixtures.

- **Impact/evidence:** restoring an equipped, explicitly unattuned item can make
  it attuned. Slot restoration calls normal equip helpers, which pass
  `IsAttunable` as the requested attunement state instead of preserving `IsAttuned`.
- **Location:** [CharacterLoadCompatibilityService.cs](../Aurora.Logic/Services/CharacterLoadCompatibilityService.cs),
  `RestoreEquippedSlots`; [CharacterInventory.cs](../Aurora.Logic/Models/Equipment/CharacterInventory.cs),
  `EquipArmor`, `EquipPrimary`, and `EquipSecondary`.
- **Correction:** rebuild slot references while retaining saved equipment and
  attunement choices. Keep restoration distinct from a deliberate equip action.
- **Done when:** initial load, tab rehydration, and rollback preserve attuned and
  unattuned items in armor, primary, secondary, and two-handed slots, with matching
  effect registration and attuned-item counts. Coordinate coverage with COR-02.

### COR-04: Web spell-slot usage is off by one

- **2026-10-13 local implementation:** Web now consumes one-based slot numbers
  consistently with App and clamps restored/saved counts. Ten slot regression
  cases in `WebMagicStatePersistenceTests` cover first/last/clearing, invalid
  buttons, zero capacity, and invalid persisted counts through save/reload.

- **Impact/evidence:** the shared UI emits one-based slot numbers; Web treats
  them as zero-based. The earlier reproduction recorded two used slots after
  clicking the first unused slot and persisted five after clicking the fourth.
- **Location:** [CharacterMagicWorkspace.razor](../Aurora.Components/Shared/CharacterMagicWorkspace.razor),
  slot-button callback; [WebCharacterEngineService.cs](../Aurora.Web/Services/WebCharacterEngineService.cs),
  `ToggleCurrentMagicSlotAsync`.
- **Correction:** use the shared control's index convention consistently with
  App, and constrain persisted usage to the available capacity.
- **Done when:** first/last slot use and clearing, zero-slot levels, and restored
  out-of-range usage behave consistently; usage remains between zero and capacity
  after persistence and reload.

### COR-05: Local session save completion and dirty state

- **2026-10-13 local implementation:** a versioned pending-write state belongs to
  the tab, including unblurred note drafts. Explicit Save awaits sidecar and any
  changed XML fields; failures keep the tab dirty and preserve drafts. External
  changes are reported without silently replacing the draft with reloaded values.
- **Verified:** seven `SessionSaveCompletionTests` cover a queued write, failed
  sidecar, locked XML replacement after sidecar success, retry, external changes,
  unrelated dirty edits, and superseded queued patches. The lock failure is an
  I/O failure test, not an ACL-denial or native close-dialog exercise.

- **Impact/evidence:** `PersistSessionChanges` returns the sidecar save result
  while currency/notes XML writes continue in an unawaited background task.
  `SaveSession` can announce success before those writes finish. Only cloud tabs
  are marked dirty by this path; a later patch failure warns without establishing
  the same pending-edit guard for a local tab. Full failure/close behavior needs
  reproduction before claiming end-to-end edit loss.
- **Location:** [Session.razor](../Aurora.App/Components/Pages/Session.razor),
  `SaveSession`, `PersistSessionChanges`, and `HandleSessionPatchFailureAsync`.
- **Correction:** track all required writes, retain local pending state until
  they succeed, and make success messages and close handling reflect completion.
  Preserve existing external-file-change protection and error feedback.
- **Done when:** delayed/failed XML writes after a successful sidecar save cannot
  produce a false all-saved state; retry and tab close preserve pending currency
  and notes. Exercise permission errors and external modifications separately.
- **Existing context:** [transactional character changes](maintained-solution-audit-2026-10-02.md#1-make-character-changes-transactional)
  describes the broader multi-write/rollback follow-up. The completed
  [failure snackbar](WISHLIST.md#recently-completed) addresses visibility, not this
  completion/dirty-state contract. Avoid turning the narrow fix into an unplanned
  persistence redesign.

### COR-06: Multiclass imports collapse class levels

- **2026-10-13 decision and local implementation:** the user chose full
  per-class/subclass JSON import support. Parsed class records retain levels,
  subclass, and explicit starting class; inference resolves main Class and
  secondary Multiclass selections separately. Creation uses each progression's
  level-up path and scopes subclasses/spells to their class. Unsupported/missing
  content fails before replacing the destination file. JSON class-spell IDs retain
  their owning class for preparation.
- **Verified:** `MulticlassImportTests` imports and reloads both source orders for
  Fighter 1 / Wizard 5 and Wizard 2 / Fighter 4, preserving the starting class and
  subclass ownership. This exposed and fixed premature subclass replay in
  `CharacterFile`: inactive class rules now defer until their progression exists
  at the required level. An unresolved-subclass case preserves the old character
  and the retryable portrait preview.
- **2026-10-14 commit review:** subclass and spell ownership now follows the
  resolved Class/Multiclass ID while retaining the exported display name. This
  corrects imports such as `Wizard (archived)` and `Wizard (legacy)`, whose class
  resolved successfully but whose subclass previously failed the ownership check.
- **Format boundary:** JSON exports provide class totals, not the complete order
  of levels or every level-specific choice; the import review warns to review
  those choices. These fixtures do not establish multiclass extraction from OCR
  PDFs or certify a particular third-party export/content combination.

- **Impact/evidence:** the JSON parser retains one class name and sums all class
  levels; inference and import then build that main class to the total level.
  A starting Fighter 1 / Wizard 5 can therefore become Fighter 6. The complete
  import interaction remains a code-trace finding.
- **Location:** [DndBeyondJsonImporter.cs](../Aurora.PdfImport/DndBeyondJsonImporter.cs),
  class parsing; [CharacterInferenceEngine.cs](../Aurora.PdfImport/CharacterInferenceEngine.cs);
  [PdfImportService.cs](../Aurora.App/Services/PdfImportService.cs), main-class level-up loop.
- **Correction:** explicitly warn and stop unsupported multiclass conversion, or
  implement per-class/subclass/level mapping. Choose and document the supported
  import contract before treating a lossy result as successful.
- **Done when:** fixtures with both class orders and an explicit starting class
  either retain the distribution through save/reload or clearly reject automatic
  conversion. Update tests that currently encode the flattened representation.
- **Existing context:** [October 2 multiclass finding](maintained-solution-audit-2026-10-02.md#2-decide-how-to-handle-multiclass-imports).

### COR-07: Import preview can overwrite an existing portrait

- **2026-10-13 local implementation:** PDF and JSON previews share an
  attempt-owned staging object; source copies and final portrait names are
  unique. Confirmation writes a temporary character before publishing it.
  Cancellation/disposal removes only that attempt's files; failed confirmation
  removes its provisional portrait and leaves the preview retryable.
- **Verified:** `StagedImportPortraitTests` covers same-name PNG/JPEG attempts,
  cancellation, retry, and concurrent commits. `MulticlassImportTests` exercises
  an actual failed character import with a staged portrait and existing output.
  Native file-picker/dialog cancellation remains a manual acceptance check.

- **Impact/evidence:** portrait extraction/download occurs during import
  analysis. Storage uses the sanitized character name plus extension and
  `WriteAllBytesAsync` replaces an existing destination. A same-name,
  same-extension preview can replace a portrait before confirmation.
- **Location:** [PdfImportService.cs](../Aurora.App/Services/PdfImportService.cs),
  portrait download/extraction and `SavePortraitBytesAsync`.
- **Correction:** stage imports at unique paths and commit the selected portrait
  only with the confirmed import; clean up only artifacts owned by that attempt.
- **Done when:** same-name PDF/JSON previews, cancellation, failed import, and
  concurrent attempts leave all pre-existing portrait bytes unchanged.
- **Historical distinction:** the [May portrait collision fix](audit-2026-05-19.md#3-portrait-files-use-the-pickers-original-filename--collisions-silently-overwrite)
  covers the manual picker in `CharacterDetail.razor`; it does not close this
  separate import-service finding.

### COR-08: Standard-array assignments cannot be rearranged

- **2026-10-13 local implementation:** both clients expose all six choices and swap
  occupied assignments through `StandardArrayAssignment`. Unassigned rows retain
  their existing score until assigned. Three helper regressions and the Web browser
  swap/navigation regression cover the behavior; native App interaction remains a
  manual check.

- **Impact/evidence:** once all six values are assigned, each dropdown excludes
  values used elsewhere and removes its empty option. Each dropdown is left with
  its current value, blocking reassignment within Standard Array.
- **Location:** [App Build.razor](../Aurora.App/Components/Pages/Build.razor) and
  [Web Build.razor](../Aurora.Web/Components/Pages/Build.razor), array controls and
  available-value filtering. Both clients were identified by the October audit.
- **Correction:** retain a clear/reassign action or implement an explicit swap;
  define how an unassigned row is represented in the underlying scores.
- **Done when:** a completed array can be rearranged using the visible controls,
  without duplicate assignments or switching methods, and the result persists in
  both clients. This is separate from the older default-to-Manual decision.

### COR-09: Point Buy conversion bypasses the normal edit workflow

- **2026-10-13 local implementation:** when conversion clamps a score, App awaits
  `ApplyAbilityChangesAsync`, using the existing recalculation, snapshot, dirty and
  autosave workflow. The Windows build passes. Manual acceptance still needs both
  autosave settings and save/reload with out-of-range starting scores.

- **Impact/evidence:** selecting Point Buy clamps base scores to 8-15 directly
  and recalculates points, without the normal `ApplyAbilityChangesAsync` path.
  Recalculation, snapshots, dirty state, and persistence can diverge.
- **Location:** [App Build.razor](../Aurora.App/Components/Pages/Build.razor),
  `SetAbilityMethod` and `ApplyAbilityChangesAsync`.
- **Correction:** apply score conversion through the normal ability-edit
  contract, with explicit behavior when existing scores must change.
- **Done when:** switching from out-of-range manual/rolled scores produces
  consistent displayed and engine values, dirty state, and save/reload results
  with build autosave both enabled and disabled.

### COR-10: Ctrl+S can save stale narrative text

- **2026-10-13 local implementation:** narrative inputs publish drafts and mark
  dirty on input; a separate change/blur callback performs configured autosave.
  `CharacterNarrativeFieldTests` verifies single/multiline final drafts reach the
  model without per-keystroke commits. Windows compilation passes; actual MAUI
  Ctrl+S with autosave on/off remains a manual acceptance check.
- **2026-10-14 commit review:** Build's narrative autosave and manual save capture
  the tab's edit version before awaiting the save and only clear dirty state if
  that version is unchanged. New typing during a local save remains pending.

- **Impact/evidence:** narrative inputs publish changes through `onchange`,
  normally after leaving the field. The keyboard handler invokes save directly.
  Typing and pressing Ctrl+S without leaving the field may save the prior value
  while reporting success; native interaction still needs reproduction.
- **Location:** [NarrativeField.razor](../Aurora.App/Components/Shared/NarrativeField.razor)
  and [shortcuts.js](../Aurora.App/wwwroot/shortcuts.js), `OnCtrlS` dispatch.
- **Correction:** keep draft values current on input or explicitly flush focused
  edits before saving; avoid introducing a full disk save on each keystroke.
- **Done when:** typing into single-line and multiline narrative fields followed
  immediately by Ctrl+S persists the final text without requiring blur. Verify
  with autosave disabled as well as enabled. The resolved snapshot-flush fix
  below does not establish that the focused DOM value reached the snapshot.

### COR-11: Web Magic builds only the first casting profile

- **2026-10-13 local implementation:** each independent profile gets its own
  stable source/name identity, ability/DC/attack, preparation limit, acquired
  spells, and targeted extensions. Preparation reads/writes and runtime handler
  state are scoped to that identity; feature grants remain visible separately.
- **Verified:** `WebMagicProfileTests` covers two profiles with distinct abilities,
  limits and spell lists, targeted extensions, a single profile and an unattached
  extension. `WebMagicStatePersistenceTests` saves/reloads/copies the same spell
  with different preparation choices in two profiles and handles source-less
  legacy state only when the name is unambiguous.

- **Impact/evidence:** `BuildMagicModel` selects the first non-extension
  spellcasting entry and constructs one section. Independent additional profiles
  do not receive their own preparation limits, ability, attack, or DC presentation.
- **Location:** [WebCharacterEngineService.cs](../Aurora.Web/Services/WebCharacterEngineService.cs),
  `BuildMagicModel` and per-profile persistence.
- **Correction:** build and persist each independent profile, applying extensions
  to their intended recipients without blending unrelated casting rules.
- **Done when:** two-profile fixtures with different casting abilities and
  preparation rules retain separate sections and selections through reload;
  single-profile and extension-only cases continue to work.
- **Existing context:** [multiple spellcasting sources](CLIENT_FEATURE_COMPARISON.md#spellcasting)
  are described broadly as partial on Web; this entry records the concrete gap.

## UI and accessibility

### UX-01: Web character selection clears search

- **2026-10-13 local implementation and verification:** Web binds the browser's
  search state. `audit-ui.spec.ts` passes selection, retained filtering, no-match
  filtering, and explicit clearing in Chromium.

- **Impact/evidence:** the Web page does not bind the shared character browser's
  search state, so selection and parent rerender can reset the filter.
- **Location:** [Web Characters.razor](../Aurora.Web/Components/Pages/Characters.razor)
  and [CharacterBrowser.razor](../Aurora.Components/Shared/CharacterBrowser.razor).
- **Correction/done when:** bind and retain the search state as App does; typing a
  query, selecting a result, and rerendering must keep the query and filtered list.
  Verify deletion and explicit clearing still update the list appropriately.

### UX-02: Shared Magic controls lack accessible names and state

- **2026-10-13 local implementation:** slot buttons expose position/usage and
  pressed state; preparation controls name the spell and profile; level toggles
  are native buttons with expanded state. Component regressions inspect those
  attributes and callbacks. A first unprepared spell also leaves the preparation
  tab reachable.
- **2026-10-14 native follow-up:** NVDA verified control names and state; the user
  confirmed understandable speech. The final7 candidate fixes focus loss during
  preparation autosave and distinguishes same-named spells by source. Forward
  and reverse traversal and restored checkbox focus passed, including after
  keyboard preparation saves. See [desktop acceptance evidence](desktop-installer-acceptance.md#screen-reader-checks).

- **Impact/evidence:** the shared Magic slot buttons are empty and do not expose
  a descriptive accessible name or pressed state; spell preparation checkboxes
  need spell-specific names. Visual state alone is insufficient.
- **Location:** [CharacterMagicWorkspace.razor](../Aurora.Components/Shared/CharacterMagicWorkspace.razor),
  slot buttons and spell-row preparation inputs.
- **Correction/done when:** expose slot level/position and used/available state,
  name preparation controls for their spell, and verify keyboard operation plus
  accessibility-tree output. Perform an actual screen-reader check before
  claiming spoken announcements are verified.
- **Scope:** the committed Android Session accessibility work (`ce6ef1b`) is a
  separate completed surface; do not reopen it solely because shared Magic still
  has these omissions.

## Performance and responsiveness

### PERF-01: Cloud requests hold the global character lock

- **2026-10-13 local implementation and verification:** upload captures immutable
  bytes under the file gate, releases both gates for the provider request, and
  reconciles under a short file gate. Download happens before one guarded
  save/recovery/replace/hydrate operation. Five `CloudCharacterConcurrencyTests`
  exercise stalled upload success/failure/conflict, late edits during download,
  and editing A while A uploads followed by saving B. Existing cloud recovery and
  hydration regressions also pass.

- **Impact/evidence:** the earlier audit stalled a Drive request and reproduced
  another local character being unable to enter its context. Cloud save/reload
  holds the global context while awaiting network work. The audit observed a
  30-second context timeout versus requests allowed to run for 90 seconds.
- **Location:** [BuildService.cs](../Aurora.App/Services/BuildService.cs),
  `SaveTabCoreAsync` and cloud reload paths; [CharacterContext.cs](../Aurora.App/Services/CharacterContext.cs).
- **Correction:** capture a consistent payload under the global context, perform
  network work with appropriate per-tab coordination, and reacquire context only
  for guarded state updates. Preserve edit-version/conflict/recovery protections.
- **Done when:** a deliberately stalled upload/download for A does not prevent
  editing local B; later edits to A survive completion, cancellation, and conflicts.

### PERF-02: Dirty-state reads hash whole files

- **2026-10-13 local implementation and measurement:** dirty reads cache hashes
  against disk stamps, receipt identity and filesystem notifications. Explicit
  saves invalidate the cache; watcher failure falls back to full hashing. On a
  2 MiB fixture, `CloudPendingStateCacheTests` verifies 1,000 unchanged dirty reads
  perform zero additional content reads, versus 1,000 in the old implementation.
  Same-size/same-timestamp edits, deletion and explicit invalidation are covered.
  Notification delivery is asynchronous; this is UI dirty-state caching, while
  save/recovery boundaries retain authoritative hashes.

- **Impact/evidence:** `CloudCharacterSession.HasPendingChanges` synchronously
  reads and hashes the character and session files; a clean cloud tab's dirty
  getter can invoke it repeatedly during rendering. No UI timing claim is made.
- **Location:** [CloudCharacterSession.cs](../Aurora.Logic/Services/Storage/CloudCharacterSession.cs),
  `HasPendingChanges` / `HashFile`; [CharacterTabModel.cs](../Aurora.App/Services/CharacterTabModel.cs).
- **Correction/done when:** cache pending state with explicit invalidation and
  retain authoritative hashes at save/reconciliation boundaries. Measure file
  reads during unchanged rerenders, and verify external edits still invalidate
  the result and conflict detection remains intact.

### PERF-03: Note and session writes are not coalesced

- **2026-10-13 local implementation:** Web note input retains a draft and writes
  on blur, Save Notes, or internal navigation; other equipment actions flush first.
  Failures retain the draft and block internal navigation. Expected-character
  checks include the engine lock boundary. Browser-close confirmation protects
  pending drafts, but force-closing a circuit cannot guarantee an asynchronous save.
  App session-only changes skip unchanged currency and notes XML patches.
- **Evidence:** `EquipmentNoteDraftTests` checks twenty inputs with no commit until
  blur. `SessionSaveCompletionTests` checks ten HP/condition saves with unchanged
  XML bytes and timestamp. Browser note acceptance is tracked in
  `equipment-notes.spec.ts` and passes typing, explicit save, blur and internal
  navigation persistence; this measures behavior, not write latency.

- **Impact/evidence:** Web equipment-note input can trigger full character saves
  per keystroke. App HP/condition/slot changes also schedule currency and notes
  XML patches even when those fields did not change.
- **Location:** [WebCharacterEngineService.cs](../Aurora.Web/Services/WebCharacterEngineService.cs),
  equipment-note updates; [Session.razor](../Aurora.App/Components/Pages/Session.razor),
  `PersistSessionChanges`.
- **Correction/done when:** coalesce note persistence and write only affected
  data. Count writes for a representative typing/session sequence; flush the final
  edit on explicit save and navigation/close, preserving failure and retry behavior.
  Establish COR-05's completion contract before reducing writes.

### PERF-04: Inventory loading recalculates weight per row

- **2026-10-13 local implementation:** nestable weight-deferral scopes wrap bulk
  equipment loading and clearing; normal edits still recalculate immediately.
  `InventoryBulkWeightTests` compares 10/100/1,000 rows, verifies equal totals and
  one final aggregate notification instead of one per row, and covers exception
  disposal, quantity, storage and subsequent edits. Expected row scans fall from
  N(N+1)/2 to N for insertion; this is a synthetic workload, not a full load-time claim.
  One local run measured 1,000-row insertion at 68.03 ms immediate versus 0.25 ms
  deferred (10 rows: 0.02/0.00 ms; 100 rows: 2.11/0.04 ms). Timings are illustrative;
  operation counts and equal totals are the regression contract.

- **Impact/evidence:** adding each inventory row recalculates weight across the
  existing collection, giving repeated work that grows quadratically during bulk
  loading. This is a code-based performance candidate, not a measured UI regression.
- **Location:** [CharacterInventory.cs](../Aurora.Logic/Models/Equipment/CharacterInventory.cs),
  item insertion and weight calculation.
- **Correction/done when:** defer aggregate calculation during bulk load and run
  it once afterward while preserving immediate updates for normal edits. Compare
  operation counts and timings at increasing inventory sizes; totals must match
  after add/remove, quantity, and weight-affecting changes.

### PERF-05: Repeated statistic searches and compendium projections

- **2026-10-13 local implementation and measurement:** statistic reads use one
  scan, preserving public mutable-list and duplicate-name semantics without a
  cache. `StatisticCollectionLookupTests` compares the old and new lookup at
  100/1,000/5,000 groups and checks mutations/aliases. Web caches the filtered
  projection by catalog identity plus all filters. `CompendiumProjectionTests`
  measured 60,000 entry visits before and 10,000 after for six reads over 10,000
  entries; filter/catalog changes invalidate it and retain source ordering.
  One local run of 1,000 lookups measured 8.22/1.25 ms at 100 groups,
  50.99/10.82 ms at 1,000 groups, and 249.96/65.52 ms at 5,000 groups
  (legacy/single scan). These synthetic timings are not an end-user latency promise.

- **Impact/evidence:** statistic access repeatedly searches collections; Web
  compendium rendering rebuilds filtered projections multiple times. Benchmark
  the relevant workloads before deciding which optimization is worthwhile.
- **Location:** [StatisticValuesGroupCollection.cs](../Aurora.Logic/Services/Calculator/StatisticValuesGroupCollection.cs)
  and [Web Compendium.razor](../Aurora.Web/Components/Pages/Compendium.razor).
- **Correction/done when:** introduce measured lookup/projection reuse with
  explicit invalidation. Verify unchanged results, sorting, filtering, source
  changes, and per-character/catalog isolation; record before/after measurements.
- **Existing context:** [load optimization follow-ups](maintained-solution-audit-2026-10-02.md#3-finish-maintained-code-load-optimizations-with-explicit-invalidation)
  cover related Magic detail loading and catalog indexes. Keep those older
  candidates distinct unless a measured change addresses both.

## Hosting

### WEB-01: Concurrent hosting requires session-local engine state

- **2026-10-13 investigation:** the engine and ownership guard remain singleton
  services; browser workspace/session services are scoped. Existing defaults
  expire workspaces after 120 minutes and run cleanup every 30 minutes. Upload
  limits exist (64 files, 64 MiB each; archives 512 entries/256 MiB expanded), but
  these do not define a budget for concurrent active engines. Cleanup uses
  directory timestamps rather than an explicit active-engine lease.
- **2026-10-13 user decision:** target a small hosted group with up to **10 active
  users**. This does not remove the current one-owner runtime constraint.
- **2026-10-14 launch scope:** offer private access to friends and subscribers in
  the owner's higher Patreon tiers. Treat **10 simultaneous active users** as
  the initial capacity and acceptance-test target, with configurable resource
  limits. Scale beyond that when demand is demonstrated; 10,000 users was a
  hypothetical discussion, not a launch requirement. Access will use **manual
  invitations approved by the owner**, including approval of eligible Patreon
  subscribers; automatic Patreon integration is outside this first release.
  Invitation-only access must be enforced before launch. This decision does not
  change the runtime's current one-owner constraint.
- **Remaining action:** choose the host/OS, memory/storage or monthly budget and
  reconnect/idle-retention policy. The next
  implementation milestone must isolate mutable character/content contexts,
  protect active workspaces during cleanup, cap active sessions, and prove two
  users' load/edit/save isolation before removing the current guard.

- **Impact/evidence:** Web currently serializes access to process-wide engine
  state and permits one browser circuit to own an active character. Rejecting
  another owner is intentional protection, not evidence of current cross-session
  leakage. It prevents normal concurrent multi-user character editing.
- **Location/context:** [Aurora.Web constraints](../Aurora.Web/README.md#current-constraints),
  [WebCharacterEngineService.cs](../Aurora.Web/Services/WebCharacterEngineService.cs),
  and [hosting follow-ups](maintained-solution-audit-2026-10-02.md#5-define-web-session-lifetime-and-hosting-budget).
- **Correction/done when:** isolate mutable engine state per session before
  removing the ownership guard. Prove simultaneous users can load, edit, and save
  different characters without state/content leakage; define session lifetime,
  active-session cleanup, and resource limits before concurrent public hosting.
  This is a separate architectural milestone from the bounded Web bugs above.

## Resolved findings

The following four P1 findings were fixed in **`c331b81`**. The implementation
turn reported 24 added regression cases plus a browser regression, with focused
checks, browser verification, and the Windows build passing. Those are historical
results; the current focused retest evidence is recorded above.

| ID | Original failure | Correction and retained evidence | Status |
| --- | --- | --- | --- |
| DONE-01 | Reopening B after switching to A could reuse A's preloaded state and overwrite B. | Character-context changes invalidate stale preload identity. [CharacterIdentityRegressionTests.cs](../Aurora.Tests/Tests/CharacterIdentityRegressionTests.cs) | Resolved in source |
| DONE-02 | A failed load could attach the preceding character to the failed file. | Reject stale character data when valid partial-load evidence is absent. [CharacterIdentityRegressionTests.cs](../Aurora.Tests/Tests/CharacterIdentityRegressionTests.cs), [CharacterLoadNavigationTests.cs](../Aurora.Tests/Tests/CharacterLoadNavigationTests.cs) | Resolved in source |
| DONE-03 | Build operations could discard pending Manage edits before saving. | Flush editable snapshot fields before mutations/resnapshotting. [BuildPendingSnapshotEditsTests.cs](../Aurora.Tests/Tests/BuildPendingSnapshotEditsTests.cs) | Resolved in source |
| DONE-04 | Top-bar Web search performed a full navigation and lost the imported workspace. | Circuit-preserving search navigation. [web-shell.spec.ts](../tests/e2e/web-shell.spec.ts) | Resolved in source |

## Sources and maintenance

- October solution-audit conversation: `01a11a90-b8f5-7163-a2cc-caf0dce3f941`.
  Its four rules/slot reproductions and cloud-lock reproduction are historical
  execution evidence; other findings remain code traces or performance candidates.
- October 13 source review and documentation inventory: consolidated here from
  conversation `01a1373a-f283-7442-b98b-4a2490f3a687` against `a5348aa`.
- [October 2 maintained solution audit](maintained-solution-audit-2026-10-02.md)
  supplies the existing persistence, multiclass, performance, and hosting context.
  Older reports remain historical records rather than being rewritten as current
  validation. Broad feature labels such as "Available" do not close a concrete bug.

When taking an item, record its owner/branch if useful, reproduce the described
case, and update the evidence with the exact focused check. On completion, retain
its ID and record the fix commit and verification result. Track release/install
verification separately. Do not close items solely because a related UI changed,
an existing test suite passed, or a similarly named historical issue was fixed.
