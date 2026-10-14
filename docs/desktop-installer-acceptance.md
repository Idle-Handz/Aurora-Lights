# Windows desktop installer acceptance

Status as of 2026-10-14: **fresh installation, save/reopen and both diagnostic
update lifecycle checks passed in Windows Sandbox**. The final candidate's fresh
installation and the follow-up safeguard for immediate restart also passed native
acceptance. The final Windows-close safeguard passed focused tests, clean
exit/relaunch, and native dirty-draft/deferred-update checks. NVDA subsequently
exposed a preparation-focus defect; final7 restores focus after autosave and
distinguishes same-named spells by source. The user confirmed audible,
understandable speech. Forward/reverse keyboard traversal and focus after
keyboard preparation autosaves passed the final native NVDA retest.
Missing-runtime bootstrap passed with final7 in a clean Windows 10 LTSC guest.
No host installation was replaced.

## Candidate and isolated environment

- Target: Windows x64 in a disposable VM or Windows Sandbox, using a fresh user
  profile. Restore a VM snapshot or start a new Sandbox for each independent case.
- Candidate version: `0.0.2-localcheck.20261014`.
- Local artifact directory, relative to the repository:
  `artifacts/desktop-release-check-2026-10-14/bin/win-x64-check/final7-packages/`.
- Installer: `Aurora.Reflections-win-x64-Setup.exe`.
- Update package:
  `Aurora.Reflections-0.0.2-localcheck.20261014-win-x64-full.nupkg`.
- Setup SHA-256:
  `67249ff06cb37c11acfc1d1c04c7156a4fe01bbe3ce48c99a870302c3648ae93`.
- Portable SHA-256:
  `076204597c727ff632faeff9b746fbf85b9cb9bd521b5d9108e377e9759ecf12`.
- Full-package SHA-256:
  `e6e0b669dfb4806a40f2035301124e74c10635841932af10cdf7284a078e2d91`.

The final local Release build and package integrity checks passed. These are
unsigned local acceptance artifacts, not a published release.

Copy only the required artifacts and synthetic fixtures into the guest. Do not
map the host's Documents, app-data directories or existing installation, and do
not sign in to personal cloud storage. Create and save test characters inside
the guest. Record the guest OS, package version and hashes, observations, and
installer/updater logs with each result.

### Local Windows Sandbox setup: 2026-10-14

The host has Windows 11 Pro, approximately 32 GB RAM, active Hyper-V services,
and approximately 61 GB free disk space at setup. Windows Sandbox was initially
unavailable. After the user approved Windows administrator elevation, enabling
`Containers-DisposableClientVM` succeeded with `State=Enabled` and
`RestartNeeded=true`. The user subsequently restarted Windows and confirmed
readiness. Sandbox launched successfully after that restart.

The prepared launcher is
`artifacts/desktop-release-check-2026-10-14/bin/sandbox/Aurora-Installer-Checks.wsb`.
It assigns 6 GB RAM, enables networking for WebView2 bootstrap, and disables
clipboard, microphone, webcam and printer sharing. Only the dedicated installer
input folder is mapped read-only; the dedicated `sandbox/results` folder is
writable for evidence. No host Documents, app-data or installation folder is
mapped. The guest preparation helper verifies the candidate's SHA-256 and stages
it under `C:\AuroraAcceptance`; it does not start installation automatically.

The feature result and transcript are in `sandbox/feature-status.json` and
`sandbox/feature-enable.log`. The launcher was exercised after the host restart.
Export relevant logs to the mapped results folder before closing Sandbox,
because closing it discards the guest's other state.

This follows Microsoft's [Sandbox installation guidance](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install)
and [configuration options](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file).

## Release decision

The requested local Windows x64 acceptance checks are complete. The following
results support the release decision within the evidence limits listed below:

- **Small Windows x64 preview:** the completed application, installer, update
  lifecycle, save/reopen, missing-runtime bootstrap and focused screen-reader
  checks support a limited preview.
- **Public installer:** fresh installation, first launch, exit/relaunch,
  installed-app detection and synthetic save/reopen passed. Missing-WebView2 bootstrap
  passed for the exact final7 x64 candidate in the clean guest described below.
- **Public automatic updates:** diagnostic A-to-B data preservation, immediate
  restart and install-on-close, including keeping the prepared update open for
  more than 60 seconds, passed. These are native process-lifecycle checks;
  production GitHub delivery and delta packages remain outside their scope.
