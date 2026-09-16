# Content database rehearsal — September 15, 2026

The full installed corpus now passes a headless Windows fresh-build and migration
rehearsal through the current Lights services and locally rebuilt Translator.
This establishes database/service compatibility, not release readiness or UI and
character compatibility. Production XML, database, and characters were not changed.

## Scope and evidence

The reusable harness is in tools/ContentDatabaseRehearsal. It links the actual app
database, loader, fallback and parity service sources; it does not render MAUI or
exercise character choices. All writes were confined to disposable cases beneath:

    buildtmp/content-rehearsal-20260914-235248-c2e091

That ignored directory contains per-operation JSON results and application logs,
manifest.json, installed-before.json, and audit.json. The installed v11 database
was copied using SQLite backup. A v10 fixture was generated with the legacy writer
on a separate copy; it is a compatibility fixture, not an archived user database.
All v12 cases received the three existing Translator alias drafts only after their
input and output hashes matched the recorded evidence. No drafts were installed.

The final audit checked 1,190 installed XML files and the installed database:
installedChanges is empty. No character files were included or modified.

## What alias corrections mean

These drafts preserve distinct local variants with new IDs and rewritten references.
They do not introduce a runtime alias lookup or automatically remap saved characters.
For example, the local Farmer becomes
ID_WOTC_PHB24_BACKGROUND_FARMER_LOCAL_PHB24_FARMER, while the authoritative Farmer
retains ID_WOTC_PHB24_BACKGROUND_FARMER.

There are 78 renamed IDs: one Farmer, 31 PHB24 pack definitions, and 46 EFA
Artificer definitions. Their suffixes are _LOCAL_PHB24_FARMER, _LOCAL_PHB24_PACKS,
and _LOCAL_EFA. The drafts have 79 add corrections because Farmer also includes
an already-unique Tough feature. These are separate local alternatives, distinct
from the six earlier hotfix files that correct authoritative content in place.

## Results

| Operation | Elapsed | Peak load working set | Result |
| --- | ---: | ---: | --- |
| Existing v11 load and postprocessing | 19.0 s | 579 MiB | Passed |
| Legacy-generated v10 load and postprocessing | 20.6 s | 572 MiB | Passed |
| Fresh v12 creation | 23.2 s | — | Passed |
| v11 to v12 refresh | 188.0 s | — | Passed |
| v10 to v12 refresh | 169.6 s | — | Passed |
| Migrated v11-to-v12 load | 21.0 s | 848 MiB | Passed |
| Migrated v10-to-v12 load | 25.0 s | 839 MiB | Passed |
| Fresh v12 load, all sources enabled | 23.4 s | 904 MiB | Passed |
| Unchanged v12 refresh | 25.0 s | — | 0 changed; 1,190 unchanged |
| v12 change scan | 0.276 s | — | Not stale |
| v12 XML/database definition parity | 19.1 s | 940 MiB | Exact match |

These are single-run observations, not controlled benchmarks. Some independent
operations ran concurrently. Load includes postprocessing and caches. Refresh
process metrics exclude the separate Translator process, so writer peak memory
is not reported here. Parity holds two snapshots and is not ordinary app loading.

Fresh v12 and both migrated databases contain 20,954 canonical rows with 20,954
distinct Aurora IDs. Prepared base/effective XML, append operations and global
catalog IDs match across all three databases. Every case passes SQLite integrity
and foreign-key checks. The v11 migration preserves disabled preferences while
retaining 1,068 definitions belonging to disabled packages in the database.

Each v12 database records 1,194 applied and 21 unresolved append operations, and
90 review-pending corrections. Missing append targets remain recorded rather than
making them disappear. Matching installed copies did not approve corrections.

The enabled v12 XML and database projections each contain 20,134 definitions,
including built-ins, with no missing IDs or differing parsed definition XML.
The fresh all-enabled projection contains 21,202. These counts precede runtime
generation and are not interchangeable with the canonical SQLite row count.

Focused validation: 37 tests passed (12 prepared projection tests plus 25 existing
correction-lifecycle/recovery tests). The Windows app and rehearsal harness build
successfully with zero warnings and errors in the final incremental builds.

