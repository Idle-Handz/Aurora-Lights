# Pre-UI validation — September 15, 2026

This follow-up checks local content differences, saved characters, reload/failure
behavior, and a Windows Release publish. It does not change production content,
source preferences, character files, or the production database. Implementation
changes in this follow-up are confined to the disposable rehearsal harness.

## Terminology and local content comparison

Local XML is not automatically an override. Local files can add independent
content; call something an override only when it replaces an existing definition.
Explicit correction metadata establishes managed correction intent. A same-ID
collision without that metadata does not establish intent to keep an alternate
definition or justify automatically changing its ID.

The installed local equipment file is
custom/user/local/players-handbook-2024-items-packs.xml. Its installed published
counterpart is custom/core/players-handbook-2024/items-packs.xml, whose update URL
points to AuroraLegacy/elements/core/players-handbook-2024/items/items-packs.xml.
Both declare version 0.0.1 and contain the same 36 top-level IDs. Neither installed
file declares correction metadata. The legacy user-file path replaces same-ID
definitions; the prepared contract requires resolution of nonidentical definitions.
This is a local duplicate with changes, not evidence of 36 independent new packs.

Across those records, 30 description blocks and 30 setter blocks differ largely
in encoding (bullets, apostrophes, dashes and fractions). The local copy improves
some text but contains garbling elsewhere; it is not uniformly cleaner. The one
extract-rule difference is Guard's Light Crossbow reference:

| Definition | Published copy | Local copy |
| --- | --- | --- |
| Guard pack crossbow | ID_WOTC_PHB24_WEAPON_CROSSBOW_LIGHT | ID_WOTC_PHB24_WEAPON_LIGHT_CROSSBOW |

The installed items-weapons.xml defines LIGHT_CROSSBOW, so the local reference
corrects that unresolved target. No unique local equipment IDs were added.
The complete file diff is buildtmp/compare-players-handbook-2024-items-packs.diff.

Farmer's installed copies also both declare version 0.0.1:

| Aspect | Published PHB24 copy | Local copy |
| --- | --- | --- |
| Farmer ID | ID_WOTC_PHB24_BACKGROUND_FARMER | Same |
| Tough grant | Direct Feat grant | Through a new Background Feature |
| New feature | Absent | ID_WOTC_PHB24_BACKGROUND_FEATURE_FARMER_TOUGH |
| Additional marker | Absent | ID_INTERNAL_GRANTS_BACKGROUND_WITH_A_FEAT |
| Display | Existing Tough reference | Adds feature reference and Tough sheet entry |

The extra feature, named Origin Feat (Tough), grants Tough and the internal marker.
Both routes ultimately grant the same Tough feat. The ability-score, skill and
tool grants otherwise remain unchanged. Neither file has correction metadata.
The complete diff is buildtmp/compare-background-farmer.diff. Neither file was
edited, archived, annotated, or assigned new IDs during this follow-up. Earlier
Farmer/pack alias drafts remain hypothetical; the v12 rehearsal uses those drafts
solely to keep its existing prepared corpus reproducible.

## Windows package evidence and remaining distribution gaps

A local win-x64 Release publish succeeds at buildtmp/pre-ui-release. Its bundled
Translator hash matches the local source bundle. Running that executable from an
isolated working directory with PATH restricted to Windows System32 creates a
fresh v12 database containing ID_USER_FIRST_RUN, with the required contract marker
and passing SQLite integrity. Evidence: buildtmp/pre-ui-package-smoke/result.json.
No app UI, installer or release publication was launched.

Two release gaps remain:

1. Aurora.App/BundledTools/AuroraTranslator is Git-ignored, and the Windows build
   and release workflows do not fetch/build a Translator artifact. Local success
   is not clean-CI reproducibility. Pin a compatible producer artifact and require
   its presence/capability during packaging; do not choose a moving producer branch.
2. The current bundle is framework-dependent. Pointing its .NET runtime lookup at
   an empty directory produces exit 2147516547 and a missing-.NET error. The release
   workflow's self-contained app does not automatically make this nested executable
   self-contained. Prefer a self-contained interim importer, then replace the
   process with the shared library. Evidence: missing-runtime-result.json alongside
   the successful smoke result. This is a simulated missing-runtime check, not a
   clean Windows VM or Windows ARM64 execution test.

## Character comparison boundaries and initial findings

