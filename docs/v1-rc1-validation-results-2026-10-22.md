# v1.0 RC1 source freeze and final package validation

**Local package validation and hosted CI passed on the frozen Windows x64 and Android RC1 source. Stable v1.0 promotion remains pending shipped-library licensing and notices, Windows distribution acceptance, actual newer-candidate update delivery and promotion-facing information.**

Windows x64 and Android are the intended stable targets. Windows ARM64 and macOS remain experimental; Web remains deferred.

## Frozen source and artifacts

- Candidate: **1.0.0-rc.1**.
- Local branch: `feature_v1-rc1-freeze`.
- Commit: `b85ee99091da9a86f70b4b45db24b921eba4c1ea`.
- Git tree: `cf6bc68f83a65d80d631d8a64e48458d0d32b70c`.
- The freeze contains 71 explicit source, test and release-note/document paths. Earlier local evidence and unrelated untracked reports were preserved. No source changes were introduced after the freeze.
- A separate checkout and ZIP snapshot contain **2,423 tracked files**. All recorded source hashes still match after builds and tests. Source-manifest SHA-256: `9cdbc69f4fe1bc9cc656abfba6a6205d02da45e4a179d529d6eae48bd93353da`.
- Built locally with .NET SDK 10.0.402, Release configuration, explicit native architectures and bounded serial builds. Resolved dependency versions are recorded in `resolved-dependencies.json`.
- Windows binary informational version is `1.0.0-rc.1+b85ee99091da9a86f70b4b45db24b921eba4c1ea`; display/file versions are 1.0.0 / 1.0.0.0. Windows Setup remains unsigned.
- Android version code is **214881890**, greater than the installed 214849101; display version is 1.0.0. The existing signing key was reused with the owner's explicit permission. The build helper added no password literals; it reused the existing release configuration through process-only environment references. Existing ignored signing material was preserved.

The shipping artifact paths below are relative to `artifacts/v1-rc1-2026-10-22/`. Their full manifest is `final-artifact-manifest.json`; build intermediates are excluded from that shipping list.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| packages/win-x64/Aurora.Reflections-1.0.0-rc.1-win-x64-full.nupkg | 155016203 | `0d08abc4a542152d159789db1eee0fd20ce842133580c21739fa8e03ee3e3574` |
| packages/win-x64/Aurora.Reflections-win-x64-Portable.zip | 155015138 | `14f7a0ce4f8ef76a3ad95294e4fb24588e98bc41980bfee22efe969e709b4ffd` |
| packages/win-x64/Aurora.Reflections-win-x64-Setup.exe | 159474699 | `9aea00da82a45c99fe5c9d66850498336772aead93ab169e163706afe7271cc3` |
| publish/android-arm64/com.auroralights.app-Signed.apk | 67735813 | `e932d614f3eb52130e537987b09f2929aee26e8f2fc531bde6d058626be4e503` |

Private backups, source snapshots, packages, screenshots and guest files are ignored by Git. The owner subsequently authorized pushing the frozen branch. `feature_v1-rc1-freeze` now points to the same commit on GitHub. Tests and native build workflows started automatically from that push; no manual workflow dispatch, tag or release was created. Hosted CI evidence is collected in `artifacts/v1-rc1-2026-10-22/ci/`.

## Checks on this exact source revision

| Check | Result |
| --- | --- |
| Core Release suite | **1,687 passed, zero failed.** One explicit optional 2024-content skip; no passed case logged a silent skip. |
| Presentation Release suite | **5 passed**, zero failed/skipped. |
| App layout and interaction suite | **41 passed**, using headless Chromium with GPU disabled. |
| Legacy API comparison regressions | Passed. |
| Required legacy APIs | Builder.Core 65, Builder.Data 1,362, Aurora.Documents 166 and Aurora.Presentation 23 required signatures preserved. Builder.Data's five additions are allowed. |
| Legacy behavior comparisons | Builder.Data, Aurora.Documents and Aurora.Presentation passed against both restored source and the recorded production assemblies. |
| Release-note tooling | **3 compiler tests passed**; required fragments compiled successfully from public v0.9.9-beta to this frozen commit. |
| Windows package inspection | ZIP/full-package integrity, declared release-manifest hashes/sizes, x64 main executable and required runtime payloads passed. No loose desktop OAuth configuration or scratch build output. |
| Android package inspection | Signature verification passed; package ID and higher version code correct; min API 24, target API 36, non-debuggable. All 11 native libraries are AArch64. ZIP integrity, APK alignment and at-least-16-KiB ELF load-segment alignment passed. These are structural checks, not a physical 16-KiB-device trial. |

