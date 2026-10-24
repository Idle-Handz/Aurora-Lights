# Resumed Windows acceptance — October 22, 2026

The owner authorized release-readiness testing to resume after the host crash investigation. **The host stability root causes remain unresolved; this session uses software rendering and ordinary feature checks. No crash reproduction, memory/disk-pressure test, driver/firmware update or host restart was requested.** Windows x64 and Android remain the intended stable targets; Windows ARM64 and macOS remain experimental, and Web is deferred.

**Latest result:** Windows Drive sign-in/restore, synthetic round trip, real revision conflict/recovery, controlled connection failure/retry and cleanup passed on `integrated2`. The active-first native exit interruption was reproduced, repaired and passed on installed `integrated3`; all 19 focused close tests passed. Evidence was exported and the disposable Sandbox was closed normally. Final source/CI, trust/runtime and production-update gates remain open.

## Candidate and isolation

- Initial candidate: `1.0.0-rc.integrated1.20261022`, unsigned and unpublished. The OAuth follow-up below changes one production source file and requires a replacement candidate.
- Setup SHA-256: `5d28b6d911ec3b06c65bf3a91e1c703a6ae981b7c064b7eaba79c099c0743118`.
- All **1,428** recorded source/resource files still matched before launch; the existing guest input's installer hash also matched.
- Fresh Windows Sandbox, Windows 11 Enterprise `10.0.26100`, AMD64, **6,144 MiB** configured RAM. Host free physical memory was approximately **14,648 MiB** before launch.
- Explicit **`vGPU=Disable`**, plus guest-only WebView2 arguments `--disable-gpu`. Microsoft documents software WARP rendering for this Sandbox setting. This reduces use of GPU sharing; it does not prove a host graphics or Bluetooth defect has been fixed. [Microsoft configuration reference](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file).
- Mappings contain only the existing synthetic inputs and separate control, request and evidence folders. Input/control/request folders are read-only inside the guest. The host's real Aurora folders, Google credentials and phone are not mapped or accessed.

## Completed checks

| Check | Result |
| --- | --- |
| Fresh Setup | Exit **0**, exact recorded installer hash; no installation or character directory existed before setup. |
| Cold launch with software rendering | Native Aurora window rendered, with both seeded synthetic characters visible. |
| Drive entry and disclosure | Cloud Saves offered Connect Google Drive; the data-use notice appeared before authorization. |
| Windows system-browser handoff | Continue to Google opened the Google Accounts sign-in page for Aurora Reflections in the guest's Edge browser. This proves browser launch, not completed consent or callback. |
| Focused existing tests | **27 passed, zero failed, zero skipped**: 17 `WindowCloseTests` cases and 10 `GoogleDriveAuthorizationTests` cases. Ran Release binaries with no rebuild/restore and one build node; test duration approximately two seconds. These use synthetic files and fake network responses. |

## Live Drive callback failure and local repair

The owner completed Google login and a second factor, then reported `ERR_CONNECTION_REFUSED` on return. Inspection confirmed the browser returned to `127.0.0.1` inside the guest, Aurora remained running, and Cloud Saves displayed “Cancelled. Any unsent changes remain on this device.” Both synthetic characters and their session sidecars retained their original hashes. No cloud character was created.

The most likely explanation is the desktop authorization listener's three-minute deadline, which included the entire manual login/second-factor/consent flow. On expiry the listener was disposed and the UI treated the timeout as user cancellation. The startup log has no per-authorization timing trace, so the exact elapsed time of the failed callback was not independently recorded; the initial failure cannot be attributed conclusively from timing alone. The subsequent attempt below reached token exchange, demonstrating a validated callback with the replacement package.

The local repair extends the bounded desktop sign-in window to **ten minutes** and reports expiry explicitly: “Google sign-in timed out. Return to Aurora and choose Connect Google Drive to try again.” Caller cancellation still closes the listener immediately. Existing credentials are replaced only after successful authorization and account validation.

**28 focused authentication/disclosure/configuration tests passed**, zero failures/skips, after rebuilding with one build node and no restore. Three new cases advance the OAuth clock without real waiting, exercise real loopback sockets, verify login after four simulated minutes, distinguish timeout from user cancellation, reject late callbacks after disposal, preserve an existing credential, and successfully retry on the same authorization service. The full core suite was not repeated for this narrow change.