The harness links the app's actual MAUI selection/spell handlers and invokes the
shared CharacterFile reader/writer after the actual database loader. External
launching is disabled. It initializes disposable portrait/gallery directories;
the first attempt exposed missing harness directory initialization and was rerun.
It does not run CharacterService's UI wrapper, BuildService extras reapplication,
or UI choice normalization. Shared round-trip failures therefore require tracing
through those app-specific stages before claiming an equivalent UI failure.

Four user character copies and one existing Paladin fixture were selected. The
original character hashes are recorded in character-input-hashes.json. Each saved
output lives under its disposable case. Comparison includes registered IDs, prepared
spell IDs, serialized inventory, and choice rows. Generated runtime GUIDs are
normalized when comparing row metadata; authored IDs and stable row parts remain.

Saved preferences disable core-ale-xml (Aurora Legacy Essentials). Under v11,
resource copies still supplied those IDs; v12's no-resurrection rule excludes them
when their catalog supplier is disabled. Art E consequently reports 44 unset
elements and Fresh E 90 versus v11's zero and three. Missing registrations include
alignment, languages, armor/weapon/tool/skill proficiencies and vision.

Enabling only that supplier in a third disposable v12 copy restores almost all
registrations for those characters. The remaining ID-set difference is generated
ID_PROFICIENCY_WEAPON_PROFICIENCY_CLAW (one instance for Art E and three for Fresh E).
Art E then reports a successful first load, while Fresh E's saved-count warning
changes to six. No real user preference was changed.

Proposed decision, not implemented: define explicit always-available intrinsic
essentials independently of selectable published sources, while continuing to
exclude ordinary disabled content. This needs a narrow host/supplier policy,
not blanket resurrection of every disabled catalog ID.

The first sequential runs also exposed shared-reader issues already present on
v11: Art E loses three ASI option registrations on save/reopen, and the run stalls
after completing two characters. Partial evidence was preserved and remaining
characters were moved to fresh, bounded processes for isolated checks. The three
lost IDs are ID_WOTC_ASI_ABILITY_SCORE_IMPROVEMENT_OPTION_1,
ID_WOTC_TCOE_OPTION_CUSTOMIZED_ASI_CONSTITUTION_INCREASE_1 and
ID_WOTC_TCOE_OPTION_CUSTOMIZED_ASI_INTELLIGENCE_INCREASE_2. These are not yet assigned
to a new v12 regression. First-load failures must not be hidden by a stable second
load of an already reduced save.

## Reload memory

Four full-corpus reloads retain 502.02, 508.05, 508.05 and 508.06 MiB after diagnostic
collection. This stabilizes after warm-up. Peak memory can be higher during a
reload because atomic replacement keeps the old snapshot alive while constructing
the new one. No production forced collections were added.

Raw evidence remains under buildtmp/content-rehearsal-20260914-235248-c2e091.
Further completed failure and isolated-character results are recorded below.

## Completed service failure checks

All 13 fixture checks pass through the actual app services: fresh creation,
secondary XML changes without import, unchanged database on secondary reload,
disable/reenable projection behavior, retention of disabled canonical records,
live-element and lookup/fallback preservation on malformed correction XML,
candidate preservation and failed status on rejected refresh, and cancellation
with an intact database and Idle state. Cancellation is injected as the main
import begins after the capability probe; this is not a power-loss/kill test at
every possible activation boundary. Evidence: archived-efa-v12/failure-check-result.json.

Two harness fixture errors were corrected before those results: Windows-relative
path normalization for the supplier lookup, and sharing-compatible read access
when hashing an open SQLite file. Neither required changing the app's runtime code.
Full-corpus memory was not repeatedly rerun to validate those tiny-fixture changes.

## Completed isolated character comparisons

All nine isolated runs completed. Gobric, the prepared-paladin fixture and Remy
Morningstar each load and round-trip successfully under v11 and under v12 with
Essentials enabled. With the saved v12 preference (Essentials disabled), their
first loads report 68, 76 and 100 unset items respectively. Saving that reduced
state and reopening succeeds, which is why first-load integrity is checked
separately from round-trip stability.

Within every completed isolated run, registered-ID/prepared-spell state, normalized
choice rows and serialized inventory match between first load and reopen. The
first-load registrations do not exactly match across database versions: besides
Claw, Gobric and Paladin lose generated proficiencies for excluded Ryoko weapons
when v12 correctly filters those sources. These differences must remain explicit;
a successful load result is not proof of identical gameplay content.

