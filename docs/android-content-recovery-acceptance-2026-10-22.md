# Android content download recovery acceptance

October 22, 2026.

Android passed the targeted interrupted-download and database-recovery checks after two local fixes: rejecting HTML error pages as content, and preserving the database-refresh reminder across process loss. The original library and app data were preserved.

Release scope remains **Windows x64 and Android stable; Windows ARM64 and macOS experimental; Web deferred**. These changes are local and uncommitted.

## Accepted package

- Samsung SM-S366V, ARM64 only, Android 16/API 36.
- Release android-arm64, informational version `1.0.0-rc.android-content2.20261022`, display version 1.0.0, version code **214841834**, target SDK 36.
- APK SHA-256: `62e3fd08802a7214061c7da81b7ba69c2ca242228ea20fa273211bbb96495aab`.
- Signer SHA-256: `3af0620d0be0e10caba177d0acdc3ac9ddc4b29fe4dff85e56706b363017d66c`, matching the existing installation. ZIP integrity passed; all 11 native libraries are AArch64.
- Installed in place with the original data directory and first-install identity retained. Aurora was never uninstalled and its app data was never cleared.

Artifacts are under [android-content-recovery-2026-10-22](../artifacts/android-content-recovery-2026-10-22/). Private backups, screenshots, packages and temporary server files are ignored by Git.

## Defects fixed

**Content response validation.** A well-formed HTML error page previously passed the XML parser, replaced a usable content file and was reported as an update. A new regression reproduced that failure. Content XML downloads now require the Aurora elements root before replacement or cache updates. Invalid and incomplete responses retain the last usable file.

**Refresh reminder after restart.** Completed downloads survived process loss, but their in-memory refresh reminder did not. After restart, a source check could report no new downloads while the database still contained older definitions. Both startup-download settings reproduced this in tests. The updater now creates a small marker before replacing or retiring active content XML. Startup restores the reminder even when automatic downloads are off; a successful refresh clears it. Database activation and acknowledgement share the update gate so a concurrent download cannot lose its reminder.

**45 focused tests passed, zero failed and zero skipped.** Coverage includes interrupted bodies with existing and missing files, cancellation and connection failure, unchanged cache validators, retry through a new updater instance, startup reminders with downloads on/off, and real database cancellation during preparation, writing and activation. The earlier HTML regression failed before its fix; both reminder regressions failed before theirs. The signed Android publish passed. Existing compiler/analyzer warnings remain.

## Phone results

| Workflow | Result |
| --- | --- |
| New source download | The native Fetch Index action registered a synthetic source and downloaded two new files. A baseline catalog containing both V1 definitions passed SQLite integrity validation. |
| HTML error response | The final package reported one failed file and retained both saved V1 payloads byte-for-byte. |
| Response cut short | The final package retained the usable slow file, accepted the separately completed fast update, reported the failure count and kept a refresh reminder. |
| Process loss during transfer | With a 2,097,392-byte response deliberately slowed, the origin had sent 475,136 bytes when the incomplete transfer was observed. Home followed by confirmed background process termination left the saved slow V1 file intact. After relaunch, the refresh reminder remained visible with automatic downloads off; the origin recorded no startup download requests. |
| Offline update and cached search | With Wi-Fi and mobile data off, the source check reported three failed entries without changing saved files. The cached compendium still returned both V1 definitions. Both network settings were restored in a finally block. |
| Online retry | Retrying downloaded complete V2 files. No manual cache deletion was needed after either interruption. |
| Process loss during database refresh | A separate candidate database and journal were observed while the accepted database remained unchanged. The ordinary background kill left this busy process alive; explicit am force-stop then ended it. The accepted database was byte-identical afterward, and the pending marker and reminder survived relaunch. |
| Refresh recovery | Retrying from the complete downloaded files activated both V2 definitions, passed SQLite integrity validation and cleared the marker. A further cold start and actual compendium search retained both V2 results. |

The baseline V1 catalog was prepared with the earlier content1 candidate. HTML rejection, truncated responses, transfer interruption, offline use, database interruption, successful retry and cold-start results above were executed on the accepted content2 package. Earlier Back, rotation, export and Drive results retain their own package boundaries.

A temporary [Cloudflare Quick Tunnel](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/) supplied HTTPS for a server with only the three fixed synthetic routes. It served no repository or personal files. Normal certificate validation remained enabled; no phone certificates, trust settings or Android helper apps were installed. The server and tunnel were stopped afterward.

## Preservation and restoration

Before installation, retained the installed APK and a fully readable external-files backup. The original three source indexes were temporarily renamed with their bytes verified, keeping normal upstream sources out of the controlled update checks. Their files and source trees were preserved. No character or Drive data was edited.

After acceptance, removed only the synthetic source, its payloads and three cache entries, plus the candidate created by the interrupted test. Restored the exact original database from the verified backup and restored all three original index names. Pre-existing candidate artifacts were preserved.

Final comparison: **2,503 original files / 477,426,641 bytes; zero missing, zero changed and zero added files**. A clean cold launch retained the usual account, showed the original empty local library with no draft recovery offer, and returned **18,458** compendium results. Startup-download preference stayed off, network settings were restored, and rotation remained unchanged. Aurora was left stopped on the phone’s home screen. Private preferences, secure storage and normal caches were retained; the file comparison covers external app files.

## Remaining release work

These results establish recovery of usable files and the accepted catalog through the tested interruptions on this API 36 Samsung. The subsequent [storage cleanup and pressure acceptance](android-storage-recovery-acceptance-2026-10-22.md) covers automatic orphan cleanup, repeated interruptions, bounded write failures and actual SQLite-full recovery. The [memory-pressure pass](android-low-memory-acceptance-2026-10-22.md) covers sustained foreground search, editing and background resume, and the [actual low-memory-kill recovery pass](android-lmk-recovery-acceptance-2026-10-22.md) verifies recovery of two pending drafts on storage2. Whole-filesystem ENOSPC, broader devices and power loss during final filesystem activation retain separate acceptance limits.

At this earlier package boundary, hard termination could leave an unused candidate database on disk. Retry safely ignored the test candidate, which was removed during cleanup, and pre-existing orphan artifacts were deliberately preserved. The subsequent storage pass added automatic candidate and preparation-folder cleanup and backed up the old orphan pair before reclaiming it.

Final shipping-package provenance, distribution/update/trust acceptance and broader native workflow coverage remain in the [release readiness record](v1-release-readiness-2026-10-21.md). Windows ARM64 and macOS remain experimental; Web stays deferred. No commit, push, tag or release publication was performed.