- **Screen-reader checks:** actual NVDA speech/state and keyboard traversal
  passed for the tested Magic controls, with human listening confirmed in
  Sandbox. This is scoped acceptance, not certification of every app surface.

## First install and persistence

- [x] From the clean snapshot, run Setup and verify installation completes.
- [x] Launch the installed application; verify the UI becomes usable, then exit
  normally and relaunch from its installed shortcut.
- [x] Create a synthetic character, change notes and session state, save, close
  the application, and reopen it. Verify the saved values persist.
- [x] Record installed-app version and whether the application recognizes its
  Velopack installation. Retain the synthetic save for the upgrade check.

Initial evidence from `final2-packages`: the installed candidate reported
`0.0.2-localcheck.20261014` (plus its source revision). Its Setup SHA-256 was
`ede0d33082ed18b732412c5ab1003801cd9f15c332eea78edc6acfb798f679cd`.
Velopack located the installed manifest and completed the
`--veloapp-install` hook successfully in 2544.9705 ms. Setup logged successful
completion. A guest-created character retained equipment note `x`, temporary HP
`1`, and inspiration `true` after normal exit, installed-shortcut relaunch and
reopening. XML, session JSON, process observations and installer logs were exported
under `artifacts/desktop-release-check-2026-10-14/bin/sandbox/results/`.

After adding the immediate-restart safeguard, the earlier `final3-packages`
candidate passed another fresh installation, usable first launch, normal
exit, and installed-shortcut relaunch. Its install hook completed in 1888.7178 ms.
Evidence is under `sandbox/final3-results`, and its repeatable launcher is
`sandbox/Final3-Install.wsb`. Its Release publish, Setup/portable/full-package build,
release-manifest hash/size checks and archive payload-layout checks passed. The
original save/reopen checks remain prior evidence; the final guard's native
saved-draft check is recorded separately below.

After the Windows-close safeguard, the final unmodified `final5-packages`
candidate passed fresh installation, usable first launch, native close with no
characters open (zero remaining Aurora processes), and installed-shortcut
relaunch. The install hook completed in 2887.803 ms. Evidence is under
`sandbox/final5-results`, with launcher `sandbox/Final5-Install.wsb`. Release
publish, packaging, release-manifest hash/size and archive payload checks passed.

## Upgrade and restart paths

Two diagnostic builds were prepared in a throwaway source copy containing the
current tracked changes and new source files: A `0.0.2-localcheck.20261014` and B
`0.0.3-localcheck.20261014`, both using package ID `Aurora.Reflections`, channel
`win-x64`, and Velopack 1.0.1. These are separate from the unmodified candidate
whose hashes are listed above.

The diagnostic copy replaces only release discovery and package sourcing:
`MauiProgram.cs` registers a local release-metadata HTTP handler, a new
`LocalUpdateRehearsalHandler.cs` reads the diagnostic metadata, and
`AppUpdateService.CreateUpdateManager` uses `SimpleFileSource`. Startup,
`ApplyAsync`, `PrepareApplyOnExitAsync`, `TryApplyPreparedUpdateOnExit`, and the
window close handler remain unchanged. The production checkout has no local-feed
override. No release was published to create this test feed.

Build inputs, source hashes, diagnostic changes, packages, feed metadata, guest
scripts and results are retained under
`artifacts/desktop-release-check-2026-10-14/bin/sandbox/diagnostic-release-check/`.
`immediate.wsb` and `deferred.wsb` use independent fresh guests with read-only
inputs and separate writable evidence folders. Each receives the same synthetic
character and session sidecar created during candidate acceptance.

- [x] Install version A with the saved synthetic character and session sidecar
  in a fresh Sandbox for each following path.
- [x] Upgrade to version B; verify the installed version changes and the
  synthetic character's notes and session state survive reopening.
- [x] From the fresh A installation, use the diagnostic local feed to download B and
  select immediate installation/restart. Verify the old process exits, B starts,
  and the synthetic save remains intact.
- [x] Repeat the A setup, download B for installation on close, and keep the
  application open for more than 60 seconds. Close normally; verify it exits
  without an unexpected restart, then relaunch and confirm B and saved data.
- [x] Inspect logs for callback failures, timeouts or update errors for each
  path. Record failures rather than treating successful packaging as evidence.

