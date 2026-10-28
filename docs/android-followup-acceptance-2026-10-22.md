# Android custom-feature, export and Drive follow-up, October 22, 2026

The custom-feature autosave defect is fixed locally. The connected ARM64 phone passed the targeted add/remove, actual export receipt/reopen, inactive-draft recovery, fresh Google consent/callback, competing remote revision and offline-save/retry checks below. This closes those specific gaps; it does not certify every Android device or the final public distribution.

Release scope remains **Windows x64 and Android stable; Windows ARM64 and macOS experimental; Web deferred**. No changes from this task were committed, pushed or published.

## Installed candidate

- Samsung SM-S366V, ARM64 only, Android 16/API 36; existing installation upgraded in place.
- Release android-arm64, informational version `1.0.0-rc.android-followup1.20261022`, display version 1.0.0, version code 214834885, target SDK 36.
- APK SHA-256: `eef0a66d92e4683cb93af3a03fa4589577621744e974f0a052f3f03e5ec9c559`.
- Signer SHA-256: `3af0620d0be0e10caba177d0acdc3ac9ddc4b29fe4dff85e56706b363017d66c`, matching the existing installation. ZIP integrity passed; all 11 native libraries are AArch64.
- The build used audited base 53d7a8d plus the local readiness fixes. Two subsequent commits changed release-note tooling/documentation only. The acceptance evidence records current HEAD and source hashes; no production source has a modification timestamp after this publish completed.

Evidence: [android-followup-2026-10-22](../artifacts/android-followup-2026-10-22/). Private backups, account-restoration details, screenshots, packages and helper build outputs are ignored by Git. See `source-provenance.json`, `package-verification.json` and `installation.json`.

## Fix and regression coverage

Build → Extras now respects selection autosave for ordinary custom features. With autosave off, additions and removals stay in the owning tab, update the engine and mark the tab dirty. Manual Save writes the current tracking list with the character in the same atomic save. An empty pending list correctly means that every custom feature was removed. Successful saves clear the pending list; failed saves retain it.

Tab snapshots, exports and recovery checkpoints include the pending list, so swapping tabs or recovering an inactive draft cannot restore an obsolete saved feature. Repeatable ability increases still stack and remove one instance at a time. Sidekick profile persistence retains its existing separate behavior.

**54 focused tests passed**, covering custom-feature autosave, duplicates, pending snapshot fields and draft recovery. After expanding the repeatable-feature regression to autosave on/off, **both cases passed**. Neither run skipped cases. There were 56 executions and 55 distinct final cases across the two runs. The new autosave test file contains 11 cases, including active/inactive recovery, tab swaps, discard/reopen and save failure/retry. The signed Android publish passed; existing compiler/analyzer warnings remain. A broad repository suite and Windows package acceptance were not repeated for this targeted fix.

## Executed phone checks

