> Later status, October 22: **RC1 is frozen at `b85ee99`; its branch is pushed and hosted Tests/native builds passed alongside Windows x64 and Android local package acceptance.** Windows distribution acceptance, actual newer-candidate updater delivery and promotion-facing information remain pending. See [RC1 validation results](v1-rc1-validation-results-2026-10-22.md). Windows ARM64/macOS remain experimental; Web remains deferred. Earlier local/uncommitted statements below describe their original checkpoints.

# Aurora Lights v1.0 release readiness review

> Historical audit. For the October 28 RC5 acceptance, public Drive confirmation,
> and remaining stable-release steps, see [current release status](v1.0-release-status.md).
> Findings and intermediate statuses below retain their original dates.

Reviewed on October 21, 2026 against commit `53d7a8d7905d18f88ffcdadc62df9aeadf441f50`.
The working tree was clean at the start of the audit. The implementation update below records subsequent local changes; the original audit findings and evidence are preserved.

**Original audit recommendation: do not tag the reviewed revision as stable v1.0.** It is a credible baseline for a release candidate, but confirmed spell-slot and export defects still affect Reflections on its native targets. The automated checks and release pipeline are substantially stronger than the remaining defects might suggest; the gap is coverage of particular user workflows and platform acceptance, rather than a generally broken solution.

Per the release owner's clarification, **Aurora.Web is an accepted incomplete future target**. Its findings are recorded separately below and do not determine this verdict.

## Implementation update

**Latest local result:** Windows Drive acceptance passed on integrated2, including restore, real conflict/recovery, controlled connection failure/retry and synthetic cleanup. The active-first native exit interruption was reproduced and fixed; all 19 focused close tests and the installed integrated3 sequence passed. Setup SHA-256 is `202908c38eab6b7a90a34d6e802b6523c7f1beb8e59384e7afd618c0059daf7f`. The guest was shut down normally after evidence export. Changes remain local and uncommitted; stable v1.0 still needs final CI/artifacts, distribution trust/runtime checks and published-update acceptance. [Current gate reassessment](v1-gate-reassessment-2026-10-22.md).

**Latest reassessment, October 22:** the earlier combined suite passed 1,682 cases with one explicit optional skip, and all 41 app layout checks pass. A fresh current-source Windows x64 candidate was built and exercised in a clean Sandbox, but a host `0x7E` blue screen interrupted final cleanup. Offline dump analysis located a null-pointer read in Windows' graphics memory manager (`dxgmms2.sys`), without establishing the underlying trigger. Static analysis of the second host crash (`0x9F`) identifies a pending Intel Bluetooth D0 power request at `BTHUSB`. The immediate failure paths differ; no direct shared cause is established. This follow-up used preserved files and cached symbols without reopening a debugger or changing devices. Native acceptance is provisional. The owner later authorized ordinary testing to resume with virtual GPU sharing disabled; debugger runs and crash/pressure reproduction remain paused. The [resumed Windows record](windows-resumed-acceptance-2026-10-22.md) reports a fresh successful launch and 27 initial focused passing tests. The manual Google login returned to a refused loopback connection; the follow-up repairs the likely three-minute authorization expiry and misleading cancellation message, with 28 focused authentication/configuration cases passing. The replacement Windows package built and upgraded the disposable guest successfully, preserving both synthetic characters and sidecars. A subsequent live callback reached token exchange, which failed DNS resolution for Google’s token host. Bounded guest DNS/HTTPS checks subsequently passed; a fresh connection then succeeded. Native Windows Drive upload/save/reopen, persistent account restore, real revision conflict/recovery, controlled request-failure/retry and synthetic cleanup passed on integrated2. A separate active-first multi-tab exit interruption was reproduced; its local navigation repair passed 19 focused tests and the installed integrated3 reproduction. Both pending fields persisted and the original close request exited after both Save decisions. The disposable Sandbox was then closed normally. See the [incident record](windows-test-host-crash-2026-10-22.md). Android's current storage2 source-provenance record still matches. The [updated gate reassessment](v1-gate-reassessment-2026-10-22.md) records the remaining Windows Drive, final-artifact/distribution/update and signing gates. No stable release is cleared by this result.

The release owner requires **Windows x64 and Android to be stable v1.0 targets** and accepts **Windows ARM64 and macOS as experimental for now**. Web remains deferred.

| Finding | Current local status |
| --- | --- |
| R1: multiclass slots | Fixed in the working tree. Magic, Session and the native PDF generator use the engine's combined pool; per-class spell-learning limits stay separate. Full/full, full/half and full/third progressions have regression coverage. |
| R2: Pact Magic | Fixed in the working tree. Pact usage is separate, short rest restores only Pact slots, and long rest restores both pools. Both play-page trackers and sidecar persistence are covered. |
| R3: pending-edit exports | Fixed in the working tree. Character export, PDF export and preview flush the captured tab's pending fields under the existing context/file locks. Exporting a copy leaves the original file and dirty status alone. Recently added custom-feature tracking is also retained. |
| P1 coverage gate | Required SRD/core cases now execute with current identifiers and valid selection workflows. Selection behavior that does not require the 2024 corpus uses embedded content or isolated synthetic fixtures. The genuinely optional 2024 parity case is an explicit runner-visible skip. |
| R4: native lifecycle/draft recovery | Android recovery is implemented locally: per-tab atomic checkpoints include pending character edits and session state, run independently of autosave, and finish a best-available checkpoint on backgrounding without waiting on the engine or cloud. Interrupted work opens as a new local copy. Root Back joins Save/Discard/Cancel after overlays and browser history. Physical ARM64 acceptance now covers signed upgrades, draft recovery, Android 16 Back, rotation, close choices, cached-library refresh and an existing-account Drive round trip; see the phone follow-up below. Mac quit/draft handling remains experimental. |
| R5: Android PDF action | Unsupported PDF actions and sheet options are hidden on Android; the Sheet page explains desktop availability and retains character-file export. |
| R6: actual and temporary HP | Actual HP is capped at its own maximum. Temporary HP changes do not alter actual HP. Legacy over-cap values normalize on load; independent persistence is covered. |
| R7: PDF flattening | Static output avoids partial flattening and now includes a final pass for newly added image fields. Windows package acceptance caught a remaining portrait widget; the corrected installed export and all three static portrait/layout variants have zero fields/widgets. All three interactive variants pass. |
| R8: Mac overwrite | Folder selection now leads to a filename prompt and explicit Replace / Choose another name / Cancel decisions for existing files. Nine platform-independent selection cases pass, including preservation on cancellation. Native Mac dialog acceptance remains open under the experimental label. |
| R9: Android storage guidance | Settings and startup comments now identify app-specific storage, variable file-manager access, removal on uninstall, and the need to export or upload before uninstalling. |

