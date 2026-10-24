# PDF migration and third-party distribution review - 2026-10-23

The PDF migration is accepted locally. Retain **iTextSharp.LGPLv2.Core 3.8.6** for the existing Windows PDF feature set. Shipping source projects no longer reference the AGPL iTextSharp 5 DLL. Aurora original code remains MIT; the replacement library is LGPL-2.0-only. Overall stable-release clearance remains open for inherited asset/content rights, Windows signing/distribution acceptance, and a newly frozen candidate with final signed-package acceptance.

Windows x64 and Android remain the stable targets. Windows ARM64 and macOS remain experimental; Web remains deferred. At this migration checkpoint Android PDF export remained unavailable; the checkpoint validated the shared engine on Windows and the changed Android package dependencies. The subsequent [Android PDF acceptance](android-pdf-export-acceptance-2026-10-23.md) enables export and supersedes this checkpoint for Android availability, PDF font assets, the notice count and corresponding source materials. The original migration test results below remain historical evidence.

## Implementation

Aurora.Documents, Aurora.Logic and their source consumers use the pinned NuGet package. The migration adapts rotated multi-widget field positions, HtmlWorker, color constants, text fitting and rectangle-based columns. Fixed column leading preserves the prior per-chunk spacing without an ignored compatibility shim. PdfCopyFields merges pages and retains editable fields. Static sheets retain the second flattening pass for image buttons.

The expanded acceptance work corrected these concrete defects:

- Optional null field values now become empty strings before calling the stricter library.
- Unchecked checkboxes use the canonical PDF Off state, keeping field values and widget appearance states consistent.
- The main stealth-disadvantage checkbox now uses its actual Yes state instead of Ja.
- Legacy attack rows two and three use their respective attacks; a missing attack row no longer throws.
- Card tables tolerate null placeholders created by merged cells, preserving cell text and subsequent content.
- Page-generator disposal closes the document/writer before their backing stream.
- Form fields use an embedded Open Sans fallback for Unicode glyphs missing from legacy templates; this fixed a dropped accented character in flattened names. The existing Apache-2.0 font notice covered that initial embedded copy; the Android follow-up uses separately bundled OFL Open Sans fonts.

The old itextsharp.dll remains solely for isolated historical oracle tests. It is absent from the migrated Windows publish and the release source bundle. Its test-only attribution remains separate from the shipping inventory. The eight document assertions compile separately against each engine. The API comparison preserves all 166 required source signatures with eight additions. Public PDF types now belong to the fork's assembly; consumers using those types must recompile.

## PDF acceptance

The final PDF group passed **75/75** tests against the final PDF implementation. The broader core suite passed **1,736 tests with one documented optional 2024-content skip** before the final merged-cell/disposal/font-fallback fixes; the complete PDF group was rerun after those fixes. No broad-suite result is claimed for code changed after that broad run.

| Coverage | Cases and assertions |
| --- | --- |
| Full sheets | 32 combinations of editable/static, formatting on/off, legacy/modern details, background and spell layouts |
| Included content | Character, background, equipment, notes, companion, two independent spellcasters, ability values, prepared spells and checkbox states |
| Card writers | All four card categories, 40 cards per export, nine-card page capacity, shared and separate pages, editable/static |
| Card integration | Spell, attack, item and feature cards through CharacterSheetEx; category page-break settings and content retention |
| Optional pages | Each non-spell sheet page exported independently |
| Images | Portrait, organization and companion images; modern/legacy background; editable/static; independent image-count checks after flattening |
| Rich descriptions | Paragraphs, emphasis, ordered/unordered lists, tables, colspan/rowspan cell text, Unicode and final content markers |
| Additional accuracy | All six scores/modifiers with flipped/unflipped modern/legacy layouts, editable/static, Unicode names, 70-line notes and wrapped-card final markers |
| Existing regressions | Six portrait/flattening/spell-layout cases and eight document compatibility cases |

Independent pypdf inspection passed on **72 generated PDFs / 294 pages / 12,858 widgets**, with zero canonical-field/widget-value conflicts, invalid checked states or missing nonempty text appearances. Four independent edit/save/reopen round trips passed. Flattened outputs have no live fields/widgets. Card tables retain Aurora's existing text-row presentation.

Poppler rendered **320 final pages**, including the four independently edited documents. Automated raster comparison covers **42 baseline/candidate exports and 250 pages**. Every saved-field difference is accounted for by Off normalization, corrected legacy attack mapping, or the deliberately enabled positive checkbox fixture values. The largest mean pixel difference is 1.901/255; that number is a comparison statistic, not proof of layout quality. Representative modern/legacy sheets, multi-caster pages, every card category, merged-cell descriptions, Unicode and image pages were inspected visually. No new clipping, overlap or missing content was observed in those inspected pages.