The immediate case displayed the updater, automatically relaunched B, and reopened
the character with note `x`, temporary HP `1`, and inspiration `true`. The deferred
case kept the same A process running for **86.2 seconds** between preparation and
the final pre-close snapshot. Normal close installed B with `--norestart`; the
post-update snapshot contained zero Aurora processes. The installed shortcut then
launched B and reopened the same values. Character XML and sidecar hashes were
unchanged throughout both cases.

Evidence is in `results-immediate` and `results-deferred` beneath the diagnostic
directory. Both cases completed `--veloapp-obsolete` and `--veloapp-updated`
callbacks and logged successful package application. Each updater logged a
nonfatal process-wait access-denied warning before continuing successfully; its
exact cause is not established. No callback timeout or failed package application
was observed. The first evidence collector stalled after copying its baseline
files; a corrected helper exported the immediate post-update evidence and every
deferred snapshot successfully. This was a diagnostic helper issue.

## Immediate restart safeguard

Review during acceptance found that immediate updates could terminate with an
unsaved character or active save. Immediate restart now requires all character
tabs to be closed through their existing Save/Discard/Cancel flow. It checks
before downloading and again after download; the final check and restart run in
one synchronous UI callback. Leaving Settings cancels that request's restart.
Clean tabs are also required to close because structural edits can still be
processing without the full-save flag. Install-on-close remains available, with
explicit instructions to save and wait for saves before closing the app.

- [x] **14 focused regression cases passed**, zero failures/skips: existing and
  newly opened tabs, pending session saves, queued editor activity, leaving
  Settings during download, download failure, and successful retry. No broad
  test rerun was needed; the earlier 71 acceptance cases remain prior evidence.
- [x] The final Windows Release publish and local package build passed, compiling
  the Settings and Velopack integration. An independent scoped code review found
  no further correctness issue in this guard.
- [x] In a fresh diagnostic guest with autosave disabled, changing note `x` to
  draft `y` and selecting **Install and restart** displayed the close-tabs message.
  A remained running and the disk still contained `x`.
- [x] **Cancel** in the tab-close prompt preserved the draft and tab. **Save**
  wrote `y` and closed the tab. Retrying with all tabs closed installed B and
  automatically relaunched it. Reopening showed `y`, temporary HP `1`, and
  inspiration `true`; saved XML and sidecar hashes were unchanged by the update.

The final diagnostic source copy, local feed and native evidence are under
`artifacts/desktop-release-check-2026-10-14/bin/sandbox/diagnostic-restart-guard/`,
with snapshots in `results-guard`. Its discovery/source substitutions use the
same approach as the earlier rehearsal; the production restart coordinator and
Settings guard are exercised unchanged. Both update callbacks completed and B
was applied successfully. The same nonfatal process-wait warning was retained in
the exported logs. The subsequent Windows-close safeguard is recorded below.

## Windows window-close safeguard

Closing the main Windows window now uses the same character Save/Discard/Cancel
workflow as closing a tab. Cancel and save failures keep the window open. Existing
full saves, session file writes and character-engine release finish before native
close; repeated tab/window close requests share one prompt. New edits and tabs
that arrive during an awaited operation are checked again. A removed tab whose
engine release is still pending also blocks immediate update/restart. Loading-tab
cancellation keeps its existing behavior.

The native [AppWindow.Closing event](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.closing?view=windows-app-sdk-1.8)
is canceled before asynchronous work starts. Only an accepted close reaches the
existing `Destroying` handler that starts a prepared update.

- [x] **38 focused cases passed**, zero failures/skips: 17 `WindowCloseTests`,
  14 `AppUpdateRestartTests` and 7 `SessionSaveCompletionTests`. The final result is
  `bin/test-results/window-close-release-tests.trx`; earlier intermediate runs are
  retained separately. Independent scoped review found no outstanding issue.
- [x] The final5 unmodified Windows Release publish/package and clean native
  install/exit/relaunch checks passed.
- [x] With autosave off and update B prepared, native close prompted for draft
  `z`; repeated native close retained one prompt. Cancel preserved that draft,
  left disk note `x`, and retained the same A process (8144) **111.3 seconds** after
  the prepared snapshot.
- [x] Retrying native close and choosing Save wrote `z`, exited A and installed
  B without restarting. Installed-shortcut relaunch reopened note `z`, temporary
  HP `1` and inspiration `true`.
