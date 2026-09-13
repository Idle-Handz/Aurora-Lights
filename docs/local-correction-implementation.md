# Local correction lifecycle implementation

Status: implemented in the working tree, September 13, 2026. Not published or
installed into the production content database. Existing unmarked local files
retain their legacy behavior; they are not automatically classified or deleted.

Subsequent policy refinement: a complete correction/group matched by a verified
authoritative download may be automatically accepted after successful import.
The code described here still uses explicit acceptance; it does not yet capture
download evidence or implement this automatic path. Disk-only matches remain
protected. See [the refined policy](local-correction-policy.md).

## XML contract

`Builder.Data/Files/LocalCorrectionDocument.cs` owns the version 1 contract:

```xml
<al:corrections xmlns:al="urn:aurora-lights:corrections:1"
                version="1" source-path="core/example.xml">
  <al:baseline encoding="escaped-xml">...escaped original elements document...</al:baseline>
  <al:correction key="example-fix" operation="replace" target-id="ID_EXAMPLE"
                 state="review-pending" group="example-with-parent-grant">
    <al:reason>Reason for the local repair.</al:reason>
  </al:correction>
</al:corrections>
```

The baseline is the complete original file, enabling identification of unchanged
companion declarations and preserving original references. `Create` writes this
payload as text, not live XML. Active metadata belongs in `user/local`; its
`source-path` is relative to that content root, cannot escape it or point into
`user`, and cannot traverse a symbolic link/junction. The namespace stays local to
the metadata section. Unknown versions, duplicate keys, unsupported operations,
ambiguous original declarations, and partial acceptance of a review group fail
conservatively. The original update URL must still match; an explicitly older
version than the baseline is rejected. File timestamps do not establish authority.

Operations are `replace`, `rename`, `remove`, and `add`. A rename also requires
`replacement-id`. For a collision involving multiple original definitions with the
same ID, `original-fingerprint` selects the exact declaration. Obtain it using
`LocalCorrectionDocument.Fingerprint`; the v1 algorithm hashes a structural JSON
representation with sorted attributes, preserving text (including whitespace).
It is a conservative identity check, not a semantic-equivalence engine.

`review-pending` pins a correction even if upstream has incorporated it. The
explicit review state `accepted-upstream` relinquishes that operation. Related
repairs share `group` and must be accepted together. `AcceptUpstream` requires
hashes of the local and upstream files actually reviewed; it refuses stale reviews
and writes the XML atomically. It is an API for a future review UI, not an automatic
acceptance action. Direct, deliberate XML editing remains possible.

## Effective content and persistence

Evaluation starts with the current authoritative file, applies pinned operations,
and preserves unclassified local edits/additions/removals. An unchanged companion
copy follows upstream, including deletion. Unmarked content that now exactly
matches upstream is redundant. Root attributes and append/other operational nodes
are included in the conservative comparison. Ignored local files do not apply
their corrections or retire automatically.

`Aurora.Importer/LocalCorrectionSync.cs` wraps both Lights importer routes. When
managed corrections exist (or an old mirror needs cleanup), it copies XML into
temporary content roots, materializes effective authoritative files there, and
removes duplicate gameplay declarations from the staged local files. Installed
authoritative and local XML remain untouched. The copied importer and bundled
Translator consume those staged inputs through their existing interfaces.

The candidate database contains normal indexed **effective** elements plus separate
tables:

- `local_override_files`: original local XML, original baseline, incoming upstream
  XML, effective XML, review details, suppressed IDs, and lifecycle state.
- `local_corrections`: individual operations, target identities, fingerprints,
  groups, reasons, and explicit review states.
- `local_correction_inputs`: hashes of actual installed inputs, separate from the
staged effective-file hashes used by the importer.

The ordinary stale check normalizes path separators when reading Translator
snapshots, avoiding a false stale result for Windows backslash paths after the
last managed correction has been removed.

Thus upstream evidence is preserved in full; local corrections are not merely
indistinguishable competing rows. Recreating the database reconstructs active
tracking from XML. A matching mirror supplies runtime effective XML without
reevaluating the baseline; edited files invalidate that cache. The raw XML loading
paths also use the evaluator and suppress obsolete IDs. Unresolvable managed
metadata must not cause fallback to uncorrected XML.