The five-character sample therefore does not establish overall character safety:
Essentials exclusion is a concrete new compatibility problem, and the Art E ASI
round-trip loss and sequential lifecycle stall still need app-wrapper tracing.
Those findings were not bypassed, silently corrected in character files, or counted
as passing. Original character hashes still match after all runs. Summary evidence:
isolated-character-summary.json, per-case character-sequential-partial.json and
character-<filename>.json.

Recommended next decisions before relying on manual UI validation: approve a
narrow intrinsic-Essentials supplier policy; select a pinned, self-contained
Windows Translator artifact for reproducible packaging; then trace the existing
character lifecycle/ASI findings through the app-specific wrapper. None of those
broader changes was implemented in this verification follow-up. No commit, push,
production refresh, additional local-file edit, or release publication occurred.

## Follow-up: confirmed content intent and required infrastructure

This section supersedes the pending Essentials decision and the pack/Farmer
assumptions above. The user confirmed that Farmer is an intentional local
preference, and that the Guard crossbow fix should be marked as a correction.

The actual installed `core/players-handbook-2024/items-packs.xml` and
`user/local/players-handbook-2024-items-packs.xml` each define the same 36 IDs:
16 background packs, 13 class packs (including two Fighter options), and seven
shop packs. Starting-equipment bundles and shop packs are distinct elements;
the duplication here is between these two complete files, not between those
categories. The PHB 2014 pack file separately contains seven shop packs.
Most other local differences concern text encoding; the files are not exactly
identical. No pack definitions were deleted or renamed.

Installed metadata now marks Guard's existing replacement as `replace`, state
`review-pending`, key `guard-light-crossbow-reference`, with the authoritative
file baseline and exact original-element fingerprint embedded in XML. Its correct
reference is `ID_WOTC_PHB24_WEAPON_LIGHT_CROSSBOW`. Three checks verified effective
Guard content, preservation of all existing local XML, and correction protection.
Other unclassified local text edits remain preserved and review-pending. They can
prevent whole-file retirement even after the Guard fix is published. The original
local file is backed up only in the disposable workspace, not in the content root.
New local-file SHA256:
`2DD8407C1D3E155F2FD0B572308B7D0DEB24F245BD1B93BCADB6C51999236BE1`.
Farmer remains unchanged; its feature wrapper and grant of
`ID_INTERNAL_GRANTS_BACKGROUND_WITH_A_FEAT` are intentional. The earlier alias
draft is not an approved conversion of that preference into a separate variant.

Live read-only inspection found `content_packages` row 1, `core-ale-xml`, name
`ALE.xml`, kind `core`, with `is_enabled=0`. A separate descriptive Essentials
row (`core:aurora-legacy-essentials`) was enabled; it did not control the ALE file's
supplier. No evidence identifies the action or time that saved the disabled flag.

The user explicitly chose mandatory Essentials/Internal infrastructure while
keeping PHB, DMG, and Monster Manual selectable despite their `core` package kind.
`RequiredContentPolicy` now distinguishes those identities from the broad kind.
Prepared projection and package-list reads treat required sources as enabled even
if a stale flag says otherwise; reads do not rewrite the database. The package
toggle rejects disable attempts, Settings labels/locks required rows, and shared
character source restrictions cannot uncheck infrastructure. Core rulebooks remain
selectable in both controls. This is a Lights consumer policy, not a producer
filter or a change to the unrestricted catalog/append contract.

Validation: 12 focused new policy cases passed; the combined policy, existing
source-editor, and prepared-projection selection passed 26 tests. Windows app
build passed with zero errors. The subsequent nullable-context-only cleanup was
verified by building Builder.Data. No production database refresh was needed.

## Follow-up: ASI trace and sequential rerun

New diagnostics at `buildtmp/current-data-followup` used a disposable v12 copy
whose saved ALE flag was still zero. With required-source policy active, Art E
and Gobric first loads succeed; Fresh E still reports six unset items. The
Art E save/reopen still reports three lost ASI elements. Original user files
were not saved. This run still uses the earlier disposable Farmer/pack alias
drafts and is not a full refresh rehearsal of the newly annotated installed file.

The missing ASI subtree is present under Dragonborn in the original character's
selection tree, but only in the summary-ID list after serialization. Diagnostics
show that the racial ASI rule has a registered selection but no active progression
manager. Its condition is `!ID_INTERNAL_GRANTS_BACKGROUND_ASI`; Art E has the
background ASI grant. `CharacterFile.CreateRuleNodes` skips rules without a
progression manager, so it drops the parent choice and both nested +2 Intelligence
and +1 Constitution choices. This is an eligibility/legacy-save mismatch, not
a missing database definition or evidence that row identity itself was lost.
The same three-ID failure occurred with v11 previously.

