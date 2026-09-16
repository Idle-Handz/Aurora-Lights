# Prepared content v12 integration plan

Date: 2026-09-14
Status: interim implementation authorized and in progress. The application ships
with essentials only; users supply content and build their database locally.
Cross-platform database creation through a shared library is required follow-up,
not an optional enhancement or a workflow based on downloading complete catalogs.

## Inputs and agreed behavior

Reviewed the sibling Translator handoff (especially section 20),
`docs/proposals/reflections-prepared-content-v12.md`, and its eight-file patch.
The patch applies to this worktree and its original-file hashes match.
The original proposal was uncompiled. The integration changes described below
are being built and tested in Lights; they are not a production rollout.

Keep the canonical catalog unrestricted. Apply app preferences to projections,
compose corrected bases before eligible exact-ID appends, and preserve distinct
IDs and protected local aliases. Retain unknown types as generic records with
diagnostics. Do not change correction acceptance or retirement rules.

The proposal has a missing argument at its new `QueryArchetypeParentIds` call.
Fixing that alone does not make its parent-map behavior correct for filtered
content; both must be addressed during integration.

## Proposed gap resolutions

### 1. Preserve the in-app refresh workflow

Use a preparation-aware Translator to build and validate a candidate, then
activate it through the existing protected replacement workflow. Supply real
input provenance and let the new preparation pipeline own correction/append
composition. Audit the existing `LocalCorrectionSync` wrapper before reuse:
passing already-prepared temporary XML through another preparation pass must
not lose provenance or evaluate corrections twice.

Replace the proposal's unconditional v12 refresh rejection with a writer
compatibility check. Prefer a machine-readable CLI capability response, followed
by validation of the actual candidate's preparation contract. Translator's
current Program.cs exposes no capability command; that is a sibling-repo
follow-up, not an available API. The interim adapter probes the writer with a
disposable XML file and verifies its output preparation marker and input tracking
before allowing it to touch real content. An old or unknown writer must not replace a
prepared database. Preserve the usable installed database and report the
refresh failure if no compatible writer is available.

The current Lights launcher supports Windows and one content root only. The
user accepts a primary-root-only database refresh only if additional configured
folders remain available through raw XML loading. Supporting those folders is
a requirement of this integration, not deferred platform work.

Combine the primary database's corrected bases with secondary XML definitions
and evaluated local corrections before replaying eligible appends once. Preserve
source provenance, existing correction intent, and explicit conflict reporting;
an additional folder alone does not authorize arbitrary duplicate-ID replacement.
Exclude XML-only secondary inputs from the primary database freshness comparison
and track their runtime freshness separately. Deduplicate overlapping root paths
so a file already represented by the database is not loaded twice. The proposed
blanket suppression of XML overlay/merge on v12 is insufficient for this behavior.

Do not silently route prepared refreshes into the legacy copied importer when
additional folders exist. Additional platform writers or the shared library
remain separate rollout work. Keep progress reporting and cancellation behavior.

### 2. Commit a complete runtime snapshot only after success

Construct elements, projected metadata maps, and fallback state separately from
the active runtime. Validate and finish applicable postprocessing before swapping
the working state under the reload coordinator. Inspect DataManager's global
state dependencies before selecting the concrete swap implementation.

On a failed candidate load, preserve the prior elements and caches together.
Parity/snapshot inspection must not mutate the live maps. If there is no usable
database at startup, retain the complete XML fallback, subject to the same
identity/correction/append rules. Do not silently claim new local corrections
are active when refresh or fallback validation failed.

### 3. Apply one projection policy consistently

Use the same explicit preference snapshot for runtime elements, option fallback,
parent metadata, and parity comparisons. Invalidate dependent projections when
preferences change and rebuild them together on reload. Preference changes must
not rebuild or delete the global database links.

Derive parent information from eligible relationships. Do not infer a singular
parent from name or priority when multiple candidates remain. Generic types
should preserve shared content and produce a visible diagnostic without
assigning invented specialized gameplay behavior.

### 4. Bound the temporary copied-code bridge

For this integration, retain only the three proposed portable reader/composer
sources, unchanged from an identified Translator revision except for reviewed
integration necessities. Record their source hashes and verify parity when
updating the bundle. Do not expand the legacy importer as a second v12 writer.

