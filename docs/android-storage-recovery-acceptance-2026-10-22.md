# Android temporary-content cleanup and storage-pressure acceptance

October 22, 2026.

Release scope remains **Windows x64 and Android stable; Windows ARM64 and macOS experimental; Web deferred**. Changes are local and uncommitted.

## Candidate and device

- Samsung SM-S366V, ARM64, Android 16/API 36.
- Signed Release android-arm64, informational version `1.0.0-rc.android-storage2.20261022`, display version 1.0.0, version code **214849101**.
- APK SHA-256: `8fda42900a44af420dac2bf561fcd5082040c2455d32a2ed562d66b0ad9a4f45`.
- Signer SHA-256: `3af0620d0be0e10caba177d0acdc3ac9ddc4b29fe4dff85e56706b363017d66c`, matching the installed app. All 11 native libraries are AArch64; ZIP integrity passed.
- Installed in place. Original data-directory and first-install identities retained; Aurora was never uninstalled and its data was never cleared.

Evidence lives in [android-storage-recovery-2026-10-22](../artifacts/android-storage-recovery-2026-10-22/). Private backups, screenshots, APKs and helper build outputs are ignored by Git.

## Changes

Android startup and each database refresh reclaim abandoned `.aurora-candidate-<32 lowercase hex>.sqlite` files and their exact journal/WAL/SHM companions in the primary content root. An OS-held file lock serializes participating imports and cleanup across service instances and processes. The empty lock file stays in place to avoid replacing its locked inode. Process termination releases the lock automatically.

Cleanup opens every member of a candidate group before deleting any. A member that cannot be opened preserves the group; other groups can still be reclaimed. Lookalike names, arbitrary SQLite files, character files, correction archives, nested directories and links are excluded. The app-owned Android content root has one package's writer; desktop candidate sweeping remains disabled.

The shared importer also stages XML copies in `translator-preparation-<32 lowercase hex>` folders under app-private temporary storage. Android cleanup removes only those exact temporary trees, after checking every entry for links. A separate app-wide preparation lock protects live work even when content roots differ. Draft recovery, preferences, secure storage and ordinary caches are outside this sweep. The dependency's correction evaluation, candidate activation and correction-retirement order are unchanged.

Content downloads now dispose their output and remove a partial temporary file in a finally block when writing or replacement fails. The accepted destination and its validators are retained on a failed payload write. An internal stream factory allows bounded storage-failure tests through the real update service without adding a production diagnostic setting.

## Focused checks

**58 desktop tests passed, zero failed and zero skipped.** This includes the earlier 45 content-recovery cases plus 13 new cases for exact candidate/preparation cleanup, active workspace exclusion, different-root exclusion, locked candidate groups, repeated partial writes, marker failure, cache failure and SQLite-full recovery. The signed publish passed; existing compiler/analyzer warnings remain.

**14 isolated checks passed on the physical Android phone.** A temporary separate package linked the production cleanup/downloader sources and the same test cases; it did not access Aurora's files or accounts. The additional Android-only case verified that a link inside a staged tree prevents its deletion and preserves its target.

Three confirmed process-termination cycles in that separate app each released both workspace locks and reclaimed two candidate files plus one staged folder. Every recovery ended with zero candidate files and zero preparation folders. These cycles prove Android lock release and cleanup after real process death; they are distinct from normal managed cancellation.

Storage-pressure methods and boundaries:

| Check | Method and result |
| --- | --- |
| Partial payload write | A byte-capped file stream wrote a prefix then threw an injected ENOSPC-like IOException. Three consecutive failures for both existing and missing destinations left no temporary downloads; accepted bytes and validators remained intact. Removing the fault allowed retry. This is injected failure, not a full filesystem. |
| Refresh marker creation | An owned directory blocked the marker path. Replacement was refused and accepted bytes remained unchanged; removing the obstruction allowed retry. This tests the failure path without exhausting storage. |
| Cache metadata failure | An owned directory blocked the cache entry. Complete payloads and the pending reminder survived; retry repaired metadata after the obstruction was removed. |
| SQLite full | At the importer's pre-activation progress boundary, a connection to its disposable candidate was limited to its existing page count. A write produced actual SQLite error 13, SQLITE_FULL. The exception unwound the real importer, preserving the accepted database or leaving first import absent; retry produced a valid catalog with the expected new definition. This quota applies to the probe connection, not every importer write or the whole filesystem. |

The phone's shared storage was never filled, repartitioned or globally limited. No network, trust, rotation or account settings were needed for these checks.

## Installed-app acceptance and preservation

Before any installation, fully read and hashed a backup of **2,503 files / 477,426,641 bytes**, including the two old orphan candidates. The first storage candidate automatically removed those two files at startup, reclaiming **236,922,328 bytes (about 226 MiB)** with startup downloads off. The accepted catalog remained byte-identical. That earlier package's metadata is retained under `earlier-candidate/`; the final package adds preparation-folder cleanup.

On the final package, **three real database-refresh interruptions passed through the normal Settings > Content > Data action**. Each used one clearly named synthetic XML file to make the content stale. A separate candidate larger than 1 MiB was observed before confirmed process termination. Only the owned synthetic XML was removed before restart. Startup automatically reclaimed the abandoned candidate and journal; every cycle ended with zero candidate files and a byte-identical accepted database. No character tabs were open and no database replacement was allowed to complete.

The separate test app directly verified preparation-folder counts after process death. The installed Aurora checks directly enumerated its external candidate files and hashed its accepted catalog; they did not enumerate its inaccessible private temporary folder.

Final complete comparison: **all 2,501 retained original external files were byte-identical**. The only missing baseline files were the two backed-up orphan artifacts; the only new external file was the empty persistent import lock. Final external count: **2,502 files**, with zero unexpected missing, changed or added files. The original active database and all source/index files were unchanged.

A clean cold start retained the usual account, the empty local character library and no draft-recovery offer. The original compendium returned **18,458 results**. Startup downloads remained off; network and rotation settings were unchanged. The temporary helper app and its isolated data were removed. Aurora was left stopped on the phone's home screen. The byte comparison covers external app files; private preferences and secure storage were retained through the in-place updates, with account/settings checks through the app and Android settings.

## Remaining boundaries

The failure handling above is covered on this API 36 Samsung and in focused desktop tests. The subsequent [memory-pressure acceptance](android-low-memory-acceptance-2026-10-22.md) covers sustained foreground search, editing and background resume on this same package; the [actual low-memory-kill recovery pass](android-lmk-recovery-acceptance-2026-10-22.md) also verifies recovery of two pending drafts after Android reclaimed Aurora. Whole-filesystem ENOSPC during every import/download phase, power loss during final replacement, broader Android versions/vendors and final shipping distribution acceptance remain open. The exact earlier navigation, export and Drive package boundaries remain in their reports. No commit, push, tag or release publication was performed.