Recommended next work, not implemented: trace this through the app's
ReapplyCustomFeatures/NormalizeSelectionState wrapper, then preserve unresolved
historical selections in the existing character save while applying only eligible
choices. Migrate to current background choices only when an equivalent legal
mapping is proven; otherwise request explicit character-level resolution. Do not
silently discard choices, or force old racial bonuses active on top of background
bonuses to satisfy the saved element count. Add one load/save/reopen regression
for this exact eligibility transition before changing production character logic.

The instrumented Art E -> Fresh E -> Gobric run, including each save/reopen,
completed in 75.64 seconds without the prior sequential stall. No lifecycle fix
was made, and this does not establish why earlier runs stalled. Recommend phase
timing plus a repeatable app-wrapper switching test before altering cleanup;
if reproduced, fix the measured non-progress/re-entrant transition and give it a
bounded failure path. Do not present a hypothesized cleanup loop as confirmed.

Packaging recommendation remains pending: ship a pinned self-contained Translator
artifact and explicitly acquire it in Windows build/release workflows. The local
ignored executable and its external .NET dependency remain release gaps. Shared
library migration remains the intended replacement, particularly for mobile.

The final installed-input audit shows only the previously authorized Artificer
archive and today's pack annotation differ from the original snapshot. Farmer,
all other installed XML, and the production database are unchanged. No commit,
push, release, or production refresh occurred.

## Follow-up: background ASI authority enforced during loading

The user confirmed PHB 2024 background ASIs should be authoritative and inactive
racial ASIs must not coexist with them. This supersedes the tentative suggestion
above to preserve this particular inactive ASI subtree for manual resolution.

`AbilityScoreSelectionCleanup.Normalize` is now shared by CharacterFile's final
load validation and the app's existing stale-ASI cleanup. It removes selected ASIs
whose choice has no active progression manager, clears their slot registrations,
and re-evaluates after each removal so nested choices cannot survive an inactive
parent. Active racial choices and unselected/granted custom features are not
removed merely for being ASIs. Non-progress and non-convergent cleanup fail
explicitly. The content's requirements determine eligibility; there is no
character-name test or broad deletion of racial ASIs.

Saved-count validation discounts only saved ID occurrences actually removed by
this cleanup, including affected descendants. Unrelated missing definitions are
not discounted. Three focused accounting tests passed. The existing count-based
loader validation is not replaced by a complete semantic comparison in this change.

The actual earlier sample is named Art E on disk. A disposable copy now passes
load/save/reopen with identical registered IDs, six additional ability scores,
choice identity records, and inventory. Nine runtime checks also verify background
authority/combination preservation, removal of all three inactive racial choices,
idempotence, retention of legal racial ASIs after removing the background, and
removal of the entire racial subtree after restoring that background. Evidence:
`buildtmp/asi-cleanup-review/asi-check-result.json`. Before this fix the same sample
failed reopen with three missing ASI elements. The sample's background ASI option
was blank; it remains a user choice in the UI. No new ability choices were inferred.
The Windows app build passed with zero warnings/errors. No original character was
saved or edited; changes apply when loading and subsequently saving through the app.

## Remaining prerequisite for a full-refresh UI review

An isolated refresh using the current installed Guard annotation and original
intentional Farmer preference failed in 6.82 seconds with
`duplicate-element-id: ID_WOTC_PHB24_BACKGROUND_FARMER`. Evidence:
`buildtmp/ui-refresh-content-review/refresh-result.json`. This rehearsal restored
the actual local Farmer and pack files into a disposable corpus instead of using
the provisional alias drafts. No production refresh or database change occurred.

Next proposal: annotate Farmer as an intentional local replacement, retaining
its canonical ID, feature wrapper, and additional background-with-feat grant;
then rerun that isolated refresh before reviewing the Refresh Database workflow.
Do not rename Farmer to make the conflict disappear or describe the preference as
an accidental authoring mistake. No Farmer annotation was installed in this turn.

The earlier sequential stall remains unproven and is a UI switching test target,
not a reason to invent a cleanup fix. Fresh E's previously reported six unset
elements remain a separate review case; no claim that all historical character
files now load cleanly is made. Reproducible self-contained Translator packaging
is a release prerequisite, not a blocker for local-worktree UI review. No commit,
push, publication, or installed content edit occurred in this ASI follow-up.

## Test E console review: distinct character and verified v11 replay