Extract the full preparation and SQLite-writing pipeline into the versioned
shared library as the next migration step; extracting just these readers is not
sufficient. Lights must invoke that pipeline in-process on Android and Catalyst
as well as Windows, including first-run database creation from user-supplied XML.
Isolate CLI-only and SQL Server dependencies from the portable library. Validate
native SQLite packaging, folder access, cancellation and memory behavior on each
target. The library removes the subprocess requirement; platform validation is
still necessary. Until then, the interim Windows executable integration is not
feature-complete cross-platform content setup.

### 5. Separate snapshot validity from local input freshness

Avoid treating a changed absolute input path as proof that the SQLite catalog
is unreadable. Validate the catalog's schema and preparation contract separately
from whether it reflects current local files. Preserve the last usable snapshot
when refreshing fails, with an explicit pending-changes status.

Here, pending changes means primary-root input files changed after the database
was built, so its persisted records do not yet include those edits. It does not
mean waiting for an upstream author to approve a correction. A validated local
runtime correction can be active before the SQLite copy is refreshed. Secondary
XML-only content is intentionally outside that database and is not perpetually
reported as missing from it or awaiting import. If a runtime update fails,
preserve the last working state and identify which changes did not take effect.

For the first rollout, evaluate current local XML under its correction intent
and provenance rules; do not require a database rebuild merely to apply a valid
runtime hotfix. Retain strict evidence checks for acceptance and retirement.
Do not approve corrections, rebind source paths,
or retire files merely because content was moved or an installed copy matches.
Portable snapshot rebasing needs a later relative-path/root mapping contract;
do not copy the rehearsal database into production.

## Implementation and validation after approval

1. Integrate the reader/composer and fix the compile issue; add focused coverage
   for exact-ID append replay, intrinsic targets, and source-filter isolation.
2. Integrate complete snapshot construction and shared projection policy; test
   failed-load preservation, parity isolation, and preference reloads. Cover a
   primary SQLite catalog plus secondary XML, including cross-root appends,
   corrections, duplicate paths, and changes to secondary files without import.
3. Integrate the compatible writer and candidate validation; test old-writer
   rejection, cancellation, failure preservation, and real input provenance.
4. Build the current Windows app and rehearse refresh/load on a disposable copy
   of the installed corpus. Check representative corrections and append effects,
   generic diagnostics, and Legacy behavior. Report other platform gaps explicitly.
5. Update the canonical Translator handoff with verified behavior and remaining
   limitations. Production refresh, publication, and commits are not part of this
   planning step.

Existing unrelated audit-document changes in this worktree are preserved.

## Interim implementation evidence

The Windows adapter now uses the real primary content root and the updated local
Translator bundle. A disposable capability probe verifies the preparation marker
and real input tracking before running on user content. Secondary XML and current
local corrections join corrected bases before append replay. Postprocessing can
operate on a candidate collection without publishing it; failed loading preserves
the prior elements and lookup state. Parity checks use the same source policy and
request a refresh before comparing stale primary XML. Unsupported types are logged
as generic diagnostics; no singular archetype parent is guessed from global rows.

Twelve focused projection tests pass, including fresh database creation with the
actual Windows bundle, secondary XML, real intrinsic resources, isolated candidate
postprocessing, current local corrections without import, future-contract rejection,
filter isolation, append ordering, and overlapping roots. The 25 selected existing
correction-lifecycle and database-recovery tests also passed. Windows builds passed;
The full installed-corpus headless rehearsal now passes fresh creation, v10/v11
loading and migration, and full definition parity. See
[the September 15 report](content-database-rehearsal-2026-09-15.md) for timings,
memory, evidence and remaining issues. Android/Catalyst and interactive UI checks
remain. Follow-up memory profiling identified duplicate fallback XML storage;
the consumer now shares immutable parsed-element XML strings and materializes
individual fallback nodes on demand. The publication callback is released after
activation. See the report for measured savings and rollback/isolation checks.

The bundled executable was rebuilt locally. No installed XML, production database,
release publication, or commit was changed. The protected-alias drafts described
in the Translator handoff are still not installed. On September 15 the user
authorized archiving the stale local Artificer override; that archive is complete
and supersedes the EFA alias-draft plan. Farmer and PHB24 pack drafts remain
uninstalled and require review for the current corpus.
