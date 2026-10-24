# Android ARM64 phone acceptance, October 22, 2026

**Subsequent status:** the [newer autosave/export/Drive follow-up](android-followup-acceptance-2026-10-22.md) fixes the custom-feature defect and passes actual export receiving/reopen, fresh consent/callback, offline retry and competing remote revisions on a newer APK. The results and hash below describe the earlier acceptance3 package.

The connected phone passes the core preservation, recovery, navigation, rotation, repeated cached-library refresh and existing-account Drive workflows after an Android Back fix. This is a qualified release-candidate acceptance result. It does not close every stable v1.0 distribution, authentication or device-coverage gate.

Release scope: **Windows x64 and Android stable; Windows ARM64 and macOS experimental; Web deferred.** No commit, push, tag or release publication was performed.

## Earlier acceptance3 package

- Device: Samsung SM-S366V, ARM64 only (`arm64-v8a`), Android 16/API 36; approximately 5.2 GiB OS-visible RAM. The device serial and account identities are omitted.
- Final candidate: `1.0.0-rc.arm64.acceptance3.20261022`, Release `android-arm64`, display version `1.0.0`, version code `214826422`, target SDK 36.
- Signed APK SHA-256: `f5864f21e420774d72f6e70246cdaca8458b243362a19b35ba2e93cb6e87b0d6`.
- Signer SHA-256: `3af0620d0be0e10caba177d0acdc3ac9ddc4b29fe4dff85e56706b363017d66c`, matching the previously installed 0.9.5 package. ZIP integrity passed; all 11 native libraries are AArch64.
- Source: HEAD `53d7a8d7905d18f88ffcdadc62df9aeadf441f50` plus the existing local readiness fixes and the Android Back correction below. `source-provenance.json` records the source hashes. The signed publish passed; broad repository tests were not repeated for this Android integration change.

Evidence is under [the acceptance directory](../artifacts/android-arm64-acceptance-2026-10-22/). Private backups, screen captures and large binaries are excluded from Git by that directory's `.gitignore`.

## Preservation and installation

The phone supports only one switchable Android user, so this was a preservation-first in-place upgrade rather than an isolated-profile or fresh-install test. The original installed APK and a fully readable external-files archive were retained before installation. The archive contains **2,503 files / 477,426,641 bytes**. Its active local character folder contained no `.dnd5e` files or session sidecars; existing Drive characters were not opened for editing.

All package installations used the matching signing certificate and an in-place update. The app was never uninstalled and its data was never cleared. Its data directory and original first-install time remained unchanged. Its connected Drive account survived the upgrades and successfully performed the live cloud test.

After cleanup, a second archive/hash comparison found **all 2,503 original files byte-for-byte identical, zero missing files and zero added files**. This includes all 1,239 original content/index/portrait files. The empty visible local library was restored. Selection autosave, rotation settings and the native Drive app's originally selected account were restored. Only the authorized synthetic cloud file was moved to Drive Trash; its normal 30-day retention still applies.

The corrected candidate remains installed. Private preferences, secure storage and normal app/cloud recovery caches were preserved; Android's release-app permissions do not permit a complete private-data checksum comparison. This is not an uninstall backup of secure storage.

## Executed workflows

| Check | Result and evidence boundary |
| --- | --- |
| Signed upgrade from installed 0.9.5 | Passed through in-place candidate updates with certificate verification and retained installation identity. See `signing-check.json`, `installation*.json` and `package-verification-final.json`. Fresh installation on this phone was deliberately not performed. |
| Two dirty tabs, including an inactive tab | Passed across confirmed background process loss and an in-place candidate update. Both latest names opened as separate recovery copies; B retained HP 5 and inspiration, and A retained Alert. Saved sources retained their checkpoint baseline hashes. The drafts were written by the first acceptance package and read by acceptance2. See `background-process-loss.json` and the recovery captures numbered 69–78. |
| Final-package recovery | Passed independently on acceptance3. A fresh pending name survived Home followed by confirmed `am kill`; a cold launch offered the latest name and opened a separate local file retaining Alert. The saved source hash remained unchanged. See `final-background-process-loss.json`, captures 103–116, and `recovered-files.json`. Alert was already saved before making this new pending edit; this does not certify an unsaved custom-feature mutation. |
| Back, keyboard and close decisions | Passed on the final package. Import-dialog and More-menu Back consumed only the overlay; keyboard Back retained the edit page; browser history ran before root close. Cancel retained the dirty draft, Save persisted it and returned home, and Discard returned home with the saved source unchanged. A final tap on Samsung's actual system navigation Back button also dismissed the import modal without leaving the library. See `final-close-and-export.json`, captures 98–105, 122–138, 250–257 and 263. |
| Rotation | Passed with a pending name through landscape and portrait. The draft remained available afterward and recovered after process loss. Navigation and system-bar insets were visually inspected. The original autorotation settings were restored. Gesture navigation, foldables and multi-window were not tested. |
| Save/discard journal cleanup | Passed. A clean cold start after explicit Save/Discard and test-file cleanup showed no recovery offer. See `cleanup.json` and captures 258–263. |
| Android document picker | The actual Open File / Downloads picker opened synthetic B on the initial acceptance package. This is separate earlier-package evidence, not a full final-hash picker rerun. |
| Android Sheet and export handoff | Final package hid PDF actions and sheet options, displayed the Windows/macOS availability explanation, and opened Android's actual share chooser with the latest pending-name filename. Cancel retained the dirty source. The phone offered Quick Share, Gmail, Drive, Discord and Bluetooth, with no local file-save receiver. No recipient was selected. Receiving and reopening the actual exported payload remains unverified. See captures 131–136. |
| Large cached library | Three Refresh Database cycles passed in the same process in approximately 44, 37 and 44 seconds. All reported up to date; All-types/All-sources searches after cycles two and three each returned **18,458 results**. Observed post-refresh total PSS was 1,082,382 / 1,506,384 / 1,526,687 KiB, settling near 1.46 GiB by cycle three. These are sampled post-operation values, not measured peaks or proof against leaks. No Aurora entry was found in the retained Android crash buffer. See `large-library-refresh.json`, `compendium-counts.json` and app-only memory samples. |
| Live Drive round trip | Passed on the final package using the already connected account and exactly one authorized synthetic file. Aurora uploaded A, saved the distinct `CloudVerified` name, closed it, then reopened that name from Cloud Saves. A separate download through native Drive independently contained the latest name and Alert. Only that file was moved to Trash; Aurora's refreshed active cloud list confirmed its removal. Existing cloud characters remained untouched. See `drive-roundtrip.json` and captures 145–214. This did not test fresh consent. |