Test E and Art E are different installed character files. Test E is a level-three
Dragonborn Barbarian with Farmer; Art E is the Artificer/Aberrant Heir sample used
for the stale racial ASI regression. The statement that background increases were
unselected applied only to Art E. Test E already selected the STR/CON/WIS +1/+1/+1
background combination, matching the user's screenshot.

The supplied console excerpt contained 676 lines: 333 plain-text-description
warnings; 58 missing-currency exceptions, each expanded into multiple lines, for
`<set name="cost">—</set>`; 19 missing selection-name warnings; 18 spell-ID
convention warnings (nine psionic definitions reported twice); one unsupported
`source` attribute on Tessen Master's selection rule; one duplicate support
removal; six intermediate missing-character-element warnings; and the final
saved-versus-loaded count warnings. The excerpt reports data v11 with 1,300
unsynced XML elements merged, not prepared v12 loading.

A new disposable copy of the actual installed v11 database/current XML and
`Test E.dnd5e` reproduced the same 19,808-element catalog and 150-element first
character load. All 116 saved element occurrences were retained. All six IDs
named by the early missing-element warnings were present at completion. The 34
additional occurrences were armor/weapon proficiencies, including spiked armor,
Claw, firearm and Ryoko weapon proficiencies. The final message "without all
elements (-34)" misrepresents this excess as an incomplete load.

The shared-loader replay passed load/save/reopen with stable registered IDs,
prepared state, normalized choice records, inventory, and additional ability
scores `[1,0,1,0,1,0]` (STR, DEX, CON, INT, WIS, CHA). The original Test E hash
remained unchanged. Evidence: `buildtmp/test-e-console-review/characters-result.json`.
This supplements the user's real UI observation; it does not execute the full
MAUI UI wrapper or establish v12 parity.

Code inspection: plain-text descriptions are accepted despite warning severity;
missing selection names receive generated labels; missing `_SPELL_` IDs cause
scroll generation to skip those entries (the underlying psionic definitions are
not removed); the unsupported selection `source` attribute is ignored and warrants
authoring/compatibility review. ItemElementParser catches missing-currency errors
and still returns the item, so those exceptions do not mean 58 lost items.

Recommended next polish: handle unspecified prices without exceptions, move
benign formatting/convention diagnostics below warning severity where appropriate,
and report unresolved character references after final registration rather than
at an intermediate traversal. Compare/report additions separately from missing
IDs. Do not suppress unsupported attributes or genuinely unresolved definitions.
No parser or diagnostic behavior was changed during this investigation. The
Farmer metadata refresh blocker and release packaging work remain as documented.

## Art E after the user saved background increases

The next supplied console excerpt includes both Test E and Art E loads. Art E's
entry records removal of the three obsolete racial ASI selections, followed by
91 expected versus 96 loaded elements. That entry precedes the user's newly saved
background choices; the negative-five wording represents an excess count, not
five missing elements.

The actual current Art E save now selects the background STR/CON/CHA combination's
+2/+1 option with +2 Strength and +1 Constitution. A fresh disposable v11 replay
passed load/save/reopen with unchanged registered IDs, choices, inventory, prepared
state and ability increases `[2,0,1,0,0,0]`. Comparing the original saved summary
with the first completed load found no missing or additional element occurrences.
The original character hash remained unchanged. Evidence:
`buildtmp/art-e-saved-asi-review/characters-result.json`.

The additional `CalculationBase [base:0] [calc:8025]` warning occurs between the
two characters. `Coinage.Clear` sets CalculationBase to zero, triggering validation
before clearing denominations. Test E's 80 gold, two silver and five copper total
8,025 copper, exactly the old amount in that warning. This is transient reset-order
noise, not evidence of lost currency. Proposed small follow-up: clear denominations
before publishing/validating the zero total. No production code, installed content,
or user character was changed in this investigation. Canonical sibling handoff
updates remain pending following the earlier declined write permission.

## Farmer annotation completed; isolated refresh passed

The user authorized completing Farmer's annotation and repeating the isolated
refresh. The installed `user/local/background-farmer.xml` now contains embedded
intent with the authoritative baseline from
`core/players-handbook-2024/background-farmer.xml`. The existing local XML content
is structurally unchanged. Its canonical Farmer ID and Tough feature wrapper are
retained; no variant alias was introduced.

The linked review group is `farmer-local-feat-presentation`:

- `farmer-local-background`: replace `ID_WOTC_PHB24_BACKGROUND_FARMER` with an
  original-definition fingerprint and an explicit intentional-preference reason.