The pre-migration baseline uses the frozen RC1 binaries through an isolated harness. Synthetic fixture strings include HTML in some raw text fields; those remain literal in the baseline and candidate as expected for those inputs. Ordinary app-generated descriptions choose plain or formatted input according to the export preference.

## Notices and corresponding source

The reviewed inventory contains **196 components**, including conservatively retained build dependencies, complete legal texts and copyright/source records. Coverage checks pass for **79 Windows** and **165 Android** resolved packages. The offline About -> Licenses and notices view lists the LGPL library prominently and explains modification/relinking rights. All **47 generated legal files** are packaged. Sandboxed browser checks passed at 390 and 1280 pixels, including MIT/LGPL license navigation, no horizontal overflow and no external requests.

The complete unmodified upstream archive is included locally at third-party/sources/iTextSharp.LGPLv2.Core-3.8.6-8f62a2d7.zip. Its exact source commit is 8f62a2d7be211c4ca9b5bfb953c7443daeeb609c and its SHA-256 is 2424eaf6ecf71bd099de2f7ec83d26dd13e30b1dbcd84e4c1206ac049f48779d. The archive, library license and published NuGet package hash are recorded in third-party/sources/manifest.json. The source rebuilt successfully as 3.8.6-local.1, and the actual Aurora.Documents source was relinked against it: all eight document compatibility assertions passed.

third-party/LGPL-RELINKING.md supplies the library notice, own-use modification/reverse-engineering permission and rebuild/relink instructions. tools/licensing/build_source_bundle.py creates a matching aurora-corresponding-source.zip containing application source/build files, vendored object packages, first-party metadata fixtures, the upstream library archive and per-file hashes. It excludes private configuration/keys, local incident reports, scratch output and the old AGPL PDF engine. The release workflow attaches that source asset at the same download location as the binaries. Offline redistributors must include matching source materials too.

The notice checker verifies source archive integrity, package pins, dependency coverage and absence of the old PDF dependency in shipping records. The PDF licensing issue is closed. The strict distribution guard remains closed for **legacy-assets** and **Bundled SRD content notice** scope review. Existing MIT owner confirmation for Aurora.Content/Aurora.Content.Contracts remains recorded; inherited art/templates and actual game-content rights still need their own evidence.

## Native package checks and preservation

- Final Windows x64 self-contained publishing passed. Its Aurora.Logic, Aurora.Documents and iTextSharp.LGPLv2.Core DLLs are byte-identical to the assemblies exercised by the final PDF tests. No itextsharp.dll is present.
- Android ARM64 compilation and unsigned APK creation passed. The package contains all 47 legal files and the 196-entry notice index. The new libSkiaSharp.so is AArch64 and every ELF load segment has 16 KB alignment. The aligned unsigned APK passed zipalign's 16 KB check.
- The legacy WPF source also builds successfully (existing platform/obsolete warnings).
- The library source rebuild, separately compiled historical oracle tests (8/8), relinked document tests (8/8), and required API comparison passed.
- These are local, uncommitted validation outputs. No release tag, push, installer publication or production-key signing was performed. Fresh signed-package/installed-app acceptance belongs to the next candidate freeze.
- All seven frozen RC1 shipping files retain their recorded sizes and hashes. No installed app, personal character, app-data store, connected account, phone installation, driver, VM, or host restart was used.

Detailed evidence is under artifacts/licensing-2026-10-23/: final PDF and broad test logs in private/, pdf-form-validation.json, pdf-visual-validation.json, pdf-comparison.json, pdf-package-validation.json, android-pdf-native-alignment.json, notice-ui.json, and the matching source bundle. The historical investigation files are retained as earlier evidence and are superseded by this acceptance record.

## Later public-baseline update

The counts and package evidence above describe the PDF-engine migration before the SRD-only baseline cleanup. The completed content/asset review now records 204 components, 652 asset paths and 55 generated notice files, with the remaining licensing records resolved. See [the bundled-content review](bundled-content-and-assets-2026-10-23.md) for the final scope and tests. The matching source bundler now excludes all historical oracle binaries and saved-character fixtures containing imported non-SRD descriptions; source-native PDF tests remain runnable without them. The later card-fitting, list-marker and material-component layout fixes require a fresh native release candidate. Earlier package checks do not validate that new candidate.