The fixtures were synthetic Human/Wizard characters derived from the earlier Windows acceptance fixture. They surfaced an existing partial-load warning about a saved Common choice against the phone's corpus. The named edits, feature, session and recovery values above were checked explicitly; this is not validation of every restored choice or a representative set of personal imports.

## Defect found and fixed

On Android 16/target API 36, Back could dismiss an import dialog and also navigate the page behind it, bypassing the intended overlay-first behavior. Registering a dispatcher callback in activity creation alone was insufficient because BlazorWebView subsequently registered its own history callback above it.

[MainActivity.cs](../Aurora.Reflections/Platforms/Android/MainActivity.cs) now uses a lifecycle-scoped AndroidX Back callback and puts it after the embedded browser's callback when the activity resumes and the WebView initializes. [MainPage.xaml.cs](../Aurora.Reflections/MainPage.xaml.cs) continues accepted root close through the dispatcher with the app callback temporarily disabled. Existing overlay, history and Save/Discard/Cancel handling remains the decision path.

The correction is supported by the executed final-package checks above. Android documents the [API 36 Back migration](https://developer.android.com/about/versions/16/behavior-changes-16); the [MAUI handler source](https://github.com/dotnet/maui/blob/10.0.110/src/BlazorWebView/src/Maui/Android/BlazorWebViewHandler.Android.cs) shows its own dispatcher registration. The earlier acceptance2 callback attempt failed the modal/history check and is not presented as accepted.

## UX finding resolved in the subsequent follow-up

**P2 — custom-feature actions save despite selection autosave being off.** On Build → Extras, adding Alert saved the entire synthetic character, including its pending name, while the UI still indicated autosave off. `BuildService.CustomFeatures.cs` flushes pending edits and calls `SaveCharacterFile`; `Build.razor` marks the tab clean afterward. The setting says disabling it allows experimenting with Build/Magic choices and saving only when satisfied, so its guidance is broader than its behavior.

The subsequent follow-up makes ordinary feature actions follow the autosave policy and verifies add/remove, export and recovery on the phone. This earlier pass recorded the defect without changing custom-feature persistence. Recovery testing established a saved Alert baseline and then made a fresh pending edit; the report does not count the automatically saved feature as an unsaved recovered mutation.

## Remaining readiness work

- Actual export receipt/reopen, fresh consent/callback, cancellation, offline saves and competing revisions were subsequently completed; see the newer follow-up.
- The later [content recovery acceptance](android-content-recovery-acceptance-2026-10-22.md) exercised synthetic new/changed downloads, interrupted transfers, offline use and database refresh/retry against the existing large library. This earlier pass refreshed an existing cache. Disk-pressure and repeated-interruption cleanup still need coverage.
- Broader supported Android versions/vendors, lower-memory devices, lock/unlock, gesture navigation and accessibility samples. The final Back correction was physically tested on this API 36 Samsung; earlier API 33 emulator evidence predates that correction.
- Carry forward the Windows final distribution/update/trust gates in the [Windows acceptance record](windows-shipping-acceptance-2026-10-22.md). The custom-feature autosave inconsistency is fixed and covered in the newer follow-up.

Windows ARM64 and macOS hardware acceptance is required before future stable promotion of those experimental targets; neither is a current v1.0 stable-target gate. Web remains deferred.