Sidecar migration retains the existing regular-slot dictionary. For an old Pact-only character, its usage is moved to Pact Magic once. For an old mixed caster, usage remains assigned to regular slots and the new Pact tracker starts unused: the old file format cannot identify which pool a spent slot belonged to. Review both trackers once after upgrading a mixed caster.

The earlier blocker batch is recorded under `artifacts/v1-readiness-fixes/`: its production PDF smoke check confirmed pending name/backstory values and the combined 4/3/3 pool, both native Release builds passed, and the full core suite passed 1,616 tests with one explicit optional 2024 skip and no silent passed-test skips. No native device was available at that point. The follow-up results below supersede those earlier totals.

The changes are local and uncommitted. The reviewed R1–R9 implementation gaps are addressed within the selected release scope, with Mac lifecycle work explicitly experimental. Stable v1.0 still requires acceptance of the final shipping Windows x64/Android artifacts and their distribution/authentication paths. The phone follow-up below closes several Android ARM64 gates, while identifying the remaining limits. Windows ARM64 is experimental and does not impose a physical-hardware gate on this v1.0 scope.

### Android recovery and R5–R9 follow-up

Current evidence is under [the follow-up directory](C:/Users/Ralla/source/repos/Aurora-Lights/artifacts/v1-readiness-followup). Both Windows x64 and Android x64 Release builds passed after the final recovery fix. Existing nullable warnings remain. The full core run passed **1,645 tests with one explicit optional 2024 skip**. After the emulator exposed a missing-directory deletion case, that narrow fix and its new regression passed the **final 94-test focused run**; the full suite was not rerun for that last idempotent-delete change. Neither run contains a passed test logging a silent skip. [Test counts](C:/Users/Ralla/source/repos/Aurora-Lights/artifacts/v1-readiness-followup/test-summary.json).

Ten recovery cases cover autosave-independent edits, subsequent edits to an already-dirty tab, session/custom-feature retention, two simultaneously dirty tabs, inactive tabs, busy engine/file locks, capture errors, corrupt records, save/discard cleanup, stale asynchronous captures and first-use cleanup. Two new engine-guard tests verify nonblocking rejection and scope release. Four HP cases cover healing limits, temporary HP independence, zero/overflow behavior, legacy values and sidecar reload. Nine destination-selection cases cover cancellation, explicit overwrite, renaming and invalid names.

A fresh isolated Android API 33 x86_64 emulator ran the corrected Release package, SHA-256 `2aa3db5c4ce40317c2c9788aa0f4f718478cdbc9b61a1cee58a58476b834f378`. With autosave off, an unsaved name survived Home/background followed by `am kill`; PID capture confirmed that the app process ended. On cold launch, the recovery offer contained the latest name and opened a separate local character. The original remained byte-for-byte identical. Native Back traversed browser history before showing Save/Discard/Cancel at the root. Cancel retained the dirty tab; Discard preserved the saved copy and left no recovery offer after another process restart. The Android Sheet page showed the supported character export action and availability explanation, without PDF actions or sheet options. [Acceptance results and exact package](C:/Users/Ralla/source/repos/Aurora-Lights/artifacts/v1-readiness-followup/android-avd/acceptance.json).

The emulator caught an actual first-use error: deleting a clean tab's nonexistent journal raised `DirectoryNotFoundException` before the journal directory existed. Missing-directory deletion is now idempotent and the final regression covers it. The user-facing failure warning was visible during the failure, rather than silently implying protection. The disposable emulator data was removed after preserving screenshots, configuration and results; the existing personal AVD was not used or modified.

Recovery is a local checkpoint, not an uninstall backup. If backgrounding interrupts a locked build/save operation, it uses the last available complete build state plus the latest pending text and session values and labels the offer for choice review. A successful load consumes the journal; a failed open leaves it available. Save and explicit discard remove only that tab's journal, preserving older interrupted-session records. Android does not guarantee `OnDestroy` on process reclamation; the implementation checkpoints accepted edits and completes a local write during `OnStop`. [Android lifecycle documentation](https://developer.android.com/guide/components/activities/activity-lifecycle), [app-specific storage documentation](https://developer.android.com/training/data-storage/app-specific).

Six generated PDF cases cover interactive/static output, spell pages absent/present, and dynamic/legacy spell layouts. Reopened static variants each have **zero fields and zero widgets**. Interactive variants retain 138, 157 and 352 fields/widgets respectively, with canonical values matching the widgets. The name and spell appearances were rendered and visually inspected in both modes. [PDF field verification](C:/Users/Ralla/source/repos/Aurora-Lights/artifacts/v1-readiness-followup/pdf-qa/verification.json).

This emulator result is not acceptance of an ARM64 shipping phone package. Real Drive authorization, large-library stress, Android API/device coverage, rotation and shipping installation/update behavior remain open. Windows package acceptance has subsequently progressed as recorded below. The Mac filename/overwrite policy is unit-tested and its dialog API signatures match the installed MAUI references, but actual Mac dialog and quit acceptance remains experimental. Web remains deferred.

### Windows shipping-package follow-up, October 22