- `farmer-local-tough-feature`: add
  `ID_WOTC_PHB24_BACKGROUND_FEATURE_FARMER_TOUGH`, retaining both Tough and
  `ID_INTERNAL_GRANTS_BACKGROUND_WITH_A_FEAT` grants.

Both entries are `review-pending` and must be reviewed together. This records a
user preference, not a claim that the authoritative author's definition is wrong.
Five checks verified unchanged XML content, identity/reference preservation,
feature grants, complete protected intent, and survival of an unreviewed upstream
change. Original Farmer backup is in the disposable workspace, not a sidecar in
the content folder. Installed SHA256:
`24E0AD17D687E38354E1DB47B528E3B4B7CA7AE4EAC9A4979AA1E2E2D8D276AF`.

A fresh copy of the current installed XML and a SQLite backup of the installed
v11 database were prepared without provisional Farmer/pack aliases. The actual
app refresh path and bundled Translator completed activation of v12 in 192.13
seconds: 1,177 changed files, 12 unchanged, 20,629 imported elements, and 20,876
total canonical elements. Database checks found 20,876 distinct Aurora IDs,
`integrity_check=ok`, zero foreign-key errors, exactly one Farmer row and one
Tough feature row, and both mirrored preference entries still protected.

The app's DB loader subsequently succeeded in 20.00 seconds with 20,277 projected
elements and zero skipped elements. This projected count reflects runtime source
preferences, rather than the full canonical catalog size. A follow-up change scan
returned `stale=false` in 0.25 seconds. These are local-machine rehearsal timings,
not performance guarantees. Evidence is under
`buildtmp/farmer-annotation-review-20260915`: `farmer-annotation-result.json`,
`refresh-result.json`, `load-result.json`, `scan-result.json`, and `verification.json`.

The installed-input audit confirms only Farmer's annotation changed. The installed
database and all other installed XML remained unchanged. No original character was
edited. The Farmer full-refresh prerequisite is resolved; the user can now review
the real refresh workflow. No production refresh, commit, push, or release occurred.
These details belong in the next authorized canonical Translator handoff update.

### Manual UI review checklist

1. Refresh the actual content database: visible progress, responsive UI, sensible
   completion/failure status. Confirm the resulting data version is 12; an immediate
   Check for Changes should report no pending content changes. If testing cancel,
   confirm the existing database remains loadable and another refresh can start.
2. Open Test E/Farmer: verify Tough is applied once, its intended feature display
   survives, and background ASIs remain +1 STR/CON/WIS. Check Art E's +2 STR/+1 CON
   background choices and absence of a second racial ASI contribution.
3. Save, close, and reopen test copies: compare ability totals, HP, selections,
   inventory/equipped items, and prepared spells where applicable.
4. Switch Art E -> Test E -> Fresh E and back: check responsiveness and that choices,
   ability increases, spells and equipment do not bleed between characters. Inspect
   Fresh E's known partial-load warning separately instead of treating it as an ASI
   regression. The older switching stall remains unexplained.
5. Check source controls: Essentials/Internal/Core infrastructure stay enabled and
   locked; rulebooks remain selectable. Disable/re-enable an optional source and
   reload to check availability follows the preference. If using a secondary XML
   folder, confirm its content still appears.
6. For a Guard starting-equipment choice, verify the pack supplies the Light Crossbow
   correctly. Distinguish starting-equipment bundles from purchasable shop packs.
7. Clear the console before each scenario and note newly produced errors alongside
   any visible malfunction. Known plain-description/unspecified-price/transient-load
   messages still need the separately proposed diagnostic polish.

Reproducible self-contained Translator packaging remains release work; it does
not block this local worktree UI review. The advanced conflict-resolution UI remains
future work and should not be expected during this review.

## September 16: proficiency dialogs after the live refresh

The user reported Claw and further missing-proficiency dialogs while reopening
Test E immediately after a live v11-to-v12 refresh. The current live database is
v12, built `2026-09-16T16:24:34.0026162Z`. Read-only inspection confirms Claw is
stored canonically. Ryoko's saved supplier preference remains disabled; the v12
projection correctly excludes both its definitions and its append operations.
The prepared base for Simple Melee Weapons does not contain a Claw grant.

The defect was in `CharacterManager.New(false)`: its outgoing-character cleanup
called `UnregisterElement` repeatedly, and each removal reprocessed the remaining
old character graph against the newly replaced catalog. The old v11 graph still
contained Ryoko weapon grants populated through XML fallback. This mixed old
parents with new target availability and raised repeated modal warnings.