- [x] In B, changing the note to draft `w` and choosing Discard on native close
  exited normally. The saved XML and session-sidecar hashes were unchanged and
  the saved note remained `z`.

Final source snapshot, diagnostic feed, logs and native observations are under
`sandbox/diagnostic-window-close-final`, with snapshots in `results-close`.
The only diagnostic source substitutions are the previously documented discovery
and local-feed seams. Both update callbacks succeeded, followed by successful B
package application with `--norestart`. The same nonfatal process-wait warning
appeared; no callback timeout or package failure was observed. At that checkpoint,
no production source changed after those focused tests and packages. The later
accessibility changes and final7 verification are recorded below.

## Missing WebView2 runtime

**PASS, 2026-10-14:** the exact final7 Setup listed above installed its missing
WebView2 prerequisite and launched Aurora's usable character library in a clean
Windows 10 Enterprise LTSC 2021 evaluation guest, build 19044 x64. The official
Microsoft ISO matched Microsoft's published SHA-256. Windows was installed
offline in the dedicated Hyper-V VM `Aurora-Release-WebView2-20261014`, ID
`6ee969fe-d82a-4751-bd07-1cb44593fff4`, using only its local test account.

- [x] Confirm genuine absence while offline and immediately before Setup.
  Both probes returned default-loader HRESULT `0x80070002`, null version, no
  WebView2 registrations or runtime executable, no loader environment/policy
  overrides, and no inspection errors. The second probe ran as the actual
  limited interactive user at `22:16:11.960Z`; hash-verified final7 Setup started
  172 ms later at `22:16:12.133Z` (PID 3792).
- [x] Observe the native missing-prerequisite prompt, approve the download, and
  complete installation. Velopack logged the missing runtime, downloaded the
  official bootstrapper and invoked `inst-webview2-evergreen.exe /install` at
  `15:16:44` local time (UTC-07:00).
- [x] Verify the bootstrapper's successful per-user installation. The user TEMP
  `MicrosoftEdgeUpdate.log` records explicit `/install` PID 724, its handoff to
  PID 2192 with `installsource otherinstallcmd`, `is_machine: 0` and
  `is_interactive: 1`, then worker PID 5528 launching archive
  `{4D2AAD20-ACA7-4190-86EB-4717C805DEF1}` with `--user-level`. The user
  `msedge_installer.log` matches that exact archive and reports first-install
  success/return 0 at `15:17:33`. The updater records installer result 0 and
  exits 0 at `15:17:49`, when Velopack resumes Aurora installation.
- [x] Verify native first launch renders the usable character library. Velopack
  logged successful Aurora installation and launch at `15:17:55`; the later
  probe at `22:18:49.232Z` returned `S_OK` (`0x00000000`) and version
  `155.0.4283.45` for the same guest account.

Evidence is under
`artifacts/desktop-release-check-2026-10-14/bin/missing-runtime-vm/`:
`results-bootstrap` contains the offline/pre-install/post-install probes, final7
hash/start records, Velopack logs and user/machine runtime installer logs.
`results-bootstrap-extra/MicrosoftEdgeUpdate.log` supplies the user handoff,
matching archive and success chain (lines 3, 57-65, 153-157, 166 and 185).

A separate background machine EdgeUpdate began a `core` force-install after
networking was enabled and completed at `15:18:00`. Its archive differs from the
successful per-user chain above. The final HKLM runtime therefore is not solely
attributed to final7; the explicit per-user bootstrap succeeded before that
machine installation completed. This is correlated installer-log evidence, not
a full OS parent-process trace. The guest was shut down with its network
disconnected after final evidence export. With explicit user approval, its VM
registration and generated disk/configuration were then removed, reclaiming
12.57 GiB. Installers, the verified Windows ISO, scripts, logs and synthetic saves
remain preserved; `bin/missing-runtime-vm/disposed-vm-result.json` records cleanup.
No host runtime or installation was changed.

The earlier Sandbox already contained WebView2; its signed runtime uninstaller
returned 93 and left it discoverable. That attempt was not used as absence proof.

## Screen-reader checks

Portable NVDA 2026.2 AMD64 was run inside a fresh Sandbox with the synthetic
Paladin and Cleric fixtures. Its OneCore speech synthesizer was enabled. The
following checks use NVDA's actual generated speech output and Speech Viewer,
not just an accessibility-tree inspection. The user confirmed that the speech
was audible and understandable.