The wrapper copies the current database into a candidate, retaining package
settings, then validates SQLite integrity, foreign keys, and presence of corrected
elements. It checks inputs again before activation and refuses a raced/failed
update. The candidate replaces the working database only after validation. Package
toggles share the app's sync lock. Failed refreshes stop the reload path before it
closes character tabs or replaces loaded elements.

## Protection and retirement

Automatic index downloads refuse to overwrite existing `user/local` XML or a file
containing correction metadata. Generic `ElementsFile.SaveContent` also refuses to
replace existing correction metadata. Explicit review uses its dedicated writer.

Once every correction is accepted and the entire effective file matches upstream
without unique/unclassified local effects, a successful sync retires the local
file by appending `.retired-<unique-id>` to its name. This preserves the complete
file for recovery while excluding it from `*.xml` scans. Retirement occurs after
database activation. A locked file can remain redundant until a later sync retries.
Retired mirror records remain historical; active correction records are removed
when their local file is intentionally removed. Settings displays correction and
retirement status, with detailed reasons under developer/advanced diagnostics.

## Boundaries and next work

- One managed local file per authoritative file is supported. Multiple managed
  overlays on the same origin require consolidation/review rather than an inferred
  ordering. The larger source identity/provenance and unique-ID migration remains
  separate; this change does not add `UNIQUE(aurora_id)` to existing element tables.
- Correction creation and review APIs exist; the interactive three-way diff/editor
  remains roadmap work. The six installed hotfix files now have ten review-pending
  operations; see [the annotation record](hotfix-metadata-annotation-2026-09-13.md).
- The bundled executable itself is unchanged. Lights wraps it; running that raw
  executable independently bypasses the lifecycle wrapper.
- Runtime XML recovery remains supported. The copied importer still has its older
  data contract; shared-library extraction and full reader parity remain separate.
- Staging copies XML when managed corrections are present. Normal importer
  incrementality is retained, but further staging/performance optimization and
  detection of later rollbacks relative to every accepted repository revision are
  future work. Version checks currently compare against the embedded baseline.

## Verification

Commit-readiness follow-up: `RawUserXmlOverlayService` now propagates failures
from managed correction files, including typed element parse failures, rather
than logging/skipping them and returning a partially corrected successful load.
`LocalCorrectionDocument.RethrowManagedRuntimeFailure` enforces that boundary;
unmarked legacy-file skip behavior is unchanged. Two focused regressions cover
managed/unsupported metadata and the preserved legacy behavior.

Annotation follow-up: runtime removals now check the authoritative file path as
well as the suppressed ID. DB/XML loading carries `ElementBase.ContentFilePath`,
and cached runtime corrections return their origin path. This preserves the
legitimate XGTE Staff when the DMG copy's reused ID is repaired. Two provenance
tests plus the 19 lifecycle tests passed, the Windows app build passed, and the
six real annotated files imported through the bundled Translator wrapper with
248 unique elements including both Staffs and three repaired grants. All six
installed annotations were read back and evaluated successfully. Production DB
mirroring remains pending a normal sync; see the annotation record for evidence.

Focused tests are in `LocalCorrectionLifecycleTests`, with the earlier
`CorrectionMetadataCompatibilityTests` and existing `ContentIndexUpdateServiceTests`.
The lifecycle suite can exercise the actual bundled Translator by setting
`AURORA_TEST_TRANSLATOR` to its executable path before running the same tests.
Tests use temporary content roots/databases and cover protected updates, companion
refreshes, duplicates/renames/removals, grouped/stale reviews, unknown metadata,
path restrictions, mirror recreation/cache invalidation, retirement, local removal,
and failed/raced syncs. Production XML and databases are not test fixtures.

Latest executed results: all 36 focused tests passed (19 lifecycle, 12 compatibility,
5 index updater). All 19 lifecycle tests also passed with the actual bundled
Translator selected through `AURORA_TEST_TRANSLATOR`. The Windows app build passed
with zero errors; no commit, push, snapshot publication, or production refresh was
performed. A transient shared build-output lock was resolved by running builds
sequentially; the final Translator regression also verified the path-separator fix.
