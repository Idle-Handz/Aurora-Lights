# v1.0 RC4 source freeze for Windows updater acceptance

Candidate: **1.0.0-rc.4**, on `feature_public-v1.0-build-preparation`.
RC4 follows RC3 (`7be8e080d9a987aa72b81d742c05cab9ad8222ac`). Its changes
are source-archive exclusions, a release-process note and this freeze record; application code, bundled
content, dependencies and notices are unchanged.

The `.gitattributes` exclusions bring Git/GitHub automatic source archives into
the same boundary as the reviewed corresponding-source bundle. They omit private
attachments, imported character fixtures, historical oracle/third-party binaries,
old correction artifacts and the production publication/signing workflow. Those
files stay in the repository; this changes source archives only. Ordinary build
workflows, licensed assets, offline notices, the LGPL library source and relinking
instructions remain available in the distributable source.

Before publication, compare an archive made from the actual freeze commit with
`aurora-corresponding-source.zip`, including every file's bytes. The only extra
file in the latter should be its generated `SOURCE-MANIFEST.json`. Check the
actual GitHub archive again after an authorized push/tag; local verification does
not establish that a remote archive was downloaded and checked.

This candidate prepares **Windows x64** packages for the published updater trial.
Build from an isolated snapshot of the frozen commit, stamp `1.0.0-rc.4` and its
source revision, and record package/source hashes under
`artifacts/v1-rc4-2026-10-24/`. Windows packages remain unsigned pending the
separate signing/distribution decision. No new Android package, signing-key use
or phone installation is part of this archive-only candidate change.

RC3's application behavior results remain relevant because production source is
unchanged, including its normal-user missing-WebView2 bootstrap acceptance. New
RC4 package integrity and source alignment must be checked separately. Published
updater delivery is still open: exercise an older public installation updating to
the new candidate, immediate restart, deferred installation on close, cancelled
close with pending edits, confirmed close, relaunch and draft/file preservation
using disposable profiles and synthetic characters.

Windows x64 and Android ARM64 remain the intended stable targets. Windows ARM64
and macOS remain experimental; Web stays deferred. Preserve installed app data.
No host restart, driver/firmware change, stress test or device reset is required.
This local freeze does not authorize a push, release tag or publication. Any
approved updater-trial release must be a clearly labelled prerelease, must not
become the latest stable release, and must include matching corresponding source.
