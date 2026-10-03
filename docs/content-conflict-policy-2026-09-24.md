# Content conflict and correction activation policy — September 24, 2026

> **Superseded in part.** The collision handling described here — quarantining conflicting
> definitions and retaining the previously imported one — was replaced in Aurora.Content 0.9.0.
> See `content-declaration-precedence-2026-09-27.md`. The correction-activation half still stands.

This implements the decisions following the September 23 review. Changes remain
local and uncommitted; installed content, character saves and the live database
were not modified.

## Evidence and scope

The installed content scan covered 1,189 XML files with no XML parse failures.
Excluding local files, 20,771 declarations contained 20,766 distinct IDs: the five
repeated IDs were identical definitions, with no unexplained differing canonical
definitions. Differing local overlaps belonged to annotated correction files.
Two recent isolated imports of the same corpus also recorded no skipped content.

These observations are not independent content-update events and do not establish
a probability per refresh. There is no evidence supporting a rate above the user's
15% threshold. Selective retention of last-known definitions while updating the
rest of the catalog remains deferred; a new conflict preserves the entire working
database instead.

## Implemented rules

- Identical declarations share one effective definition while retaining supplier
  provenance. Different definitions of one ID never acquire a winner by filename
  or directory order. Case/whitespace identity collisions require review too.
- On a first installation, exclude every declaration of a conflicted ID and report
  that identity as unavailable. Import unrelated definitions, including those in
  the same files. This rule is independent of the unreadable-file skip option.
- A new conflict during refresh refuses activation and preserves the existing
  database. An ID already recorded as unavailable can remain unavailable while
  unrelated updates proceed. Repair followed by refresh clears its unavailable
  status. If a previously conflicting supplier becomes unreadable, refresh refuses
  activation rather than promoting the other supplier by omission.
- Invalid, unsupported, overlapping or inconsistently reviewed correction metadata
  refuses activation in both skip modes, including on first installation. An
  unresolved collision involving an explicit correction target also blocks.
- Ordinary local content is not automatically a correction. Well-formed unmarked
  content with an invalid Aurora structure can use the unreadable-file policy.
  Malformed XML under `user/local` blocks because protected intent cannot safely
  be determined. Misplaced correction metadata also blocks instead of being skipped.
  In well-formed XML, an unused correction namespace declaration or a comment
  mentioning correction markup is not correction intent. Actual elements in the
  reserved namespace are protected; applying it as the document's default
  namespace triggers this check before the ordinary unnamespaced-root diagnostic.
- A rejected unrelated append from an authoritative file remains a separately
  reported content issue; skipping it must not discard a valid protected correction.
- Runtime local/secondary XML, host definitions and append replay cannot resurrect
  unavailable identities. A failed prepared load cannot bypass this rule by using
  unrestricted raw XML fallback. Corrected content becomes available after refresh.
- Retain the XML and declaration provenance for review. Do not retire a managed
  local file while its effective definitions are unavailable.

## Format and integration

The development package is `Aurora.Content` / `Aurora.Content.Contracts` 0.7.0,
schema version 1, data version 13. `content_unavailable_elements` stores each
unavailable ID and its diagnostic. `content_declaration_provenance` retains all
suppliers; the prepared catalog contains only eligible definitions. Skip records
use `definition-conflict` for identity exclusions, distinct from `unreadable`
whole files and `append` operations. Lights reports these counts separately.

An existing empty or unusable database is still treated as existing; this work
does not automatically delete it to obtain first-install behavior. The reader
continues to require the current data version: a preserved data-12 database is
not readable by the data-13 reader until a successful refresh. Retaining the file
on failure is recovery protection, not an older-format reading guarantee.

The library owns these rules and its current schema/data versions. Lights consumes
the vendored package. The Translator handoff is updated alongside the source.
The physical SQL uniqueness migration and advanced diff/resolution UI remain
separate future work. No automatic name-based merging or reference rewriting is
introduced here.

## Verification

Translator: 50 distinct focused scenarios passed, covering conflict exclusion,
protected corrections, repair, reader behavior, append replay, and retirement.
The final staged build had zero warnings/errors. Rejected-refresh cases assert
unchanged database/input hashes. The accepted-correction companion test verifies
retirement is deferred until the unavailable companion is repaired.