`CharacterManager` now skips remaining-graph reprocessing only during full reset.
Normal edits retain reprocessing and missing-target diagnostics. The existing
reset suppression flag was renamed to describe this wider purpose; cleanup still
removes grants, selection state, and spellcasting state through the existing path.
No importer, source preference, canonical identity, or content file was changed.

Evidence in `buildtmp/refreshed-proficiency-review-20260916`:

- Cold loading current v12: Test E loaded and round-tripped with zero missing grants.
- Before the fix, loading Test E with v11 and then replacing the catalog with v12
  reproduced Claw plus 16 additional missing Ryoko proficiency diagnostics.
  The shared loader eventually returned success, explaining why checking its
  return value alone missed the native modal disruption. Preserved result:
  `reload-before-fix.json`.
- After the fix, the same in-session transition followed by Art E and Test E
  load/save/reopen produced **zero missing-grant diagnostics**. Test E's first load
  and reopen succeeded; IDs, choices, inventory, and +1 STR/CON/WIS remained stable.
- Art E retains a separate first-load saved-count warning: its saved Simple Melee
  subtree and sum include Claw, which is absent from the filtered v12 projection.
  This is a previously derived grant, not an explicitly selected proficiency.
  Art E's +2 STR/+1 CON, loaded state, choices and inventory remained stable across
  the disposable round-trip, whose reopen succeeded. The combined rehearsal
  intentionally reports failure because the original Art E load returned partial;
  do not describe the entire batch as clean. Count-validation policy is unchanged.
- Two focused synthetic regression cases distinguish full reset from ordinary
  unregister: the reset case fails before the fix; both pass afterwards. These
  require no installed corpus. Windows app build passed with zero warnings/errors.
- The live database and both original character SHA256 hashes are unchanged.
  All rehearsal writes targeted copies, with database absolute paths relocated
  to the disposable content root. No live refresh was performed during this fix.

The earlier Farmer refresh rehearsal loaded the catalog but did not exercise
an already-open character across replacement. That coverage was insufficient;
the new rehearsal mode covers this lifecycle and treats missing-grant warnings as
failures independently of the eventual character load result.

Rebuild/restart the app and reopen Test E; another database refresh is unnecessary.
For the manual lifecycle check, load Test E, refresh/reload content, and reopen it
in the same process. Confirm no proficiency dialogs and verify its ASIs/Tough and
equipment. Retain Art E's separate saved-count warning in review findings.
These findings should accompany the next authorized canonical Translator handoff
update: catalog replacement must not re-evaluate an outgoing graph against new
definitions. No canonical handoff edit or commit was made in this turn.

## September 16: settled character diagnostics and usage-aware validation

The user approved fixing premature character-load/rule-evaluation warnings and
replacing net element-count validation. Unspecified prices and other content/parser
issues are explicitly deferred for separate review.

`CharacterFile` now queues saved children whose parent/selected element has not yet
materialized and revisits them after character and equipment reconstruction. A
successful later read removes the pending entry. Only unresolved saved usage is
reported after final validation (with the existing bounded settling window).
Normal conditional ungranting, successful load completion, and approved cleanup
are informational events; actual missing grant targets and parser exceptions have
not been suppressed.

`CharacterLoadValidation` compares exact saved IDs and occurrence counts with the
final character, rather than subtracting total counts. Its rules are:

- Additional current grants are not missing content and cannot compensate for a
  missing saved ID. A missing repeated occurrence is also detected.
- Saved choices and their direct/indirect grant chains remain required, even when
  their source has become unavailable. The character XML tree supplies the saved
  path included in the warning. Explicit choices remain checked if absent from an
  older save's summary.
- Source-only availability records are ignored only when identifiable as such.
  Separate source restrictions are not character element usage. A Source-rooted
  bookkeeping subtree is ignored unless it contains an actual character choice;
  an ID also used by a build grant/choice remains required.
- Unknown-origin summary entries remain checked conservatively. Being supplied
  by a disabled source, or being a proficiency, is not sufficient to ignore loss.
- Approved duplicate and inactive-ASI cleanup discounts only the affected IDs and
  occurrences. Removing an unsaved duplicate cannot excuse loss of a retained
  saved occurrence. List-option labels are not treated as Aurora element IDs.

