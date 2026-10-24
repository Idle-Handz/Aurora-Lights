# v1.0 RC3 source freeze and package validation

Candidate: **1.0.0-rc.3**, on `feature_public-v1.0-build-preparation`.
Windows x64 and Android ARM64 are the intended stable targets. Windows ARM64 and
macOS remain experimental; Web remains deferred.

RC3 follows RC2 (`d58af031e2743ae3c9c2ded9138bc244b6fb5e06`) after its broader test
run exposed a legacy scenario that expected Variant Human to remain bundled.
The scenario now supplies three original synthetic imported definitions, retaining
its actual feat/ability-selector assertions without restoring excluded book content
or skipping the case. The focused scenario group passes three cases; its existing
optional 2024-content case remains explicitly skipped. Production source is unchanged
from RC2; the candidate version and source revision are restamped by the builds.

The source commit/tree, source manifests, dependency versions, package hashes and
validation evidence belong under `artifacts/v1-rc3-2026-10-23/`. Windows x64 and
Android packages must be built from an isolated checkout of this freeze commit.
Earlier RC2 artifacts and its failed full-suite evidence remain separate.

Package acceptance is pending at this freeze. Windows packages are unsigned;
public signing/distribution acceptance and actual published updater delivery remain
separate promotion gates. No push, release tag or public release is authorized by
this local package-validation pass.

Preserve all installed app data. Back up the phone before any in-place update,
verify a matching signer and a higher version code, and compare accessible original
file hashes after installation and testing. Do not uninstall Aurora or clear its
data. Windows interface checks use disposable profiles with only synthetic files
mapped. No pressure tests, driver/firmware changes, device resets or computer
restarts are part of this pass.