The Web browser suite was deferred during local validation. The existing hosted Tests workflow subsequently ran its browser regressions successfully; Web remains a deferred release target. Builds retain existing nullable/analyzer warnings. An initial local parity runner selected an older PowerShell runtime that could not find Get-FileHash; the runner was corrected to PowerShell 7 and the failed step and remaining checks passed. Successful checks were not repeated. Two evidence-parser assumptions about emitted field names were corrected after inspecting the actual manifest/XML. These helper corrections required no production-source change.

## Windows runtime acceptance

Three fresh disposable Windows 11 Enterprise 10.0.26100 AMD64 guests ran **sequentially**, with virtual GPU sharing disabled, a 6,144-MiB guest limit and WebView2 GPU use disabled. Only dedicated synthetic inputs, requests and evidence folders were mapped. Host Aurora data, personal character directories and credentials were not mapped.

- **Exact RC1 clean install:** no existing Aurora installation or character directory; Setup exited 0 and created shortcuts. First launch reached the synthetic library. Settings displayed the expected RC1/commit version. The bundled compendium returned **2,194 entries**. Normal exit left zero Aurora processes.
- **Upgrade from public v0.9.9-beta:** the cached public installer was reverified against its recorded SHA-256 `2bd422c89e34b40d5815566452d3e0398f6721d27ea9c5a1faa1570401ccf8ca`. It opened an existing synthetic Human/Wizard character. After normal exit, RC1 Setup upgraded it with exit 0. Both character files and both session sidecars were byte-identical immediately after upgrade. The installed shortcut reopened RC1 and both characters remained available.
- **Affected native exit workflow:** with autosave off, both tabs held distinct unsaved player markers. The first tab was active on Manage. All four original character/session files remained unchanged before Save. One native close request prompted for A and then B; choosing Save for each exited the app. Exported XML retained A=`a` and B=`b`; both session files remained unchanged and zero Aurora processes remained. Physical key events were used to confirm visible markers after remote paste-based input did not reach the guest.
- **Portable archive:** the exact ZIP was extracted into a fresh guest and launched through its root launcher, without using Setup. The library loaded, a saved Human/Wizard character opened with the expected name/build and maximum HP, and normal exit succeeded. All four synthetic source files remained unchanged.

The clean guest already had WebView2 155.0.4283.62. **Installation with WebView2 absent remains untested.** These local mapped packages did not exercise Internet-download security prompts or a published newer update. The earlier live Drive acceptance remains associated with its original integrated2 package; production text was compared with integrated3 and is unchanged apart from Git-normalized line endings across all 1,428 earlier recorded build inputs. Drive was not signed in or mutated again in this RC1 pass.

## Android runtime acceptance and preservation

The connected Samsung SM-S366V ran Android 16 / API 36, ARM64. RC1's signer SHA-256 is `3af0620d0be0e10caba177d0acdc3ac9ddc4b29fe4dff85e56706b363017d66c`, matching the previous installed APK. APK v2/v3 signature verification passed.

- Before mutation, Aurora was not running. The installed APK and **2,502 accessible external app files / 240,504,313 bytes** were backed up into ignored private evidence.
- The exact signed RC1 APK upgraded the existing installation in place. The data-directory identity and first-install timestamp remained unchanged. Aurora was never uninstalled or data-cleared.
- Immediately after installation, all **2,502** original files were present and byte-identical; no new external files appeared.
- Cold launch reached the library, retained the existing connected account, preserved the originally empty local library and offered no unexpected draft recovery. Content search returned the original **18,458 entries**.
- After startup/search and returning the phone to Home, all **2,502** external files were again byte-identical, with no missing, changed or new files. Aurora was left not running.

Private preferences and secure storage cannot be byte-inspected through ordinary ADB on this Release build. Their preservation is supported by the in-place installation identities, no data-clearing operation and retained account/UI state; the byte-for-byte claim applies to the accessible external files only. Existing Drive characters were not changed. No export, Drive mutation, download interruption or low-memory pressure scenario was repeated on RC1; their earlier physical acceptance retains its original package boundaries.

## Hosted CI completion, October 22