## Fixes made during this rehearsal

- Runtime correction composition attributed newly added local definitions to the
  authoritative supplier. It now retains the local supplier, matching Translator
  preparation. Replacements retain the authoritative supplier. A regression test
  verifies independent supplier filtering for the new local definition.
- Generic-type diagnostics now exclude intrinsic resource definitions, avoiding
  a misleading authored-content warning on the built-in Weapon Category resource.

## Remaining work before release

1. Repair the EFA draft before installing it. The local class and official class
   both contain the nested ID_EFA_MULTICLASS_ARTIFICER identity. The draft/helper
   must assign a distinct local multiclass ID and rewrite its requirements and
   references without changing the embedded authoritative baseline. This identity
   is generated at runtime from a nested declaration, so canonical SQL uniqueness
   alone does not catch it. No draft was modified to hide this finding.
2. Profile prepared-load memory. The saved-preference v12 case peaks about 269 MiB
   above v11 (approximately 46%). Projection semantics also changed, so this does
   not isolate the cause. Retained XML/projection caches are candidates to inspect,
   not a proven explanation. This matters particularly for mobile targets.
3. Rehearse real character loading, source toggles, choices, progress/cancellation,
   and failure recovery in the UI before a release claim. Headless service success
   cannot establish that existing character selections still behave correctly.
4. Complete the shared preparation/writer library for on-device creation on Android
   and Catalyst, then validate storage, native SQLite, cancellation and memory.
   The interim Windows executable does not supply that cross-platform capability.

The v12 runtime has 463 duplicated generated-ID groups; 462 already occur in the
v11 runtime. The EFA multiclass ID is the only new group in this comparison. Many
existing duplicates are generated scroll/proxy identities; the scroll generator
discards the source prefix when deriving IDs. This is a separate identity audit,
not evidence of duplicate canonical database rows.

Minor producer follow-up: content_root_hash is null on fresh v12 and retains the
legacy value on migrations. Current input freshness uses local_correction_inputs
and the unchanged scan succeeds; the legacy metadata field should be clarified
or maintained to avoid misleading diagnostics.

The disposable databases contain rehearsal paths and must not replace production.
No commit, push, public snapshot, or installed-content activation was performed.

## Follow-up: Artificer intent and measured memory cost

The installed user/local/efa-class.xml predates the new alias draft. It declares
version 0.1.4; the authoritative the-book-of-xellarant/efa-class.xml declares 0.1.5
and uses the same update URL. Both contain the same 58 top-level IDs. Normalized
comparison finds 44 changed definitions: 41 descriptions, eight sheet blocks,
one rules block and one setter block (categories overlap). The only rules-block
difference is the local subclass selector's extra level=3. The setter difference
is a garbled dash in the local Steel Defender challenge value. Descriptions are
often condensed and sometimes omit details, such as Experimental Elixir expiry.

The local copy already has the class/feat/epic-boon progression changes present
in source commit 553cfd5 (July 27, 2026), but lacks that commit's subclass-selector
change. The content repo also records picker-description work on June 9 and fuller
descriptions on June 11. This supports interpreting the local file as an older
working override; it does not prove the original creator's intent. A complete
current-file comparison is in buildtmp/artificer-local-comparison.txt.

This supersedes the narrow recommendation above to repair and then install the
EFA alias draft: first determine whether any local correction still needs keeping.
Differing content alone does not establish an intended alternate class. Prefer
retiring a stale override or preserving only demonstrated corrections over
permanently introducing a second Artificer. No local file or draft was changed.

Additional diagnostic runs isolate approximately 175.8 MiB of retained managed
memory in the eagerly constructed v12 fallback index for 20,134 definitions.
profile-projection measures 61.1 MiB before and 236.9 MiB after building that index,
holding its input projection alive. The input XML contains 23,535,503 characters.
profile-load independently measures approximately 674.8 MiB retained after GC,
and 499.0 MiB after releasing the fallback snapshot and its publication callback.
Invalidating the static snapshot alone releases none of it: the loader lookup
state retains the callback that captured it. These measurements concern managed
heap retention, not peak process working set, and should not be subtracted directly
from the prior 848 MiB peak figure.