Lights: 76 focused tests passed with no failures or skips against the vendored
0.7.0 package, including full loader exclusion of built-ins, user XML and generated
proficiency proxies, prepared projections, content trust, source restrictions,
correction lifecycle, progress and reporting. Results are in
`buildtmp/content-policy-test-results/content-policy.trx` (local artifact).
The Windows MAUI x64 build passed with zero warnings/errors. Test-project output
still contains existing nullable/blocking-task analyzer warnings.

The 14 changed Translator source/test/handoff files were applied to its checkout
only after checking their original hashes; applied hashes match tested staging.
Both packages are immutable development artifacts over Translator base `1a4a93a`,
recorded with `dirtySource: true` in `vendor/nuget/manifest.json`:

| Package | SHA-256 |
| --- | --- |
| Aurora.Content.Contracts.0.7.0.nupkg | `f8a08e6e1730f5c0b873ef91a88eea24489181f4686a24c3da5060ac7e7f5716` |
| Aurora.Content.0.7.0.nupkg | `eddad5fd2c4905dcc8f6a264ae911c0388ecc5d5bb345544b86bd10f463604df` |

No full character-parity rerun, installed-platform testing, or manual UI/PDF review
was performed for this bounded policy change. No changes have been committed or
published.

## Automated lifecycle rehearsal — September 24 follow-up

The first three previously listed manual checks are now automated with the actual
`ContentDatabaseService`, persisted Settings report data, and full `DbElementLoader`.
Run `tools/ContentDatabaseRehearsal/run-policy-checks.ps1` to repeat them against
new disposable fixtures. The runner never reads installed XML or character saves.

Both values of the skip-content preference passed: **22 distinct processes,
222 assertions**. Results are retained in the local artifact directory
`buildtmp/content-policy-smoke-20260924-122340-85b9b7/summary.json`.

| Check | Evidence |
| --- | --- |
| First import | One unavailable ID; report identifies both suppliers; unrelated feature, its grant and target proficiency remain loaded. A fresh process reads the same report and exclusions. |
| Rejected refresh | New conflict and invalid marked correction both report failed sync. SHA-256 confirms unchanged database bytes; existing in-memory definitions remain intact, including the protected feature. |
| Repair and restart | Repairing the conflicting definition clears the persisted report and restores exactly one canonical definition. Repairing correction metadata restores successful sync. Separate processes load the repaired definition and protected feature correctly. |

An additional cold-start probe confirmed a separate availability limitation:
while invalid correction metadata remains on disk, runtime XML re-evaluation
refuses a fresh load even though the preserved current-format database is valid.
The narrow quarantine fallback guard does not block this case, but the raw XML
loader also rejects the invalid marked correction before publishing content
(verified in source). This is not silent replacement of a protected definition.
Repair followed by refresh and a fresh-process load passed in both skip modes.
No last-known-correction fallback or new policy was implemented in this rehearsal.

The tests exercise sync/reporting/loading services without rendering MAUI. They
do not claim character-tab navigation, snackbar placement, or picker/PDF visual
review. The existing [UI and PDF review](manual-ui-pass-phases-4-6.md) and installed
platform checks remain manual; these three functional lifecycle checks do not.

## Compatibility test alignment — September 24 follow-up

Five older characterization expectations in `CorrectionMetadataCompatibilityTests`
predated correction enforcement: four treated probe correction sections outside
`user/local` as inert during import, and one expected the generic root-namespace
error when using the reserved correction namespace. All five failures were
reproduced against the pinned 0.7.0 package.

The updated tests require misplaced metadata to block refresh in both skip modes
and verify unchanged database and input-file hashes. Separate cases retain the
nonvolatile-row equivalence check for unused namespace declarations and comments,
plus distinct rejection diagnostics for ordinary and correction default namespaces.
Legacy parser/repair compatibility tests remain intact. All **40 focused tests**
across metadata compatibility, correction lifecycle and provenance pass. This
aligns tests with the existing policy; no importer or package change was made.
