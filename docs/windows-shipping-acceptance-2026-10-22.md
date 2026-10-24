# Windows shipping-package acceptance, October 22, 2026

The local Windows x64 candidate passes clean installation, upgrade from the latest public installer, launch, saved-character preservation, native close/save failure handling, and the corrected portrait PDF export. **This is a qualified package acceptance result, not approval to tag stable v1.0.** Production update delivery into this unpublished candidate and Windows Google Drive authorization remain open. Android physical acceptance has progressed in the separate phone report. Windows ARM64 and macOS are experimental; Web is deferred.

## Exact candidate and environment

- Source: HEAD `53d7a8d7905d18f88ffcdadc62df9aeadf441f50` plus the existing uncommitted readiness fixes and the portrait fix described below. `source-provenance.json` records hashes of the changed source files. No commit, push, workflow dispatch, tag or publication was performed.
- Final package: `1.0.0-rc.acceptance2.20261022`, channel `win-x64`.
- Final Setup SHA-256: `3aa77d8ff2b42bee2fdcdfc169958c3dd73ab931ffd1c7146cbf96e770d4e05c`.
- Built with the release workflow's self-contained Windows publish flags, required bundled desktop OAuth configuration and pinned Velopack 1.0.1. OAuth configuration presence was checked without recording its values. No sign-in was attempted.
- Archive integrity, release-manifest hashes/sizes, native x64 main executable and required .NET/Windows App Runtime/WebView2 payloads passed inspection. The portable archive was inspected, not launched separately.
- Three disposable Windows Sandbox profiles ran Windows 11 Enterprise `10.0.26100` on AMD64. Only dedicated input, request and evidence folders were mapped. The host's installed app, character folders and account state were not shared with the guests. Clipboard, camera, microphone and printer redirection were disabled. All characters were synthetic.

Evidence is under [the acceptance directory](../artifacts/windows-shipping-acceptance-2026-10-22/). Package/build logs, hashes, installation manifests, file snapshots and screenshots are retained there. The initial candidate and the corrected candidate have distinct package directories and hashes; their results must not be conflated.

## Executed checks

| Check | Result and evidence boundary |
| --- | --- |
| Final exact-package clean install | Passed. `final-results/guest-ready.json` records no previous installation or character directory; Setup exited 0 and installed the expected version/hash. Desktop and Start Menu shortcuts were created; first launch reached the empty library. |
| Upgrade from public 0.9.9-beta | Passed for the final exact package. The public Setup was downloaded and matched its GitHub SHA-256 `2bd422c89e34b40d5815566452d3e0398f6721d27ea9c5a1faa1570401ccf8ca`. That build opened and saved a Human/Wizard character named `qabc`, then exited. The final Setup upgraded it with exit 0. Both character and session files were byte-for-byte unchanged immediately after upgrade. Shortcut relaunch reopened the same name/build, HP 5, temporary HP 0 and inspiration enabled. See `upgrade-results/02-baseline-saved`, `03-upgrade`, `upgrade-installation.json` and `04-session-after-upgrade.png`. |
| Failed save during native close | Passed on the final exact package. A deliberately read-only synthetic source caused Save to fail. The app stayed open with its dirty draft `qabcd`; the original file hash was unchanged. Restoring write access allowed Save and exit; the saved file contained `qabcd` and no app process remained. See `07-failed-save`, `07-failed-save-retains-draft.png` and `09-saved-after-retry`. |
| Current-draft portrait PDF | Passed on the final exact package. With Form Fillable off and Background/Equipment enabled, native Save As produced four pages containing the pending name `qabcd`. Reopening with pypdf found zero canonical fields and zero widgets. All four rendered pages were inspected: the portrait, name, build and spell-page header remained visible. Export left the original character unchanged and its tab dirty. See `installed-pdf-verification.json`, `upgrade-results/06-corrected-pdf` and `pdf-render/after-*.png`. |
| Fresh-profile content refresh | Passed for the installed bundled-content/no-change path. Refresh reported zero changed files; subsequent compendium search returned 2,194 results. This is not large-library import, download-failure or cancellation stress acceptance. See `final-results/02-refresh-no-changes.png` and `03-compendium-after-refresh.png`. |
| Production GitHub release discovery | Passed for read-only discovery on the final exact package. The installed Updates page found public `v0.9.9-beta` and correctly offered no downgrade from this 1.0 RC. This does not exercise downloading/applying a newer candidate. See `final-results/04-production-release-discovery.png`. |
| Create/edit, autosave off, exports, HP and native close choices | Passed on the initial `1.0.0-rc.acceptance.20261022` package, before the portrait-only correction. Created `qa`, chose Human/Wizard, disabled selection autosave, changed the pending name to `qab`, and exported a character copy with that latest name while the original remained unchanged. Actual HP stayed capped at 6 independently of temporary HP; HP 5 and inspiration survived restart. Cancel retained the draft; Save exited and persisted it; Discard exited without changing the saved file. The final candidate repeats save/failure/portrait export and upgrade checks above; this initial sequence is not presented as a full rerun on the final hash. See `results/02-before-save` through `08-final`. |

## Defect found and fixed during acceptance

The initial installed package's static portrait sheet retained one field/widget, `background_portrait_image:replaced`, on the background page. The earlier six PDF cases omitted that page and portrait, so they missed it. This was an executed package finding, separate from the earlier partial-flattening spell-page defect.

