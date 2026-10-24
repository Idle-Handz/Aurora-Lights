# Android PDF export acceptance - 2026-10-23

Android PDF export is implemented and accepted locally on the connected Samsung SM-S366V (Android API 36, ARM64). The phone retains its existing Aurora installation and app data. Windows x64 and Android remain the v1.0 stable targets; Windows ARM64 and macOS remain experimental, and Web is deferred. This is an unfrozen local acceptance build, not a published release.

## Implementation

The existing draft-safe export path now runs the shared PDF generator on Android. Save PDF, Share PDF and Open PDF are separate actions. Save uses Android's document picker and reports success only after the selected provider finishes writing; cancellation returns without claiming a save. Android creates a distinct file when the selected name already exists. Failed writes attempt to remove only the newly created incomplete document. Share/Open use PDF MIME types and temporary content-URI read grants.

Sheet layout, formatting, ability inversion, prepared-spell filtering, legacy spell layout, optional background/equipment pages and all four card categories are available on the Android Sheet screen. Controls are disabled during export. The existing character-context and file-write locks still capture the selected tab's current draft without saving its character file or marking it clean.

Four pinned Open Sans 3.003 fonts provide actual regular, bold, italic and bold-italic faces without desktop Calibri. Both font helpers use embedded fallback fonts, including form substitution. Their unmodified upstream binaries, hashes, copyright and OFL-1.1 text are recorded in the offline notices. The pre-existing UI font keeps its separate Apache notice. The inventory now contains 200 components and 48 generated legal files.

Each temporary export receives a unique folder and safe filename. The filename limit counts UTF-8 bytes and retains complete Unicode characters. Cleanup removes only recognized export folders older than two days; recent copies remain available to external readers. This never targets character files or saved user documents.

## Validation

| Check | Result |
| --- | --- |
| Desktop focused PDF/export group | 81 passed, no skips |
| Draft/file preservation and filename checks | 20 passed, no skips; two overlap the first run |
| Physical Android PDF assertions | 72 distinct cases passed |
| Independent PDF inspection | 76 base files, 298 pages, 12,858 widgets; zero field/widget errors |
| Independent editable round trips | Four edit/save/reopen cases passed |
| Phone-generated raster output | 80 PDFs including edited copies, 324 pages rendered |
| Installed Aurora output | Four additional PDFs / 16 pages rendered and checked |
| Real installed-app actions | Save, cancel/retry, duplicate-name save, Share, Open and return to Aurora passed |

The Android engine cases cover 32 full-sheet combinations; editable/static output; formatting on/off; modern/legacy layouts; flipped abilities; both spell layouts and multiple casters; independent optional pages; all four card categories and their page-break settings; portrait/organization/companion images; rich descriptions with lists and spanning tables; Unicode; dense notes; wrapped card endings; and all four embedded font faces. Cached-export cleanup and byte-limited filenames also execute on the phone.

The separate com.auroralights.pdfacceptance application links the real PDF implementation and shared assertions while keeping its own data directory. The 32 full-sheet cases completed before a test-driver attribute-array discovery error; the driver was corrected to use xUnit's data API and the remaining 40 cases passed. No PDF assertion failed. The helper also required a clean rebuild/dynamic JNI registration after an incremental callback problem. These were test-harness issues, and both incomplete attempts are retained privately rather than counted as successful full runs.

On the actual installed Aurora package, a synthetic Human Wizard exported a static four-page sheet and a form-fillable four-page sheet. An isolated receiver verified that both Share and Open could read their content URIs. The editable output has 452 leaf fields (467 entries in pypdf, which also counts field groups). Name, biography ending, page structure and field values were preserved. Drive PDF Viewer displayed the editable PDF locally; Add to Drive was not used. No file was uploaded or sent to another account/service. The original static save survived a second save under the same suggested name. Form Fillable and Formatting were restored to their original settings.

Poppler rendered all 340 pages across the engine and installed-app outputs. Representative contact sheets, all card layouts, image pages, Unicode/style fixtures, modern/legacy pages and the installed-app documents were inspected. No new clipping, overlap or missing content was observed in those inspected pages. Automated text/field checks cover the remaining fixture content; rendering alone is not treated as proof of field correctness.

## Package and data preservation

The accepted signed APK is version code 214964203, SHA-256 7ff343647e3e41f6f4003d6ba88284b887f98f8bc58cdde18858ea38e3ef928b. Its signing certificate matches the previously installed Aurora package. Signature verification, 16 KB ZIP alignment and all 12 ARM64 native libraries' ELF alignment passed. All 48 legal files are packaged.

An initial local partial-target APK had inconsistent generated Android resource artifacts and failed startup. A clean complete dotnet publish produced the working package. A speculative saved-screen-state change was removed; no lifecycle workaround is included. Release acceptance must use the complete publishing pipeline and an actual installed launch, not only compilation or APK parsing. The existing release workflow already uses publishing.

Before updating, the installed APK and every accessible external app file were backed up. After testing and cleanup, all 2,502 original files / 240,504,313 bytes have identical SHA-256 hashes, with zero missing or changed originals and no added synthetic external files. First-install identity and the app-data directory were retained. Aurora was never uninstalled or cleared. The connected Drive account and existing cloud entries remained visible. The synthetic local character, three verified test downloads and the separate test app were removed. Temporary Aurora PDF copies remain in its ordinary cache for the bounded cleanup policy.

Matching corresponding-source materials are generated at artifacts/android-pdf-export-2026-10-23/aurora-corresponding-source.zip, with a per-file SOURCE-MANIFEST.json. They include the PDF implementation, embedded fonts, legal texts and exact LGPL library source, and exclude private configuration/signing keys. Source-bundle verification is recorded separately in the evidence directory.

Evidence is under artifacts/android-pdf-export-2026-10-23/: phone-tests.json, installed-app-pdf-acceptance.json, phone-pdf-form-validation.json, phone-pdf-rendering.json, installed-pdf-rendering.json, android-candidate.json, android-package-validation.json, data-preservation.json and cleanup.json. Screenshots, generated PDFs, packages and backups remain private and ignored by Git.

## Scope limits

This validates the connected API 36 phone; it does not establish physical coverage of every supported Android version or device. No new disk-pressure, low-memory, driver, VM or host-restart testing was performed. The broad repository suite was not repeated. Windows signing/distribution, inherited content/asset rights and freezing/accepting the final release candidate remain separate v1.0 gates. Changes are local and uncommitted; nothing was pushed, tagged or published.

## Later SRD baseline update

The phone acceptance above predates the SRD-only resources and later card-layout fixes. Content and asset licensing is now resolved for the reviewed source versions; see [the final bundled-content review](bundled-content-and-assets-2026-10-23.md). The installed phone app and its data were not changed during that review. A new signed release candidate and native package acceptance are still required before publishing these changes.