| Check | Result |
| --- | --- |
| Add with autosave off | Created a fresh synthetic Aarakocra/Wizard on the phone. Appended a pending name, added the 2014 Alert feat, and confirmed the saved source was byte-identical to baseline while the tab stayed dirty. Manual Save persisted both name and Alert. |
| Remove with autosave off | Appended a second pending name and removed Alert. The saved source stayed byte-identical to the manual-save baseline and still contained Alert. The UI retained the pending removal. |
| Actual Android export | Sheet → Export Character File opened the native share chooser. A temporary receiver accepted the actual granted file URI and saved through Android’s native document picker. The received 497,679-byte file had the latest pending name and no Alert; its SHA-256 is recorded in `custom-feature-and-export.json`. The source remained unchanged. |
| Export reopen | Aurora’s Open File action and the actual Android Downloads picker reopened that received file with the latest name and build. This verifies payload receipt and reopening beyond the earlier chooser/cancel check. |
| Inactive feature draft recovery | With the original removal draft inactive and the clean export tab active, Home followed by confirmed background process termination ended Aurora. Cold launch offered the latest draft. Open Recovery Copy created a separate file with the latest name and no custom feature. The saved original retained Alert. |
| Fresh account consent and cancellation | Used the owner-designated separate account. Cancelling both the native account picker and the fresh Google permission prompt returned Aurora disconnected. Retrying and allowing the files-used-with-this-app grant completed the native callback and connected the intended test account. The usual account grant was never revoked. |
| Competing remote revision | Uploaded one synthetic recovery copy. Android’s Drive document provider allowed an independent writer to change its name to Android Followup Remote Writer. Aurora’s refreshed server preview confirmed that update. Saving the older open tab was blocked with a clear conflict message, retaining the pending LocalConflict name and dirty state. |
| Conflict draft export and reload | Exported and parsed a separate copy of the unsent conflict draft before replacing it. Load Drive Save then restored Remote Writer, demonstrating that the rejected stale save had not overwritten the competing update. |
| Offline failure and online retry | Briefly disabled Wi-Fi and mobile data with unconditional restoration. Save reported a connection failure and retained the OfflineRetry name, dirty dot and unsaved banner. Both original network settings were restored. Retrying online succeeded and marked the tab clean. Closing and reopening from Cloud Saves retained the latest name. |
| Independent Drive download | Native Drive downloaded the same synthetic file. Its XML independently contained Android Followup Remote Writer OfflineRetry and no Alert. The 498,933-byte download and SHA-256 are recorded in `drive-followup.json`. |

The temporary receiver was a separate package, verified absent before installation. It had no network or broad storage permissions, accepted only synthetic character names, and used Android-granted document URIs. It was removed afterward. Android documents the [document picker and URI access workflow](https://developer.android.com/training/data-storage/shared/documents-files). This was a test receiver, not a new Aurora shipping dependency or validation of every third-party sharing app.

The earlier Android Back, rotation and three cached-library-refresh results remain tied to the earlier acceptance3 hash. This follow-up candidate was exercised for the workflows listed here; those earlier stress/navigation checks were not all repeated. The new character was deliberately incomplete beyond its race/class and test edits; this does not certify every character choice.

## Preservation and cleanup

Before updating the phone, retained its installed APK and fully hashed external-files backup. The starting visible local library was empty. Aurora was never uninstalled and its app data was never cleared; installation identity and signing certificate remained intact.

Only recorded synthetic local characters, their test sidecars and received/downloaded test exports were removed. Only the single new synthetic Drive file was moved to Trash; the refreshed Aurora list confirmed its absence. Existing cloud characters were not edited. The usual Aurora connection survived the final cold launch, the native Drive selected account remained unchanged, selection autosave was restored on, network settings were restored, and rotation stayed unchanged.

Final archive comparison: **2,503 original files / 477,426,641 bytes; all byte-identical, zero missing, zero changed and zero added files**. A clean cold launch restored the empty local library with no recovery offer. The updated candidate remains installed, and Aurora was left stopped with the phone on its home screen. Private preferences, secure storage and normal caches were retained; the external-file comparison does not checksum those private stores.

## Remaining release work

- Final shipping artifact and distribution/update acceptance, including Windows production delivery/trust, Windows Drive authorization, and final package provenance after the readiness changes are committed.
- Controlled Android new/changed-content downloads and interrupted/offline recovery were subsequently exercised in the [content recovery acceptance](android-content-recovery-acceptance-2026-10-22.md). Disk-pressure and orphan-candidate cleanup remain follow-up work.
- Broader Android versions/vendors, lower-memory hardware, lock/unlock, gesture navigation and accessibility samples.
- Existing polish observations remain: generic new-character tab labels, and Android’s Recovery Files action displays a folder path rather than opening a file browser. Exporting the open pending draft is verified; browsing that recovery folder was not certified here.

Windows ARM64 and macOS hardware acceptance is required for future stable promotion of those experimental targets. Web stays deferred.