The corrected local x64 installer `1.0.0-rc.acceptance2.20261022`, SHA-256 `3aa77d8ff2b42bee2fdcdfc169958c3dd73ab931ffd1c7146cbf96e770d4e05c`, passed a fresh Windows 11 Sandbox installation and an installer upgrade from public `0.9.9-beta`. The upgrade preserved the character/session files byte for byte; shortcut relaunch reopened the expected name, Human/Wizard build, HP and inspiration. A controlled failed save kept the dirty draft open and the original unchanged; after restoring write access, Save and exit succeeded. Fresh-profile content refresh retained 2,194 compendium results, and production GitHub discovery correctly found 0.9.9-beta without offering a downgrade.

Actual installed export exposed one remaining portrait-image widget when Form Fillable was off. The shared writer now performs a final static-flattening pass after reopening the completed document. All six portrait/background/layout regressions pass; the rebuilt installed app independently produced a four-page export with the latest unsaved name, zero fields/widgets and correctly rendered portrait. The previous full-suite totals remain earlier evidence; only the focused six-case suite and Windows publish/package were rerun for this small desktop PDF fix.

The detailed [Windows acceptance record and physical ARM64 checklist](windows-shipping-acceptance-2026-10-22.md) distinguishes initial-candidate checks from final-hash evidence. Production download/apply/restart into the unpublished candidate, signing/download trust, missing-WebView2 acceptance on the final hash, real Drive authorization and broader native workflow samples remain open. Two minor UX issues were recorded: generic new-tab identity labels and a misleading sharing-conflict explanation for read-only save targets.

That earlier inventory left the Samsung SM-S366V on 0.9.5. The subsequent physical ARM64 pass below installed a matching-signer candidate in place and verified preservation. Windows ARM64 hardware acceptance is deferred under its newly accepted experimental label; macOS remains experimental and Web deferred.

### Android ARM64 phone follow-up, October 22

The signed final phone candidate is `1.0.0-rc.arm64.acceptance3.20261022`, APK SHA-256 `f5864f21e420774d72f6e70246cdaca8458b243362a19b35ba2e93cb6e87b0d6`. It upgraded the API 36 Samsung in place with the existing signing certificate. After synthetic-file cleanup, all **2,503 original external files were byte-for-byte identical**, with zero missing or added files. The existing Drive connection, selection-autosave preference and rotation settings were preserved/restored; no uninstall or app-data reset occurred.

Physical acceptance found a real Android 16 Back-routing defect: the embedded browser could navigate behind a dismissed modal. The final callback ordering fix passes modal, More-menu, keyboard, browser-history and root Save/Discard/Cancel checks, including a tap on the actual system Back control. A new final-package draft recovered as a separate copy after confirmed background process termination and retained its saved custom feature without changing its source. Two active/inactive dirty drafts and session values also recovered across an earlier candidate update; the detailed report distinguishes those package boundaries.

Three existing-library refresh/reload cycles completed in approximately 44, 37 and 44 seconds in the same process. Searches after the second and third each returned **18,458 results**. Post-refresh memory settled near 1.46 GiB on this device after the third cycle; this is bounded observation, not low-memory or leak certification. One authorized synthetic Drive file passed upload, remote save, close/reopen and independent native-Drive download; it was then moved to Trash and disappeared from Aurora’s refreshed active cloud list.

That pass found a **P2 custom-feature autosave inconsistency** and left export receiving, fresh consent and offline/conflict behavior open. The subsequent follow-up below fixes that defect and closes those targeted checks. The [earlier Android acceptance record](android-arm64-acceptance-2026-10-22.md) retains its exact package boundaries.

### Android autosave, export and Drive follow-up, October 22

The newer signed phone candidate `1.0.0-rc.android-followup1.20261022`, version code **214834885**, APK SHA-256 `eef0a66d92e4683cb93af3a03fa4589577621744e974f0a052f3f03e5ec9c559`, passed the targeted follow-up on the same API 36 Samsung. Ordinary custom-feature add/remove now respects selection autosave. Pending feature tracking follows the owning tab through swaps, export and draft recovery, and is committed in the same character save. **54 focused tests passed**, followed by **two passing repeatable-feature cases** after expanding autosave coverage; no cases were skipped. The signed Android publish passed. Earlier full-suite and Windows package evidence was not repeated for this change.

With autosave off, physical add/remove checks kept saved source bytes unchanged until manual Save. An actual shared export was received, saved and reopened through Android’s document picker with the latest pending name and removed-feature state. The inactive removal draft also recovered correctly as a separate copy after confirmed background process loss.

The designated separate Google test account completed fresh Drive consent and the native callback; cancelling both account selection and the permission prompt returned cleanly disconnected. One synthetic cloud file then passed a real competing remote update, blocked stale save with retained local draft, verified conflict export and authoritative reload, offline save failure, online retry, close/reopen and independent native Drive download. The usual account grant was never revoked.

Cleanup restored the usual connection, autosave and network settings, removed the temporary receiver and synthetic local files, and moved only the single synthetic cloud file to Trash. All **2,503 original external files remained byte-identical**, with zero missing/changed/added files. The final cold launch showed an empty local library and no recovery offer; Aurora was left stopped. See the [follow-up acceptance report](android-followup-acceptance-2026-10-22.md) for evidence and remaining limits.

This closed the autosave, actual export receiving/reopen, fresh consent, offline-save and competing-revision gaps. The content-recovery pass below subsequently exercised interrupted/new-content downloads. Broader device/accessibility coverage and the Windows/final distribution gates remain open. Earlier Back/rotation/library stress results remain tied to their earlier acceptance3 hash.

### Android content recovery October 22

The signed `1.0.0-rc.android-content2.20261022` package, version code **214841834**, APK SHA-256 `62e3fd08802a7214061c7da81b7ba69c2ca242228ea20fa273211bbb96495aab`, passed targeted content-recovery acceptance on the API 36 Samsung. **45 focused tests passed with no skips**, and the signed Android publish passed. Two defects were reproduced and fixed locally: well-formed HTML error pages could replace usable XML, and the database-refresh reminder disappeared after restart even though completed downloads still needed activation.