The shared writer now reopens the completed static document for a final full-flattening pass. iText can then flatten image buttons added during the first stamping pass. Interactive output keeps its image field. The six existing layout cases now include a portrait and background page: before the fix, all three static cases failed and all three interactive cases passed; after the fix, all six passed with no skips. The installed final-package export independently confirms the correction and retained portrait rendering. Static export now buffers the first pass in memory; very large card/image-heavy exports were not stress-tested here.

The focused tests and Windows publish were rerun for this change. The full solution suite was not rerun; the preceding readiness follow-up's 1,645-pass core run and final 94-test recovery run remain separate earlier evidence. One restricted test attempt could not connect to its local test process; the executed before/after runs used the required execution access. Existing nullable warnings remain.

## Minor issues observed

- A newly created character tab and its close prompt initially use the generic label `Character`; reopening refreshes the correct saved name. Character data and exports retained the intended name. This is an identity-label polish issue, not an observed save failure.
- A read-only save target produces an error saying another app has the file open. The failure itself is safe and retryable, but that explanation misidentifies this cause. The message should cover permissions/read-only state as well as sharing conflicts.

These were recorded without expanding this package-acceptance change into unrelated UI work.

## Windows gates still open

1. **Published-candidate update delivery:** exercise the actual production release channel from an older public installation into the final published candidate, including download, immediate restart and deferred close with dirty work. The executed Setup upgrade proves installer migration; read-only GitHub discovery and the historical diagnostic-feed updater tests do not certify this unpublished release's complete delivery path. Do not publish a stable release solely to obtain this evidence.
2. **Final distribution trust/runtime checks:** these local packages are unsigned. Browser download/SmartScreen, the chosen release-signing contract, and installation on a supported machine without WebView2 remain unaccepted for the final hash. The October 14 missing-WebView2 and NVDA checks refer to an older package. Any rebuilt/signed final artifact needs its own recorded hash and focused acceptance.
3. **Real Drive authorization and cloud flows:** use a dedicated test account and synthetic characters for consent/callback, upload, reopen, offline failures and revision conflicts. Bundled configuration and mock-provider tests do not prove live consent or callbacks.
4. **Broader workflow samples:** realistic sanitized imports, multiple dirty tabs, large content refresh/failure, long-text/card/image-heavy exports, and final-package keyboard/screen-reader interaction remain useful explicit acceptance items. This pass does not claim an exhaustive rerun of every native feature.
5. **Windows ARM64:** experimental for v1.0, as accepted by the owner. The architecture-specific physical checks below are required before future stable promotion. x64 Sandbox success does not certify that architecture.

## Physical ARM64 checklist

The initial inventory, before the subsequent Android phone acceptance, read the attached USB device on October 22: **Samsung SM-S366V, `arm64-v8a`, Android 16/API 36**, authorized for USB debugging. Installed Aurora is **0.9.5**, version code `213712360`, target SDK 36. No package was installed or removed, no app data was cleared, and no personal character data was read. `android-physical-inventory.json` records only model/platform/app-version metadata, without the device serial.

| Target | Remaining physical acceptance |
| --- | --- |
| Android ARM64, available phone | Signed final ARM64 candidate upgrade and preservation passed; see the subsequent phone report for the exact hash. A fresh install on this personal phone was not performed. |
| Android draft/lifecycle | Two-tab recovery and final-package process-loss/rotation/close/cleanup passed with source hashes preserved. The report distinguishes the earlier cross-package two-tab run from final-package evidence. Lock/unlock and forced low-memory behavior remain untested. |
| Android navigation and exports | Final-package Back, close choices, keyboard, portrait/landscape, PDF-control hiding and native share handoff passed after a Back fix. Receiving/reopening the actual shared export, gesture navigation and broader accessibility remain open. |
| Android content and cloud | Three existing-library refreshes and a live existing-account synthetic Drive round trip passed. Fresh consent/callback, interrupted/offline content downloads, cloud conflicts and broader Android versions/devices remain open. |
| Windows ARM64, experimental; hardware needed before stable promotion | Use the native `win-arm64` Setup and verify native payload/runtime architecture, WebView2 bootstrap, first launch, shortcut relaunch and supported Windows version. Exercise create/import/edit/save/reopen, both exports with portrait/caster, native close/failure handling, content refresh and Drive callback. Upgrade an older ARM64 installation through the production channel and verify immediate/deferred restart and data preservation. This needs Windows ARM hardware; the Android phone cannot cover it. |
| macOS ARM64 | Deferred under the accepted experimental label. Physical first launch/quarantine, quit/draft handling, external-folder persistence, overwrite/cancel and authentication remain prerequisites for a future stable promotion. |

The [subsequent Android ARM64 acceptance record](android-arm64-acceptance-2026-10-22.md) supersedes the phone’s untouched/0.9.5 inventory state above. It records a matching-signer in-place upgrade, preservation of all 2,503 original external files, the Android 16 Back fix, recovery/rotation/close, repeated large cached-library refresh and a cleaned-up synthetic Drive round trip. Remaining Android export receiving, fresh consent and failure/device coverage are called out there. Windows ARM64 and macOS remain experimental, and Web remains deferred.