Important clarification: source restrictions filter availability; they do not
themselves grant character features. The previously discussed weapon proficiencies
are beneath saved character grant chains. In particular, Art E's Claw is saved
under Class -> Simple Weapons -> Simple Melee Weapons -> Claw, so it still receives
one specific final missing-element warning. No automatic deletion or silent
acceptance of that missing grant was introduced.

Validation:

- 15 focused tests pass (10 identity/usage validation cases, two catalog-reset
  cases, three existing ASI accounting cases).
- The isolated current-v12 replay reports no premature missing-child, ungranting,
  or count-mismatch warnings. Test E loads and round-trips successfully. Art E's
  first load remains partial with the specific Claw grant-path diagnostic; his
  disposable round-trip reopens successfully. The overall two-character report
  intentionally retains failure status for that real original-save mismatch.
- Both characters' effective loaded states match the pre-change rehearsal;
  choices and inventory round-trip unchanged. Test E retains +1 STR/CON/WIS and
  Art E retains +2 STR/+1 CON. Evidence: `characters-result.json` and
  `diagnostics-verification.json` under `buildtmp/refreshed-proficiency-review-20260916`.
- The earlier stale racial-ASI fixture passes its nine runtime cleanup/round-trip
  checks, with no load warnings (`buildtmp/asi-cleanup-review/asi-check-result.json`).
- Windows app build passes. Live database and original Test E/Art E hashes remain
  unchanged. No content edits, live refresh, save-format migration, commit or push.

Rebuild/restart for UI verification; a database refresh is unnecessary. Genuine
content-authoring/parser warnings, including unspecified-price errors, remain for
the separately requested review. These app-layer validation decisions are also
pending inclusion in the next authorized canonical Translator handoff update;
they do not impose new import filtering or database identity rules.

## September 16: remove selections when their owning choice disappears

Testy's revised Guard save still contained the two former Astral Drifter languages,
the Sorcerer Magic Initiate option, and three selected spells in its summary/magic
sections. These were live orphaned selections at save time: selected elements are
progression roots, and the legacy WPF expander used to unregister them when its
choice was deleted. The MAUI bridge did not perform that deletion.

`CharacterManager` now performs the remaining cleanup after the selection-deleted
event, matching the exact owning rule instance. It removes nested selected roots
and clears every slot of the obsolete choice. Recursive cleanup defers rule
re-evaluation until the enclosing change finishes, avoiding resurrection of the
departing owner's choices. Progression iteration tolerates roots removed by that
cleanup and skips those removed roots. Independent selections, including the same
language obtained from another active choice, remain intact.

Validation:

- 19 focused tests pass: four owner-cleanup regressions and the 15 existing
  load-validation, reset-after-refresh, and ASI accounting checks. Replacing the
  background and losing a choice's level requirement reproduced the orphaned
  selections before the fix; the final suite also covers lost requirements,
  nested spells, all choice slots, saved summary/magic output, and independent
  acquisition of the same language.
- An isolated copy of Testy's cleaned save was changed to Astral Drifter, given
  the six previously orphaned choices, then changed to Guard. All six existed
  before replacement and disappeared afterward, including their registry slots.
  Saving and reopening succeeds with no load warnings or missing grants; effective
  state, remaining choices, and inventory are stable across the round trip.
  Evidence: `buildtmp/refreshed-proficiency-review-20260916/characters-edit-background-result.json`.
- The sequential Test E / Art E / Testy replay preserves state, choices, and
  inventory through save/reopen. Test E and the cleaned Testy fixture load fully.
  The original Art E Claw mismatch and Testy's six already-stale saved entries
  still produce their existing first-load diagnostics; subsequent disposable
  saves reopen cleanly. The overall report deliberately remains unsuccessful
  for those original-save mismatches, with no missing-grant warnings.
  Evidence: `selection-owner-characters-result.json` in the same case directory.
- Original Testy and live database hashes are unchanged. No live refresh, content
  edit, or save-format migration is required.

Rebuild/restart, change a background with selected languages/nested feat spells,
then save/reopen and confirm that only its old choices disappear. This prevents
new stale saves; it does not rewrite existing files or silence ambiguous
summary-only warnings in older files. Testy's already-stale original still needs
the normal reviewed load/save cycle to persist its reconstructed current build.
This is app-side lifecycle behavior, pending the next authorized canonical handoff
update; it adds no Translator/importer responsibility.

## Spell acquisition implementation (2026-09-16)

See [spell-acquisition-rules.md](spell-acquisition-rules.md) for the approved casting contract, compatibility adapters, optional rule metadata, and manual review cases. This is the pending Translator handoff supplement; the sibling repository handoff has not been modified.