The final package rejected HTML and truncated responses while preserving usable files. Confirmed background process loss during a slow transfer preserved the saved file and the refresh reminder. An offline source check preserved disk files and cached compendium searches remained usable; network settings were restored and the online retry completed both updated files. During database refresh, explicit process termination with a separate candidate in progress left the accepted database byte-identical. The reminder survived, retry activated both V2 definitions with valid database integrity, and a cold-start search retained both updates.

The exact original database and all three original source indexes were restored after the synthetic checks. All **2,503 original external files were byte-identical**, with zero missing/changed/added files; the restored compendium returned **18,458 results**. The account and settings were retained, the temporary HTTPS feed was stopped, and Aurora was left stopped. See the [content recovery acceptance record](android-content-recovery-acceptance-2026-10-22.md) for methods and package boundaries.

The tested file-preservation and catalog-retry paths pass. The subsequent storage pass below addresses automatic orphan cleanup, repeated hard-termination candidate disk usage and bounded storage-failure coverage. Earlier navigation, export and Drive checks retain their own artifact hashes; they were not all repeated on these content updates.

### Android storage cleanup and pressure October 22

The signed `1.0.0-rc.android-storage2.20261022` package, version code **214849101**, APK SHA-256 `8fda42900a44af420dac2bf561fcd5082040c2455d32a2ed562d66b0ad9a4f45`, passed three real refresh-interruption cycles on the phone. Startup reclaimed the separate abandoned candidates each time while leaving the accepted database byte-identical. Cleanup now covers exact candidate/journal names and exact app-private preparation trees, protected by OS-held import locks. Partial download write failures remove their temporary output too. Desktop candidate sweeping remains disabled.

**58 focused desktop tests and 14 isolated physical Android checks passed**, including real SQLite-full errors on disposable candidates, repeated injected partial-write failures, marker/cache failure recovery, active-work exclusion, different-root preparation locking and link protection. Three additional process-death cycles in the isolated app left zero candidates and preparation folders. These bounded checks did not fill the phone's shared storage; whole-filesystem ENOSPC is still an acceptance limit.

The old orphan pair was backed up before automatic cleanup reclaimed **236,922,328 bytes (about 226 MiB)**. All **2,501 retained original external files were byte-identical**; only the empty import lock was added. The final cold start retained the usual account, settings and **18,458** compendium results. The temporary helper was removed and Aurora was left stopped. See the [storage acceptance report](android-storage-recovery-acceptance-2026-10-22.md) for exact methods and package boundaries. The subsequent memory-pressure pass is recorded below. Whole-filesystem ENOSPC, final-replacement power loss, broader devices and final distribution remain open. Changes remain local and uncommitted.

### Android sustained memory pressure October 22

The existing storage2 package passed bounded sustained-pressure acceptance on the same approximately 5.2 GiB API 36 Samsung. A separate temporary app held and repeatedly touched native allocations while independent USB samples recorded live PIDs, available memory and kernel pressure. A higher-load trial caused Android to reclaim that helper with a documented **LOW_MEMORY** exit; Aurora stayed alive. That interrupted trial was excluded from the sustained-hold result.

The accepted foreground hold lasted **370.16 seconds**, with **11 searches** all matching baseline. Editing two synthetic characters with autosave off produced peak 10-second device-wide memory-stall averages of **11.72% some / 11.11% full**, while both saved sources stayed byte-identical. A separate background loop completed at least **180 seconds** under a helper allocation ceiling of 1.5 GiB. Aurora stayed in the same process, and both active/inactive pending names were verified through their actual input fields after return. The inactive field was inspected after pressure release. Thermal status remained normal.

No application regression was reproduced, so no Aurora code change, rebuild or unit-suite rerun was needed. The owned drafts were explicitly discarded, autosave restored, and the helper and fixtures removed. All **2,502 original external files / 240,504,313 bytes** were byte-identical, with zero missing/changed/added files. A final cold start retained the usual account, settings and **18,458** compendium results; Aurora was left stopped. See the [memory acceptance report](android-low-memory-acceptance-2026-10-22.md) for pressure ranges and evidence.

This closes representative sustained-pressure search/edit/background-resume coverage on this phone. Aurora itself was not reclaimed in that pass; the subsequent actual low-memory-kill recovery result follows below. Lower-RAM devices, broader Android versions/vendors, multi-hour leaks, pressure during import/export/Drive, and the existing shipping/distribution gates remain outside this result. Windows ARM64 and macOS stay experimental; Web stays deferred.

### Android actual low-memory-kill recovery October 22

The same storage2 package subsequently passed recovery after Android actually reclaimed Aurora's background process. The matching exit record identifies **PID 12849, reason 3 (LOW_MEMORY), importance 400**, at device time **11:24:24.962**. No explicit Aurora kill, trim simulation, priority change or memory-factor override was used during the trial. A foreground helper reached **2,160 MiB** of native allocations, with peak ten-second kernel memory pressure of **23.83% some / 19.81% full**, and was stopped promptly after Aurora disappeared.

Android created a new Aurora process, **PID 16334**, when the helper stopped. Both active/inactive synthetic drafts were offered and opened as separate recovery copies. Their latest pending names were verified in actual editable fields and XML; both session sidecars were valid. All **175 build elements** in each copy matched the original after normalizing the intended name change and ordinary load-generated selection identities. The inactive draft's existing choice-review warning was retained, and its build comparison also passed. Saved originals remained byte-identical. See the [actual low-memory-kill acceptance report](android-lmk-recovery-acceptance-2026-10-22.md) for cleanup, preservation and exact evidence. No Aurora production change or rebuild was needed. Broader devices and the existing shipping/distribution gates remain separate.

## Release scope and platform assessment

