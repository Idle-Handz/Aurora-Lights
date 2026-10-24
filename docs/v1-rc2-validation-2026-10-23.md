# v1.0 RC2 source freeze and package validation

Candidate: **1.0.0-rc.2**, prepared on `feature_public-v1.0-build-preparation`.
Windows x64 and Android ARM64 are the intended stable targets. Windows ARM64 and
macOS remain experimental; Web remains deferred.

This freeze contains the LGPL PDF migration, portable fonts, Android Save/Share/Open
actions, the reviewed SRD-only bundled baseline, third-party notices and matching
source/relinking materials. The baseline migration guide describes compatibility
with saved characters and separately imported definitions.

The source commit and tree, dependency versions, source manifests, package hashes
and validation results are recorded under `artifacts/v1-rc2-2026-10-23/`. Packages
must be built from an isolated checkout of the freeze commit. Build version flags
stamp RC2 and that exact revision; Android's version code must exceed the installed
version and retain the existing signing identity.

Validation is pending at the freeze. Earlier acceptance evidence remains associated
with its original source/package; it does not establish acceptance of RC2. A later
results record will distinguish executed checks from remaining distribution gates.
Windows packages remain unsigned until a signing provider is configured. This local
freeze creates no remote push, release tag, prerelease or public release.

Installed characters, imports, account state and preferences must be preserved.
Before an in-place Android update, back up accessible files and the installed APK,
check for pending work, verify the matching signer and compare original file hashes
after installation and acceptance. Never uninstall Aurora or clear its data. Use
synthetic inputs and isolated Windows profiles for runtime checks. No stress runs,
driver/firmware changes, device resets or computer restarts are part of this pass.

See [the bundled-content review](bundled-content-and-assets-2026-10-23.md),
[the baseline migration guide](v1-public-baseline-migration.md), and
[the earlier Android PDF acceptance](android-pdf-export-acceptance-2026-10-23.md).