- [x] Tab through the castable, known and prepared search boxes and four filters.
  NVDA identifies each purpose, role and selected value; changing the level
  filter announces the new value.
- [x] Focus the first first-level spell slot and press Space. NVDA identifies
  its position and changes between used/pressed and available/not pressed.
- [x] Expand a spell-level group with Enter. NVDA reports collapsed/expanded.
  Preparation controls announce their spell/profile and checked state; a spell
  blocked by the preparation limit announces unavailable/not checked.
- [x] Verify focus after both unprepare and prepare autosaves on final7.
- [x] Confirm understandable audible playback with a human listener.
- [x] Repeat reverse-Tab traversal between the two same-named source variants.

The first native NVDA pass found a real defect: autosave dropped focus to the
document, and same-named spells from different sources had identical labels.
Preparation labels now include their source. Rows use stable spell/profile keys;
pending changes are guarded without natively disabling the focused checkbox.
Ten focused component cases passed. The final6 native retest confirmed the new
source label but still reproduced focus loss: the layout's page-wide `inert`
save lock also removes focus. The final7 layout helper preserves that save lock
and restores its displaced control only when the same page/character remains
active and the user has not moved focus elsewhere. Seventeen real Chromium cases
passed, covering restoration, consecutive saves, user navigation/focus changes,
stale or unavailable controls, and disposal. Final7 Release publish, packaging,
manifest integrity and payload layout passed. Final7 installed and reached a
usable native UI. NVDA's focus reports at 14:18:19 and 14:18:56 identify the same
original-source checkbox as focused/not checked and focused/checked after its
respective saves. Tab reached the 2024 source and announced its distinct label
at 14:19:32. Reverse-Tab output in that Sandbox run was inconsistent; the
following fresh-guest retest closes the pending navigation check.

In the Windows 10 LTSC guest, the same final7 package passed two complete
forward/reverse traversals with NVDA 2026.2, without bypassing NVDA interception.
Tab moved from the original-source checkbox to its details button and then the
2024 checkbox; Shift+Tab returned to the original details button and checkbox.
The first reverse sequence is logged at 15:23:52-15:24:13 Pacific. Keyboard Space
then prepared and unprepared the original spell; NVDA+Tab confirmed focused/checked
at 15:24:58 and focused/not checked at 15:25:23. A second full traversal, including
NVDA+Tab on the 2024 checkbox, again returned correctly at 15:26:09-15:26:31.
No further production change was needed. The earlier anomaly's cause was not
established, and it did not recur in these two retests.

The final log and exported synthetic XML/session saves are in
`bin/missing-runtime-vm/results-final`; `guest-finished.json` confirms normal
Aurora/NVDA exit and disconnected networking before shutdown. This basic Hyper-V
connection has no audio endpoint, so this retest verifies generated speech and
focus; audible playback remains the user's earlier Sandbox confirmation. The
log retains transient IA2/UIA errors and brief watchdog recoveries during saves.

Focused regression commands:

```powershell
dotnet test Aurora.Tests/Aurora.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~MagicAccessibilityTests|FullyQualifiedName~MagicPreparationAccessibilityTests"
.\node_modules\.bin\playwright.cmd test --config tests/page-focus.config.ts
```

Raw NVDA input/output logs and exported synthetic saves are retained under
`sandbox/runtime-accessibility/results` (first pass) and `results-final6`
(failed focus retest). Both guests were discarded after export. Final7's mapped
speech log and installer-start record are in `results-final7`; that guest ended
before the final fixture/export helper ran. Its logs retain transient IA2/UIA
warnings, brief watchdog recoveries and an unrelated NVDA update-check TLS error;
no blanket claim of error-free screen-reader interoperability is made.

## Outstanding acceptance limits

No requested local acceptance check remains open. The local full-package feed does not
validate production GitHub discovery/downloads, delta
updates, signing/SmartScreen behavior, ARM64, or other Windows versions.

The Velopack packaging warning about `VelopackApp.Run` residing in the Windows
`App` constructor remains: generated MAUI `Main` initializes COM/WinUI first.
The candidate install hook and both diagnostic update callbacks succeeded in the
tested guest. This provides lifecycle evidence without establishing that every
supported environment behaves identically; no startup change was made solely to
suppress the warning.

The original lifecycle cases used already-saved characters; the follow-up
safeguards and their separate acceptance evidence above cover unsaved work.