The prepared path parses XML into XElement trees, serializes the projection,
parses again into gameplay XmlDocuments, parses again for the spell-access map,
and separately parses a complete fallback index. Gameplay elements also retain
their XML node and serialized string. Some additional full-definition payload is
intentional fidelity; the measured duplicate fallback representation is avoidable.
Recommended optimization: build a lightweight fallback index over the completed
candidate's XML (with mutation isolation), release the publication callback after
successful activation, and remove redundant parsing/serialization. Preserve
complete content, current corrections, source filtering and failed-load rollback.
These are identified optimization opportunities, not yet production fixes.

## Completed follow-up: fallback storage and Artificer archive

The user authorized both changes. The installed local Artificer was moved to:

    C:\Users\Ralla\Documents\5e Character Builder\Content Archive\2026-09-15-stale-artificer\efa-class.xml.archived

The archive is outside custom and has an extension excluded from XML discovery.
SHA-256 is 4EEDAA7D481DAD269F696E576BA1743D60F15BCD06132049735E5905FAD0E11C,
identical before and after the move. installed-archive-audit.json confirms this
is the only installed XML change and the production SQLite hash is unchanged.
The EFA alias draft is now superseded and must not be installed. Farmer/PHB24
pack drafts remain uninstalled; their status was not changed by this decision.

The prepared fallback index now shares the immutable ElementNodeString already
owned by each parsed definition, with small identity/support lookup records.
It no longer holds a second complete DOM. Each node-consuming fallback operation
parses only the requested definition into an independent temporary document.
Gameplay mutation or materialization cannot modify the cached source definition.
Legacy raw XML append assembly retains its existing mutable-document behavior.
The prepared lazy-reload path also retains XML strings rather than a second DOM.

The loader builds spell-access lookups from already-parsed nodes and clears the
publication callback after activation. Snapshot staging and rollback remain intact.
No forced garbage collections were added to the application.

Measurements on the same original migration-v12 rehearsal corpus, before removing
its EFA draft, isolate the code change from the content change:

| Metric | Before | After |
| --- | ---: | ---: |
| Peak process working set | 844.3 MiB | 617.5 MiB |
| Retained managed heap after diagnostic collection | 674.8 MiB | 503.1 MiB |
| Retained fallback index | 175.8 MiB | 4.3 MiB |
| Publication callback retained after successful load | Yes | No |

The fixed run succeeds in 21.9 seconds. These remain single-run measurements;
managed retention and peak process working set are different quantities. This
addresses the measured duplicate fallback cost; broader mobile profiling remains.
The original result is preserved as migration-v12/profile-load-before-fallback-fix.json.

Nine linked-service behavioral checks pass: list and starting-equipment lookup,
live mutation isolation, staged publication, activation, rollback, materialization
provenance, repeated-materialization isolation and source exclusion. All 37 selected
projection/correction/recovery tests pass. The Windows app and harness build with
zero warnings/errors. git diff --check passes apart from an existing line-ending
normalization notice on DataManager.cs.

A separate archived-efa-v12 case rehearses removal of the draft, refresh, full
load and complete XML/database parity. Its 20,908 canonical IDs are unique; the
authoritative ID_EFA_CLASS_ARTIFICER remains and no _LOCAL_EFA IDs remain. SQLite
integrity/foreign-key checks pass. Both runtime-input projections contain 20,088
definitions with no missing or differing XML. Postprocessing produces 93,620
runtime elements, and the EFA multiclass duplicate is gone. The 462 previously
existing generated duplicate-ID groups remain outside this fix's scope.

Production refresh, character/UI rehearsal, commit and publication were not done.
Rebuild/restart the app to use the memory fix. The installed database still predates
the archive and must be refreshed as part of the coordinated content rollout.

The subsequent [pre-UI validation](pre-ui-validation-2026-09-15.md) adds character,
reload/failure and Release-bundle evidence. It identifies an Essentials filtering
compatibility gap and packaging gaps despite the earlier database/parity success.
It also clarifies that local files are not inherently overrides and records the
actual Farmer/equipment-pack differences. Do not infer character release readiness
from the successful corpus import or parity checks alone.