The owner-authorized push published only `feature_v1-rc1-freeze` at the frozen commit `b85ee99091da9a86f70b4b45db24b921eba4c1ea`. Existing push triggers started the workflows automatically. All completed successfully; no retry, source change, manual workflow dispatch, release tag or release was needed.

| Workflow | Result |
| --- | --- |
| [Tests](https://github.com/Idle-Handz/Aurora-Lights/actions/runs/39886743730) | **Passed:** 1,687 core tests, five presentation tests, 19 browser regressions and 41 App layout checks. One existing optional 2024-content skip. API/behavior compatibility and release-note tooling steps passed. |
| [Windows MAUI builds](https://github.com/Idle-Handz/Aurora-Lights/actions/runs/39886743743) | **Passed:** win-x64 and win-arm64. ARM64 remains experimental. |
| [Android build](https://github.com/Idle-Handz/Aurora-Lights/actions/runs/39886743719) | **Passed:** android-arm64 publish, signing verification and artifact upload. |
| [Legacy Windows build](https://github.com/Idle-Handz/Aurora-Lights/actions/runs/39886743691) | **Passed.** |

CI's Android signing certificate matches the accepted phone package: `3af0620d0be0e10caba177d0acdc3ac9ddc4b29fe4dff85e56706b363017d66c`. The CI APK has version code 214883791 and display version 1.0. The phone retains the separately accepted, stamped RC1 package (version code 214881890). No CI artifact was installed on it.

The remote branch still matches the frozen commit, and all seven recorded local shipping-artifact hashes still match. CI outputs remain separate from the accepted RC1 package hashes. Evidence: `ci/summary.json`, `ci/remote-ref.json`, `ci/android-signing-check.json` and final job/step records. Full logs are retained in ignored private evidence. The existing workflow's browser regressions do not change the platform support scope: Windows ARM64/macOS experimental; Web deferred.

## Remaining promotion gates

1. **Windows distribution decision and acceptance.** Complete signing or explicitly accept/document unsigned delivery. Validate the final downloaded hash, publisher/SmartScreen behavior, and WebView2-absent bootstrap. Android's local signed upgrade has passed; any different delivery channel or CI-produced APK must retain the established signer.
2. **Actual newer-candidate Windows updater delivery.** When authorized, deliberately publish a prerelease and test download/apply from an older public installation, including immediate restart and deferred close with pending work. The installer upgrade above does not substitute for that delivery path. Do not publish stable only to obtain this check.
3. **Promotion-facing information.** Align downloads, platform labels, release notes, backup/recovery/support guidance and known feature limitations. Windows ARM64/macOS remain experimental; Web remains deferred.

All guest harnesses finished and all three test guests were closed normally. No crash reproduction, debugger, pressure run, driver/firmware installation, device reset or host restart was performed. No new host crash was observed during this run; this does not resolve the earlier host faults.

## Windows signing and distribution assessment on October 23

**Signing and distribution acceptance remain open.** Static inspection preserved all six frozen Windows shipping files byte-for-byte. Setup, Aurora's app/portable launchers and Velopack's updater executables are unsigned; the two inspected copies of Microsoft's RestartAgent retain valid Microsoft signatures and timestamps. No current-user code-signing certificate is available. Evidence and options are in `artifacts/windows-signing-2026-10-23/inspection.json`, `authenticode.json` and `signing-options.json`.

### Licensing prerequisite

The actual Windows payload contains **iTextSharp 5.5.13.0**, referenced by the PDF-generation projects. The owner confirmed no existing commercial license or documented AGPL distribution decision. iText offers AGPLv3 and commercial terms. Distribution compliance must be resolved before stable promotion, whether signing is free or paid. The choices are to document and satisfy the AGPL distribution obligations, obtain an appropriate commercial grant, or replace the library. This assessment does not change Aurora's license or establish a legal violation. [iText licensing](https://itextpdf.com/how-buy/AGPLv3-license).

Of 75 resolved Windows NuGet packages, only `Aurora.Content` and `Aurora.Content.Contracts` lack license/license-URL metadata. Their vendored archives also contain no license or notice entry; their source repository and commit are recorded. The local `itextsharp.dll` and `DynamicExpresso.Core.dll` references lack NuGet license metadata. No loose LICENSE/NOTICE/COPYING files were found in the publish tree. Complete the license and attribution inventory, including templates/images/content, and ship required notices. Package metadata alone is not a compliance determination; embedded notices and private grants were not exhaustively reviewed. The documented legacy restoration authorization remains valid evidence of that particular authorization, not a license inventory for every dependency.

### Available routes

| Route | Current cost and practical fit |
| --- | --- |
| SignPath Foundation | Free if accepted. It requires component eligibility, repository-verifiable builds, MFA, named signing roles, a published policy and release approval. The publisher is SignPath Foundation. Resolve the licensing gaps and clarify its no-commercial-dual-licensing condition for iTextSharp before relying on acceptance. [Program](https://signpath.org/), [conditions](https://signpath.org/terms.html). |
| Azure Artifact Signing Basic | USD **$9.99/month/account**, including **5,000 signatures**; additional signatures are $0.005 each, subject to the actual billing quote/tax. A practical paid fallback for GitHub/Velopack. Individual public-trust applicants must be in the USA or Canada and complete ID verification with a matching individual billing identity. [Pricing](https://learn.microsoft.com/en-us/azure/artifact-signing/how-to-change-sku), [eligibility and setup](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart). |
| Microsoft Store MSIX | Free individual registration and Store signing, but requires MSIX packaging, certification and separate data/update acceptance. The Store does not re-sign this existing EXE installer or portable download. [Registration](https://blogs.windows.com/windowsdeveloper/2025/09/10/free-developer-registration-for-individual-developers-on-microsoft-store/), [submission and packaging](https://learn.microsoft.com/en-us/windows/apps/publish/get-started). |
| Certum Open Source | Cloud offering listed from **EUR49**, but currently out of stock; card/reader set listed from **EUR69**. Confirm validity, tax, availability, eligibility and CI integration before purchase. [Cloud offering](https://shop.certum.eu/open-source-code-signing-on-simplysign.html), [catalog](https://shop.certum.eu/code-signing.html). |
| Other public-trust certificate | Vendor pricing plus protected-key hardware/cloud costs. Identity verification still applies. EV signing does not guarantee an immediate SmartScreen bypass. [Microsoft comparison](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options). |
| Unsigned delivery | No signing fee, with Windows warnings and possible managed-device blocking. Requires an explicit owner decision, which has not been given. A self-signed certificate or published checksum does not establish a publicly trusted Windows publisher. [Microsoft comparison](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options). |

The recommendation is to resolve licensing/notices and pursue SignPath first, with Azure Basic as the CI-friendly paid fallback. Neither provider acceptance nor warning-free Windows delivery is established.

### Signed candidate acceptance

Integrate the selected signer **during Velopack packaging**, covering the generated installer, launchers and updater as well as intended application binaries. Preserve existing upstream signatures and agree the Foundation signing policy for Velopack-generated executables. A provider's configuration must match the actual binary metadata; some restored DLLs intentionally retain legacy versions. The frozen workflow uses Velopack **1.0.1**, so confirm that version's options rather than assuming current documentation for 1.3 applies. Post-signing Setup alone leaves portable/update payloads unsigned and can invalidate published hashes. [Velopack signing integration](https://docs.velopack.io/packaging/signing).

Produce the signed candidate separately, regenerate feeds/hashes and inspect publisher, chain, timestamp and signature coverage. Then validate the actual Internet download and Windows prompts, absent-WebView2 bootstrap, installation/upgrade/portable behavior and actual newer-candidate update delivery with immediate restart and deferred dirty close. New publisher reputation may still produce SmartScreen warnings; evaluate the observed experience instead of promising none. [Microsoft signing behavior](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options).

This pass launched no app or VM, touched no app data, installed nothing and made no host/device changes. No service application, account/permission grant, purchase, signing, commit, push, tag or release was performed. Windows ARM64 and macOS remain experimental; Web remains deferred.

## Licensing follow up on October 23

The owner chose to retain permissive application licensing and investigate replacing iTextSharp. A local notice bundle for 191 components, offline About entry and release checks are now implemented and verified. The PDF backend remains unchanged: the LGPL fork was not a drop-in replacement, and MIT PDFsharp 6.2.4 passed only a bounded real-template form-write experiment. The owner subsequently accepted LGPL if it is the better fit. The LGPL fork is recommended after five passing capability checks; its actual migration/acceptance and remaining asset/content grants are still open. The Aurora.Content/Contracts MIT grant is now recorded. See [PDF library and notice work](pdf-library-and-notices-2026-10-23.md) for evidence and the scope of the checks. All seven original RC1 shipping artifacts remain unchanged; these uncommitted changes require a later candidate.