A replacement frozen-source Windows candidate, `1.0.0-rc.integrated2.20261022`, was built separately under `artifacts/v1-oauth-recovery-2026-10-22/`. Its manifest differs from the initial candidate in exactly one production file: `Aurora.Logic/Services/Storage/GoogleDriveAuthorization.cs`. The initial installer and source manifest remain preserved. The guest app and expired browser error page were closed normally before replacement. The replacement Setup SHA-256 is `36e4281918267cff3373fc583b5e29b8277d980c967e65c8f9e82aae90a074c8`; it remains unsigned and unpublished. Archive integrity, manifest hashes/sizes, x64 architecture and required runtime payload checks passed. The existing guest installation upgraded with exit **0** and its installed version manifest matches the replacement. Both character files and both sidecars retained their exact hashes. The installed shortcut relaunched Aurora and both character cards rendered. All **1,428** source/resource hashes still match the new frozen manifest.

### Follow-up: callback accepted, token service DNS failure

On the owner-started retry with `integrated2`, Aurora displayed `No such host is known. (oauth2.googleapis.com:443)`. The installed desktop flow reaches that token endpoint only after receiving and validating the loopback callback; this therefore establishes the browser-return step, but **not** token exchange, persistent connection or Drive file access. No credentials or callback codes were extracted for investigation.

The host resolved `oauth2.googleapis.com`, `www.googleapis.com` and `accounts.google.com`. A bounded, read-only guest probe at **00:07:09 UTC on October 23 (5:07:09 PM Pacific on October 22)** also resolved all three. An unauthenticated HTTPS HEAD request to the token host's root completed with `404 Not Found`, establishing DNS/TLS/HTTP reachability rather than OAuth success. No credentials were sent in the probe. This supports a **transient name-resolution failure**, without proving its underlying cause or continued reliability.

The probe used Microsoft's existing Windows Sandbox command interface to run a hidden diagnostic in the already-running guest. It did not reset devices, flush DNS, change network/proxy settings, restart the guest/host, reopen a debugger or run pressure tests. Both synthetic character files and sidecars still matched their original hashes. No additional product code, package build or test run was needed for this diagnosis. The raw DNS warning is a remaining wording improvement for the connection failure path.

The owner was asked to start one fresh connection now that guest DNS/HTTPS checks pass. The existing candidate and browser profile remain in place. Evidence: `artifacts/v1-oauth-recovery-2026-10-22/dns-followup.json` and `google-network-observation.json`.

The subsequent owner-started connection succeeded. The completed live checks below supersede the pending status of the earlier attempts. Credentials are entered only by the owner; the computer-use skill's guidance says, “Do not automate user authentication dialogs.” The guest browser profile is retained for the next attempt. At that handoff Aurora was idle at Connect Google Drive so the deadline did not run while waiting; the owner subsequently started the successful attempt. Refreshing the expired callback page cannot resume the old attempt.

The native-close follow-up reproduced the active-first-tab exit interruption described below; a local repair and focused tests are complete, with replacement-package proof completed below.

Evidence is under `artifacts/v1-final-gates-2026-10-22/windows-resumed-acceptance.json`, `artifacts/v1-oauth-recovery-2026-10-22/`, and the ignored `sandbox/software1/` directory. Changes are local and uncommitted. No push, tag or release was made; source/CI, signing/distribution/runtime and production-update gates remain open. See the [gate reassessment](v1-gate-reassessment-2026-10-22.md).

## Live Windows Drive acceptance completed

These checks passed on `1.0.0-rc.integrated2.20261022` with the owner-selected test account:

