# Shared content library review fixes — September 23, 2026

Follow-up to the review of Lights `6fac657` and Translator `1a4a93a`.
These changes are local and uncommitted; nothing has been published. Live content,
character saves, and the installed database are untouched.

## Implemented

- Library 0.6.1 prevents persisted rejected append operations from being replayed.
  Runtime XML skips only the recorded rejected operation from the same input
  revision, retaining the file's declarations and valid append operations. A
  changed local file is evaluated anew, so an old rejection cannot hide a repair.
- The library owns the current schema/data version values, including the writer's
  metadata and stale check. Lights consumes those values instead of hardcoding
  versions 1 and 12. Unsupported versions still require a refresh; no second
  legacy database reader was introduced.
- Grant parsing trims surrounding reference whitespace for both `id` and legacy
  `name` attributes. Declaration IDs, case, internal whitespace, and raw XML remain
  unchanged. Progression uses the normalized reference.
- PDF identity lookup and parent-race lookup ignore retired database package
  enable flags. Character source restrictions remain a runtime concern.
- Windows release jobs no longer restore the deleted importer. Test and platform
  workflow triggers now include the library pin, vendored feed, NuGet configuration,
  and the relevant DataIntegration project.
- Refresh summaries count whole files and append operations separately. Settings
  reports content issues and describes the existing conflict behavior accurately.
- The manual checklist no longer mentions the removed default-restrictions toggle
  and distinguishes protected infrastructure from selectable rulebooks.

## Policy decisions (superseded September 24)

The questions below record the previous review state. The user subsequently
approved blocking invalid corrections and importing unaffected content on first
installation. The implemented policy and evidence for deferring selective
preservation are in [the September 24 record](content-conflict-policy-2026-09-24.md).
The current package is 0.7.0; the 0.6.1 validation below remains historical evidence
for the preceding fixes.

At the time of the September 23 review, no new conflict-authority or
correction-fallback policy had been implemented.

1. Skip-enabled imports currently retain the first conflicting declaration in
   path order and discard the later file. Should a conflict instead stop activation
   and preserve the working database, or should a separately designed mechanism
   retain the last known definitions while importing unrelated content?
2. Invalid/overlapping correction metadata currently allows the correction file
   to be omitted when skipping is enabled. Should this always stop activation
   until corrected, or should the last known effective correction be carried
   forward? A new installation has no previous correction state to carry forward.

The recommended bounded next step is to keep these two categories blocking even
when malformed standalone files may be skipped. That recommendation is not
silently treated as an approved policy change.

The advanced diff UI, verified-download acceptance, physical canonical SQL
uniqueness migration, and separately recorded character round-trip issues remain
outside this follow-up.

## Validation and package provenance

- Translator: 33 distinct focused scenarios passed across the skip, reader,
  correction, and append filters. Six new projection scenarios and one new schema
  compatibility scenario are included. Three new regressions failed before the
  implementation. The staged test build finished with zero warnings/errors.
- Lights: 75 focused tests passed, none failed or skipped, against the vendored
  0.6.1 package. Coverage includes prepared projection, correction lifecycle,
  source restrictions, progress/reporting, PDF inference, and grant progression.
  Results: `buildtmp/content-fixes-test-results/content-fixes.trx` (local artifact).
- Builder.Data: all 1,362 required legacy API signatures remain available. All
  ten behavior compatibility tests passed against both source and oracle builds.
- Windows MAUI x64 build passed with zero warnings/errors. This is build proof,
  not installed-app/UI validation.
- Six workflow files parsed; trigger coverage and release restore paths checked.
  The test build still reports existing obsolete-API/analyzer warnings separately
  from these successful test results. No full 60-character parity rerun was made.

Both repositories contain uncommitted changes. The vendored 0.6.1 packages were
built from the reviewed Translator changes over base commit `1a4a93a`; the same
source/test/handoff files were then applied to that checkout after checking every
original file hash. The manifest records `dirtySource: true`. This is a local
development package, not a released or clean-commit artifact; follow the existing
immutable-version process when preparing a later clean-source release package.

SHA-256 hashes (also in `vendor/nuget/manifest.json`):

| Package | SHA-256 |
| --- | --- |
| Aurora.Content.Contracts.0.6.1.nupkg | `a3f0ba2d13232d703aaa3e619c60dae6ca7c72e20e3ae135aae65d1d461f1d25` |
| Aurora.Content.0.6.1.nupkg | `50acc4a07182c1acf35ff51d50c76294372f027cc2b4d38c971a285842b39270` |

## Manual verification still required

Use disposable copies for checks that alter character choices or settings.

- Upgrade preference migration before refresh; protected Essentials/Internal and
  selectable rulebooks.
- New characters, saves with no source settings, explicit empty restrictions,
  save/reopen, and sequential switching between characters.
- Source restriction changes removing effective grants/choices, with automatic
  grants restored but cleared choices still awaiting selection after re-enabling.
- Installed Windows application first import, progress, cancellation, restart,
  and skipped-content reporting/repair in both skip modes.
- Test E, Art E, and Testy save/reopen; spell association, preparation/slot/free-use
  behavior, ASIs, and equipment; Fresh E's Legacy round trip.
- Exported PDF layout and values, plus a separate PDF import review.
- Installed Android, macOS, and Windows ARM64 SQLite/import/read smoke checks for
  each platform included in the release.

See [the detailed manual guide](manual-ui-pass-phases-4-6.md). The earlier parity
run compared against a baseline containing known issues; it is not evidence that
those unrelated issues or these manual checks have been resolved.