| Target | Current evidence | Assessment |
| --- | --- | --- |
| Reflections Windows x64 | Corrected local installer passed fresh-profile install and upgrade from public 0.9.9-beta, preservation/reopen, failed-save recovery and installed portrait PDF inspection. Six focused PDF cases passed. | Package installation/data preservation look sound. Production candidate update delivery, final distribution/runtime trust, real Drive and remaining workflow samples are still open. |
| Reflections Windows ARM64 | Original audited-commit CI build passed; current local edits have not run in CI or on physical Windows ARM hardware. | Experimental, as accepted by the owner. Native launch, persistence, installer and updater acceptance is required before future stable promotion. |
| Reflections Android ARM64 | Storage2 passes candidate cleanup, bounded storage failures, sustained-pressure searches, two-tab editing/background resume, and two-draft recovery after an OS-confirmed LOW_MEMORY kill on the API 36 Samsung. Content2 and earlier packages retain their download, autosave, export, Drive and navigation boundaries. | The targeted paths pass. Lower-RAM/broader-device and power-loss coverage, whole-filesystem ENOSPC and final distribution acceptance remain open. |
| Reflections Mac Catalyst x64 and ARM64 | Both release jobs passed on the original audited commit. The new overwrite-selection policy is unit-tested; current native Mac build/dialog acceptance was unavailable. | Experimental, as accepted by the owner. Quit/draft handling, external-folder persistence, exports, sign-in and first launch require actual Mac acceptance before stable promotion. |
| Aurora Legacy Windows | Original audit Release build and CI passed. Shared compatibility and presentation tests passed in the audit; the current shared PDF change has core regression coverage. | A viable maintained client. This is not a new full native Legacy acceptance pass. |
| Aurora.Web | Existing browser tests passed; additional failure probes found defects. | Deferred, as requested. Do not present it as a stable hosted multi-user product. |

The requested release boundary is stable Windows x64 and Android, with experimental Windows ARM64 and macOS. The remaining stable-target gates still apply. Physical acceptance is required before either experimental target is promoted to stable.

## Findings affecting the native release

Priority meanings: **P1** should be resolved before stable release within the affected scope; **P2** is a concrete correctness or UX issue to fix before v1.0 or explicitly document and accept. Evidence is identified as an executed reproduction or source trace. Source traces do not establish a completed native-device interaction.

### R1 Multiclass spell slots are understated in Magic and Session

**P1 — executed reproduction — all Reflections platforms.**

A synthetic Wizard 3 / Cleric 3 correctly produces an engine spell-slot pool of **4 first-level, 3 second-level and 3 third-level slots**. The character snapshot retains each class's separate **4/2** progression. The actual shared Magic component then takes the maximum at each level and exposes only **4 first-level and 2 second-level slots**, with no third-level slots. Session uses the same maximum-per-level approach.

This is a mismatch between the correct shared engine calculation and the live-play interfaces. It hides usable slots and upcasting capacity during normal multiclass play.

- [CharacterSnapshot.cs line 710](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Services/CharacterSnapshot.cs:710) reads profile-specific statistics rather than the combined pool.
- [CharacterMagicWorkspace.razor line 746](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Components/Shared/CharacterMagicWorkspace.razor:746) aggregates those values with `Max`.
- [Session.razor line 1151](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Components/Pages/Session.razor:1151) repeats that aggregation.
- [CharacterManager.cs line 994](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Logic/CharacterManager.cs:994) already calculates the correct combined pool.

Use a common slot-pool model derived from the engine's combined progression, preserving the distinction between slot capacity and the spells each class can learn or prepare. Acceptance should include two full casters, full/half/third caster combinations, save/reload, both play pages and PDF comparison. Pact Magic needs its own pool, as described in R2.

### R2 Pact Magic has no separate usage pool or short rest recovery

**P1 — executed state reproduction and source trace — all Reflections platforms.**