| Check | Observed result |
| --- | --- |
| Sign-in and persistent connection | Native Cloud Saves displayed the expected account. Normal app restart restored access without another browser sign-in. |
| Upload/local handoff | Uploaded only `QA_Windows_Drive_20261022.dnd5e`, cloned from a synthetic fixture. The standalone QA character/sidecar were replaced by the cloud workspace after Drive confirmation. |
| Save and download/reopen | Player marker `q`, HP 4, temporary HP 1 and inspiration off survived a real save, library refresh and download/reopen. Workspace files matched their saved receipt. |
| Competing remote revision | Drive web Manage versions updated the same file with player `r`, HP 2, temporary HP 3 and inspiration on. Aurora rejected the stale local save, retained local player `l` and its session state, and created three recovery files. |
| Explicit load after conflict | Load Drive Save loaded the exact replacement payload hash and remote values. The earlier local recovery copy remained. |
| Connection failure and retry | A temporary CONNECT gate applied only to a newly launched guest Aurora process. After a normal baseline, one deliberate blocked connection made Save report failure and retain unsaved player `o` on disk without advancing the receipt. Restoring forwarding allowed the same draft to save and reload from Drive. TLS remained encrypted; no headers/bodies/credentials were recorded. No adapter, DNS, firewall, Windows proxy or host network setting changed. The helper stopped after the check. |
| Cleanup | Moved only the exact synthetic file to Drive Trash, without emptying Trash or changing existing files. A normally launched Aurora restored its connection and showed zero cloud characters. |

The controlled request failure is not a physical network-disconnection or offline cold-start test. The owner completed authentication; automation did not type credentials, extract callback codes or read secure token storage. Original Sandbox A/B fixture files and sidecars remained unchanged throughout the Drive checks. Host app data and phone data remained outside the guest mappings.

Evidence: `artifacts/v1-oauth-recovery-2026-10-22/windows-drive-acceptance.json` and its sanitized state/connection observations; raw owned-character recovery copies are ignored under `sandbox/software1/results/`.

## Native multi-tab exit follow-up

Cancel on the first prompt retained both unsaved tabs and all four original file hashes. With the second tab active, one native close request saved A, prompted for B, discarded B on request and exited. The saved A player marker was `a`; B remained unchanged on disk.

With A active on Manage and both tabs dirty, Save wrote A's new marker `x` and closed it, then navigation to B's Overview interrupted the exit sequence before B's prompt. B stayed open and dirty; another native close still offered its normal decision. No tested draft was silently discarded. This reproduces the earlier observation more narrowly than a general multi-tab failure.

The local change suppresses per-tab navigation for the native window-close callback; ordinary tab-X closure retains its normal navigation. **19 focused WindowCloseTests passed**, zero failures/skips, including two new cases for two initially dirty tabs and the second tab's independent Cancel/Discard choice. These coordinator tests do not render MAUI navigation; the installed-package comparison below covers the observed rendered regression. The separate `integrated3` candidate changes only `MainLayout.razor` relative to `integrated2` and was installed from `artifacts/v1-window-close-recovery-2026-10-22/`.

### Replacement-package proof and controlled finish

The installed `1.0.0-rc.integrated3.20261022` repeated the failing active-first Manage sequence with both tabs dirty. **One native close request** prompted for A; Save immediately led to B's prompt, and Save on B exited the app. Exported XML contained A's new player marker `y` and B's marker `b`, with both session sidecars unchanged. No second native close request was needed. The normal shortcut launch after upgrade also restored the existing Drive connection.

Setup SHA-256: `202908c38eab6b7a90a34d6e802b6523c7f1beb8e59384e7afd618c0059daf7f`. The installer upgraded with exit 0 and preserved all four fixture files at installation. ZIP/update-package integrity, declared hashes/sizes and x64/runtime payload checks passed. All 1,428 source/resource hashes still matched the frozen `integrated3` manifest after native verification. The candidate remains unsigned, uncommitted and unpublished; this is local acceptance, not final release CI.

The app exited normally with zero Aurora processes. The temporary connection helper had stopped and its outage flag was absent. After all raw synthetic observations and recovery copies were exported, the owner-created disposable Sandbox was closed normally; the Sandbox command interface returned **zero environments**. The cloud test file remains in Drive Trash, not permanently purged. Host character/app data and the phone were not touched; no host restart, device reset, driver/firmware change, debugger run or pressure test occurred. This does not establish that the two earlier host defects are fixed.

Final proof: `artifacts/v1-window-close-recovery-2026-10-22/acceptance.json`; the pre-fix observation is recorded separately in `native-exit-before-fix.json`. The full earlier 1,682-case suite and 41 layout checks were not repeated for these narrow follow-ups; affected authentication/close tests, Windows package builds and the named native flows supply the new evidence.