Setting two second-level slots as spent and calling the production `SessionState.ShortRest()` leaves both spent. The Short Rest button calls this method directly. It resets death saves and short-rest resources but never restores spell slots, and it receives no information that could identify Pact Magic. Warlock Pact Magic is supposed to recover on a short or long rest. [Official Warlock rules](https://www.dndbeyond.com/classes/7-warlock#PactMagic-388).

There is also no way to distinguish regular slots and Pact Magic slots of the same level: usage is a `Dictionary<int, int>` keyed only by spell level, and the Magic bar combines every casting section. A Warlock multiclass therefore cannot track the two pools independently.

- [SessionState.cs line 58](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Services/SessionState.cs:58)
- [SessionState.cs line 77](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Services/SessionState.cs:77)
- [Session.razor line 1219](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Components/Pages/Session.razor:1219)
- [Magic.razor line 82](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Components/Pages/Magic.razor:82)

Represent pools with their own identity and recovery rule, and migrate existing sidecar usage deliberately. Cover single-class Warlock recovery, mixed pools at the same level, short rest preserving regular spent slots, long rest, both page views and persistence.

### R3 Character and PDF exports can omit pending edits

**P1 — executed reproduction including the generated PDF — native Reflections export paths.**

With an unsaved snapshot containing `LATEST UNSAVED NAME` and `LATEST UNSAVED BACKSTORY`, the PDF generator read the previous engine name and an empty backstory. The generated PDF's name fields and its rendered first page confirm the old name. The character-file export likewise contained the old name.

The character export copies `file.FilePath` directly. PDF generation enters the character context but never applies the snapshot edits before reading `CharacterManager.Current`. This is reachable with autosave disabled; it also matters when an export races a pending save. Users can receive a successful export that does not represent the character currently shown.

- [Sheet.razor line 394](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Components/Pages/Sheet.razor:394)
- [Sheet.razor line 416](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Components/Pages/Sheet.razor:416)
- [Sheet.razor line 462](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Components/Pages/Sheet.razor:462)
- [BuildService.cs line 775](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Services/BuildService.cs:775) provides the existing pending-edit flush mechanism.

Capture a consistent current draft for export under the tab's context and file coordination. Respect the user's autosave preference: exporting need not silently save the original document. Alternatively, explicitly resolve pending edits before export. Cover both formats, autosave on/off, a save already in progress, failures and tab changes.

### R4 The native unsaved close safeguard is wired only on Windows

**P1 for stable Mac support; Android lifecycle acceptance gate — source trace.**

`App.CreateWindow` installs the Save/Discard/Cancel close interception only inside `#if WINDOWS`. Mac Catalyst's AppDelegate has no corresponding quit integration. Android's Back handler dismisses overlays and then delegates to history or the activity; it does not join the character-close workflow. Unsaved character snapshots and pending edits otherwise live in memory.

This leaves a concrete safeguard gap when autosave is disabled or a save has failed. A Mac quit or an Android lifecycle transition must be tested with dirty drafts and pending cloud/session writes. The review did not reproduce native Mac quit or Android process recreation, so those exact device outcomes remain acceptance work. Android backgrounding and forced process death also need a draft-recovery policy; desktop close prompts alone cannot solve them.

- [App.xaml.cs line 32](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/App.xaml.cs:32)
- [MacCatalyst AppDelegate.cs line 5](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Platforms/MacCatalyst/AppDelegate.cs:5)
- [MainPage.xaml.cs line 85](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/MainPage.xaml.cs:85)

For stable Android, add platform lifecycle integration and recoverable drafts, then accept the native behaviors before release. Mac can remain experimental, as accepted by the release owner.

### R5 Android exposes a Save PDF action that always fails

**P2 — deterministic source trace; Android build passed.**

Sheet hides Preview on Android but leaves Save PDF enabled. Its Android generator immediately throws `PlatformNotSupportedException("PDF export is not available on Android.")`. The user therefore receives a failure from a normal enabled action after configuring sheet options.

- [Sheet.razor line 63](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Components/Pages/Sheet.razor:63)
- [MauiCharacterSheetGenerator.cs line 37](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Services/MauiCharacterSheetGenerator.cs:37)

Either implement and accept Android PDF export or gate the action and explain its availability before the user attempts it. The character-file sharing path remains a distinct supported operation.

### R6 Temporary HP raises the allowed actual HP above maximum

**P2 — source trace — all Reflections platforms.**

Session shows Current HP and Temp HP as separate values, but permits Current HP up to `MaxHp + TempHp`. A character with maximum 10 and temporary 5 can therefore hold Current 15 plus Temp 5. Lowering Temp also clamps Current down, changing actual HP as a side effect.

Temporary HP is a separate damage buffer; it does not increase the maximum for actual HP. [Official combat rules](https://www.dndbeyond.com/sources/dnd/basic-rules-2014/combat#TemporaryHitPoints).

- [Session.razor line 190](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Components/Pages/Session.razor:190)
- [Session.razor line 725](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Components/Pages/Session.razor:725)

Clamp actual HP independently to the actual maximum. Cover temporary HP changes, healing, zero HP, persistence and any damage action separately.

### R7 Turning Form Fillable off does not fully flatten caster sheets

**P2 — executed PDF inspection — shared desktop PDF writer.**

The production MAUI generator, with Form Fillable explicitly at its false default, produced a four-page caster sheet with **530 remaining AcroForm fields**. The character-name field remained editable, with its read-only flag unset.

The shared writer initially requests full flattening when form filling is disabled. Dynamic spell pages then supply partial-flattening names; adding those names switches iText into partial flattening and leaves other form fields intact.

- [MauiCharacterSheetGenerator.cs line 52](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Services/MauiCharacterSheetGenerator.cs:52)
- [CharacterSheetEx.cs line 152](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Logic/Models/CharacterSheet/CharacterSheetEx.cs:152)
- [CharacterSheetEx.cs line 224](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Logic/Models/CharacterSheet/CharacterSheetEx.cs:224)

Honor full flattening when Form Fillable is off; use partial flattening only where the selected interactive mode requires it. Acceptance should inspect field trees and page widgets as well as rendering, with spell pages on/off and both spell-page layouts.

### R8 Mac exports can replace an existing file without an overwrite prompt

**P2 — source trace — Mac Catalyst.**

The Mac save-location helper chooses a folder and appends the suggested filename. It does not check whether that filename exists or request overwrite approval. Character export copies with `overwrite: true`; PDF generation opens its destination with `FileMode.Create`. Repeating an export to the same folder can replace a previously exported character or PDF without a Save As overwrite decision.

- [FileSaveService.cs line 100](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Services/FileSaveService.cs:100)
- [Sheet.razor line 442](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Components/Pages/Sheet.razor:442)
- [CharacterSheetEx.cs line 77](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Logic/Models/CharacterSheet/CharacterSheetEx.cs:77)

Use a proper save-file interaction or explicitly confirm replacement and allow a different name. Verify cancellation leaves the existing destination unchanged.

### R9 Android storage guidance overstates file-manager access

**P2 — source trace checked against platform documentation.**

Settings says External Storage makes files accessible from any file manager without ADB/root, and reports that files are now accessible from the Files app. The selected path is actually `GetExternalFilesDir`, an app-specific directory. Android restricts access to other applications' app-specific directories, and app-specific files are removed on uninstall. The startup comment also incorrectly says this directory survives uninstall. [Android app-specific storage documentation](https://developer.android.com/training/data-storage/app-specific).

- [Settings.razor line 203](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Components/Pages/Settings.razor:203)
- [Settings.razor line 2338](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Components/Pages/Settings.razor:2338)
- [CharacterService.cs line 710](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/Services/CharacterService.cs:710)
- [MauiProgram.cs line 29](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Reflections/MauiProgram.cs:29)

Describe the location accurately and direct users to a verified export/backup or cloud workflow before uninstalling. Cloud support improves the available recovery options, but does not automatically protect a local character that was never uploaded.

## Release gate hidden by passing test totals

The fresh core run reports **1,603 passed, zero failed, zero skipped**. Inspection of its TRX output found **20 of those passed cases logged `[SKIP]` and returned early**. These include all four `MulticlassFlowTests`, a complete Human/Fighter/Soldier build scenario, grant surfacing, proficiency/spell save-reload cases, and several 2024 content scenarios.

For example, [MulticlassFlowTests.cs line 36](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Tests/Tests/MulticlassFlowTests.cs:36) returns successfully if its Fighter identifier is absent. This is distinct from `ContentFixture.SkipIfUnavailable`, which already fails when the entire catalog cannot load. The catalog can load successfully while individual scenarios do no work.

**Treat this as a P1 validation gate, not 20 product failures.** Make required baseline scenarios fail when their fixtures are absent; report genuinely optional corpus cases as actual skips. Align the identifiers and content fixtures with the current catalog. Add focused regressions for R1-R3 and distinguish core/SRD coverage from optional 2014/2024/third-party corpus coverage. The current passing total cannot certify all those interactions.

## Verification completed for this review

| Check | Result | Practical limit |
| --- | --- | --- |
| Core Release .NET suite | 1,603 reported passed; no failures or formal skips | 20 passed cases returned early; list preserved in evidence. |
| Windows presentation suite | 5 passed | Presentation compatibility tests, not a full Legacy UI pass. |
| App component layout and interaction suite | 41 passed | Chromium fixture rendering at desktop/narrow sizes, focus, overlays, pickers, equipment/shop and creature management; not native mobile execution. |
| Web workflow suite | 19 passed, 5 mobile cases explicitly skipped | Deferred client; not a native Reflections acceptance result. |
| Release-note compiler | 3 passed | Validates release-note generation, not publication. |
| Reflections Windows x64 Release | Build succeeded, zero errors | Existing nullable warning remains. |
| Aurora Legacy Release | Build succeeded, zero errors | 110 existing compiler/analyzer warnings; no warning-as-error gate was added. |
| Reflections Android x64 Release | Build succeeded, zero errors | Emulator architecture compile/package path; not a phone run or the ARM64 shipping artifact. |
| Native-core targeted probes | Reproduced R1, R2 state behavior and R3; generated and inspected the PDF for R3/R7 | Uses actual production sources/compiled services with isolated test context and a minimal preference stub; not native window automation. |
| Exact-revision GitHub checks | All 17 returned check runs succeeded | Includes repeated runs. Build success is not device acceptance. |

The exact-revision [test workflow](https://github.com/Idle-Handz/Aurora-Lights/actions/runs/39225917902), [Windows builds](https://github.com/Idle-Handz/Aurora-Lights/actions/runs/39225917716), [Android build](https://github.com/Idle-Handz/Aurora-Lights/actions/runs/39225917899), [Legacy build](https://github.com/Idle-Handz/Aurora-Lights/actions/runs/39225918144), and [release workflow including both Mac architectures](https://github.com/Idle-Handz/Aurora-Lights/actions/runs/39120042372) were read live. No workflows were dispatched or rerun by this review.

Initial restricted-environment attempts encountered Windows SDK registry access, test-host connection, browser launch and temporary Git-repository permissions. The successful runs above were completed with the needed execution access. A stale Legacy package restore was refreshed before its successful build. These initial environment failures are not classified as product defects. NuGet vulnerability-feed connectivity warnings do not establish a clean dependency security scan.

## Feature coverage and confidence

| Feature area | Evidence considered | Remaining acceptance focus |
| --- | --- | --- |
| Character library, creation, open, multiple tabs and identity | CharacterService/CharacterContext/tab and write coordination traces; load, identity, collision, pending-edit, close and persistence tests | Current packaged create/edit/save/reopen and failed-load recovery with a representative corpus. |
| Rules, choices, ASIs, prerequisites, advancement and multiclass | Shared resolution/registration, build-tab and progression traces; broad core suite; actual Wizard/Cleric probe | Correct R1/R2; eliminate early-return gaps; test meaningful 2014 and 2024 combinations. The older class-ASI omission is no longer assumed open: current GetAsiEntries includes class-owned choices and fixture coverage exists. |
| Equipment, attunement, hand conflicts, shop, currency and notes | Shared inventory and transaction traces; equipment/shop/note persistence regressions; responsive interaction tests | Packaged purchase/sale, magic templates, tab switches, failure/reload and printed output in representative characters. |
| Magic and live session | Snapshot, shared Magic, slot, preparation, rest and HP traces; accessibility/persistence tests; targeted probes | R1/R2/R6; focus and keyboard checks on the final package; appropriate behavior for each supported rules edition. |
| Companions and sidekicks | Owner/progression, choices, HP and combat-state source review; lifecycle/progression/persistence tests and narrow layout tests | Native multi-creature edits, owner level changes, save/reopen and both phone orientations. No blanket claim of complete sheet parity. |
| PDF and D&D Beyond JSON import | Staged portrait/commit/rollback and multiclass import traces; import/cancellation/collision tests | Realistic sanitized import corpus, diagnostic clarity and current-package cancellation. OCR remains platform-dependent. |
| Content, compendium, source restrictions and Content Doctor | Download/sync/fallback/activation and override-authoring traces; content trust, failure/recovery, alias, filtering and restriction tests | Large current libraries on devices, failed/cancelled refresh, retained working content and restart after upgrade. |
| Google Drive characters | Provider conditional-write/recovery/authentication and host-adapter traces; cloud cross-device, conflict, concurrency, recovery and sign-in tests | Actual release-package sign-in and synthetic save/conflict/recovery on each native platform; no live account data was changed here. |
| Sheets and exports | Production generator execution, PDF field inspection and first-page rendering; output configuration traces | R3/R5/R7/R8; cards, long text, images, multiple casters, both spell layouts and supported desktop viewers. |
| Settings, app updates and installation | Host/bootstrap/release workflow and updater/close-coordinator traces; current release-note and update regression tests; prior installer acceptance record | Fresh final-package installer/update acceptance and production delivery, not only a diagnostic feed. |
| Legacy/shared restored libraries | Current Windows build, shared regression suite and presentation tests; existing compatibility architecture | Representative Legacy/Reflections round trips. Oracle-swapped compatibility scripts were not rerun separately in this review. |

## Platform acceptance still required

Use one fixed candidate revision and record the actual artifact version and hash. Do not combine a new package's build result with an older package's native acceptance and call the new package accepted.

1. **Windows x64:** after R1-R3 and the small correctness fixes, install the actual candidate in a clean profile. Exercise create/import/edit/save/reopen, autosave on/off, failed save, multiple tabs, native close, both exports and a content refresh. Verify update from the previous public version through production GitHub discovery/download and normal restart/deferred-close paths. The October 14 final7 installer results are valuable prior evidence, but refer to a different candidate. See [installer acceptance](C:/Users/Ralla/source/repos/Aurora-Lights/docs/desktop-installer-acceptance.md:15).
2. **Windows ARM64:** experimental for v1.0. Accept the ARM64 installer, WebView2 bootstrap, launch, persistence and update path on ARM64 hardware before future stable promotion.
3. **Android:** accept the signed ARM64 candidate on a physical device, including a large library refresh, repeated refresh/reload, background/resume/process recreation, native Back, dirty drafts, file sharing, Drive authorization, keyboard/safe-area behavior and portrait/landscape navigation. Earlier GC mitigation and refresh evidence is documented in [the Android investigation](C:/Users/Ralla/source/repos/Aurora-Lights/docs/android-gc-investigation-2026-10-03.txt:66); its older package/runtime results are not a current-candidate stress certification.
4. **Mac:** accept both architectures on actual machines. Include first launch/quarantine behavior, Cmd+Q with unsaved work, save failures, folder selection followed by a complete restart, repeated exports and overwrite cancellation, sheet preview, Drive callback and content refresh. The source Info.plist still declares required `arm64` capability while an x64 package is produced; verify the final x64 manifest and launch rather than assuming this source inconsistency proves a runtime failure.
5. **Cloud:** use synthetic characters and a dedicated test account to verify real authentication, stale remote revisions, offline/error handling and recovery in the actual package. Mock-provider regressions establish useful logic coverage, not current Google consent or platform signing configuration acceptance.
6. **Release contract:** refresh the feature comparison and user-facing platform/backup limitations. For example, its cloud-save row still says Reflections has no cloud saves, despite the now-implemented Drive library. The owner has selected stable Android and Windows x64, accepts Windows ARM64 and Mac as experimental, and keeps Web deferred; make the v1.0 notes match that boundary.

## Deferred Web findings

These were reproduced before or while applying the scope clarification. **They do not block the native v1.0 recommendation.** Preserve them for the later hosted target:

- **Imported data is discarded by ordinary edits.** A narrative edit removed an imported `custom-features` node, another extension node and an unavailable inventory item. Web mutation methods construct a fresh CharacterFile instead of retaining the loaded document and unresolved-build recovery state. See [WebCharacterEngineService.cs line 230](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Web/Services/WebCharacterEngineService.cs:230).
- **A failed character switch can overwrite the prior character on the next edit.** Opening a synthetic file missing its currency node threw after resetting the singleton. The session still showed `Stable Original`, while its editable model represented `Broken Candidate`; the next edit wrote that second identity into the first character's file. See [WebCharacterSessionService.cs line 88](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Web/Services/WebCharacterSessionService.cs:88) and [WebCharacterEngineService.cs line 1053](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Web/Services/WebCharacterEngineService.cs:1053).
- **An uploaded portrait path can disclose a server-readable file.** A character referencing a synthetic sentinel outside its browser workspace caused that sentinel's bytes to appear as base64 in the downloaded character after an ordinary save. No real private file was read for the probe. Containing the character file path does not contain embedded portrait paths. Fix this before untrusted public hosting. See [CharacterFile.cs line 907](C:/Users/Ralla/source/repos/Aurora-Lights/Aurora.Logic/Models/CharacterFile.cs:907).
- The existing one-active-browser-circuit engine ownership limit remains a documented hosting constraint; it is not a newly discovered regression.

## Evidence and reproduction

Audit outputs are under [the evidence directory](C:/Users/Ralla/source/repos/Aurora-Lights/artifacts/v1-readiness-2026-10-21). Key records:

- [Native-core probe results](C:/Users/Ralla/source/repos/Aurora-Lights/artifacts/v1-readiness-2026-10-21/app-probe-output/results.json)
- [PDF field verification](C:/Users/Ralla/source/repos/Aurora-Lights/artifacts/v1-readiness-2026-10-21/app-probe-output/pdf-verification.json)
- [Rendered export page](C:/Users/Ralla/source/repos/Aurora-Lights/artifacts/v1-readiness-2026-10-21/app-probe-output/pending-draft-page1.png)
- [Tests that returned early](C:/Users/Ralla/source/repos/Aurora-Lights/artifacts/v1-readiness-2026-10-21/hidden-test-skips.json)
- [Exact-revision GitHub check results](C:/Users/Ralla/source/repos/Aurora-Lights/artifacts/v1-readiness-2026-10-21/github-checks.json)
- [Deferred Web probe results](C:/Users/Ralla/source/repos/Aurora-Lights/artifacts/v1-readiness-2026-10-21/probe-output/results.json)

The review-only console projects preserve the reproduction code under `app-probes` and `probes`. They reference the already-built Release test/Web outputs; build those projects first before rerunning them. The native-core probe also links the production MAUI sheet-generator source and substitutes preference reads with their defaults. All probe characters and the server sentinel are synthetic. Disposable Web workspaces were removed after recording results. No commit, push, release, installed-app replacement or personal-cloud mutation was performed.

This is a solution-wide risk review with full core and presentation test runs, browser/layout/release-note tests and targeted failure probes. It is not proof that every character/content combination or every native platform interaction is defect-free. The concrete stopping point for v1.0 is the findings and platform gates above; a broad rewrite or full Web parity is not required.
